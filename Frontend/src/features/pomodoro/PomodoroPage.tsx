import { useEffect, useState } from 'react'
import { Button } from '../../components/ui/Button'
import {
  DEFAULT_POMODORO_SETTINGS,
  PHASE_LABELS,
  createInitialState,
  formatRemaining,
  getRemainingMs,
  isAtPhaseStart,
  pause,
  phaseDurationMs,
  resetPhase,
  start,
  type PomodoroPhase,
  type PomodoroState,
} from './pomodoroTimer'

/** Kolor fazy wzmacnia tylko jej nazwę, która zawsze jest napisana obok. */
const phaseStyles: Record<PomodoroPhase, { ring: string; text: string }> = {
  work: { ring: 'stroke-blue-600', text: 'text-blue-700' },
  shortBreak: { ring: 'stroke-emerald-600', text: 'text-emerald-700' },
  longBreak: { ring: 'stroke-violet-600', text: 'text-violet-700' },
}

/** Jak często odświeżamy licznik. Sam czas liczy się ze znaczników czasu, więc to wpływa tylko na płynność. */
const TICK_MS = 250

const RING_SIZE = 288
const RING_STROKE = 12
const RING_RADIUS = (RING_SIZE - RING_STROKE) / 2
const RING_CIRCUMFERENCE = 2 * Math.PI * RING_RADIUS

export function PomodoroPage() {
  const settings = DEFAULT_POMODORO_SETTINGS
  const [state, setState] = useState<PomodoroState>(() => createInitialState(settings))
  const [now, setNow] = useState(() => Date.now())
  const running = state.endsAt !== null

  // Tyka tylko, gdy timer działa. Po dojściu do zera zatrzymuje się na 00:00.
  useEffect(() => {
    if (!running) return
    const id = window.setInterval(() => {
      const time = Date.now()
      setNow(time)
      setState((current) => (getRemainingMs(current, time) === 0 ? pause(current, time) : current))
    }, TICK_MS)
    return () => window.clearInterval(id)
  }, [running])

  const remainingMs = getRemainingMs(state, now)
  const duration = phaseDurationMs(state.phase, settings)
  const fraction = duration > 0 ? remainingMs / duration : 0
  const atPhaseStart = isAtPhaseStart(state, settings)
  const finished = !running && remainingMs === 0
  const styles = phaseStyles[state.phase]
  const mainLabel = running ? 'Pauza' : finished ? 'Nowa sesja' : atPhaseStart ? 'Start' : 'Wznów'

  const handleMain = () => {
    const time = Date.now()
    setNow(time)
    setState((current) => {
      if (current.endsAt !== null) return pause(current, time)
      // Po zakończonej sesji (00:00) przycisk od razu zaczyna następną.
      const ready = getRemainingMs(current, time) === 0 ? resetPhase(current, settings) : current
      return start(ready, time)
    })
  }

  const handleReset = () => {
    setNow(Date.now())
    setState((current) => resetPhase(current, settings))
  }

  return (
    <div className="h-full overflow-y-auto bg-white text-slate-900">
      <div className="mx-auto flex max-w-md flex-col items-center gap-8 px-5 pt-8 pb-16">
        <h1 className="self-start text-lg font-semibold">Pomodoro</h1>

        <div className="relative" style={{ width: RING_SIZE, height: RING_SIZE }}>
          <svg viewBox={`0 0 ${RING_SIZE} ${RING_SIZE}`} className="h-full w-full -rotate-90" aria-hidden>
            <circle
              cx={RING_SIZE / 2}
              cy={RING_SIZE / 2}
              r={RING_RADIUS}
              fill="none"
              strokeWidth={RING_STROKE}
              className="stroke-slate-100"
            />
            <circle
              cx={RING_SIZE / 2}
              cy={RING_SIZE / 2}
              r={RING_RADIUS}
              fill="none"
              strokeWidth={RING_STROKE}
              strokeLinecap="round"
              strokeDasharray={RING_CIRCUMFERENCE}
              strokeDashoffset={RING_CIRCUMFERENCE * (1 - fraction)}
              className={`${styles.ring} transition-[stroke-dashoffset] duration-300 ease-linear`}
            />
          </svg>
          <div className="absolute inset-0 flex flex-col items-center justify-center gap-1">
            <p role="timer" className="text-6xl font-semibold tracking-tight text-slate-900 tabular-nums">
              {formatRemaining(remainingMs)}
            </p>
            <p aria-live="polite" className={`text-base font-medium ${styles.text}`}>
              {PHASE_LABELS[state.phase]}
              {finished ? ' · koniec' : !running && !atPhaseStart && ' · pauza'}
            </p>
          </div>
        </div>

        <div className="flex w-full flex-col items-center gap-3">
          <button
            type="button"
            onClick={handleMain}
            className="min-h-12 w-full max-w-60 rounded-full bg-blue-600 px-6 text-base font-semibold text-white hover:bg-blue-700 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600"
          >
            {mainLabel}
          </button>
          <Button variant="secondary" onClick={handleReset} disabled={atPhaseStart} className="min-h-11 w-full max-w-60 sm:min-h-9">
            Reset
          </Button>
        </div>

        <p className="w-full rounded-lg bg-slate-50 px-4 py-3 text-sm text-slate-600">
          Sesja pracy trwa {settings.workMinutes} min. Po jej zakończeniu kliknij „Nowa sesja”, żeby zacząć kolejną.
        </p>
      </div>
    </div>
  )
}
