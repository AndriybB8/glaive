import { Button } from '../../components/ui/Button'
import { usePomodoro } from './pomodoroContext'
import { PHASE_LABELS, currentSessionNumber, formatRemaining, type PomodoroPhase } from './pomodoroTimer'

/** Kolor fazy wzmacnia tylko jej nazwę, która zawsze jest napisana obok. */
const phaseStyles: Record<PomodoroPhase, { ring: string; text: string; pill: string }> = {
  work: { ring: 'stroke-blue-600', text: 'text-blue-700', pill: 'bg-blue-600 text-white' },
  shortBreak: { ring: 'stroke-emerald-600', text: 'text-emerald-700', pill: 'bg-emerald-600 text-white' },
  longBreak: { ring: 'stroke-violet-600', text: 'text-violet-700', pill: 'bg-violet-600 text-white' },
}

const PHASES: PomodoroPhase[] = ['work', 'shortBreak', 'longBreak']

const RING_SIZE = 288
const RING_STROKE = 12
const RING_RADIUS = (RING_SIZE - RING_STROKE) / 2
const RING_CIRCUMFERENCE = 2 * Math.PI * RING_RADIUS

export function PomodoroPage() {
  const { state, settings, remainingMs, phaseDurationMs, running, atPhaseStart, nextPhase, start, pause, reset, skip, restartCycle } =
    usePomodoro()

  const styles = phaseStyles[state.phase]
  const fraction = phaseDurationMs > 0 ? remainingMs / phaseDurationMs : 0
  const session = currentSessionNumber(state, settings)
  const atCycleStart = state.phase === 'work' && state.completedWorkSessions === 0 && atPhaseStart
  const mainLabel = running ? 'Pauza' : atPhaseStart ? 'Start' : 'Wznów'

  return (
    <div className="h-full overflow-y-auto bg-white text-slate-900">
      <div className="mx-auto flex max-w-md flex-col items-center gap-8 px-5 pt-8 pb-16">
        <h1 className="self-start text-lg font-semibold">Pomodoro</h1>

        <ul aria-label="Fazy cyklu" className="flex flex-wrap justify-center gap-2 text-sm">
          {PHASES.map((phase) => {
            const active = phase === state.phase
            return (
              <li
                key={phase}
                aria-current={active ? 'step' : undefined}
                className={`rounded-full px-3 py-1 font-medium ${active ? phaseStyles[phase].pill : 'bg-slate-100 text-slate-600'}`}
              >
                {PHASE_LABELS[phase]}
              </li>
            )
          })}
        </ul>

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
            {/* Ogłasza czytnikom ekranu zmianę fazy (nie co sekundę). */}
            <p aria-live="polite" className={`text-base font-medium ${styles.text}`}>
              {PHASE_LABELS[state.phase]}
              {!running && !atPhaseStart && ' · pauza'}
            </p>
          </div>
        </div>

        <div className="flex flex-col items-center gap-2">
          <p className="text-sm text-slate-600">
            Sesja {session} z {settings.sessionsBeforeLongBreak}
          </p>
          <div className="flex gap-2" aria-hidden>
            {Array.from({ length: settings.sessionsBeforeLongBreak }, (_, i) => (
              <span
                key={i}
                className={`size-2.5 rounded-full ${i < state.completedWorkSessions ? 'bg-slate-700' : i === session - 1 && state.phase === 'work' ? 'bg-slate-400' : 'bg-slate-200'}`}
              />
            ))}
          </div>
        </div>

        <div className="flex w-full flex-col items-center gap-3">
          <button
            type="button"
            onClick={running ? pause : start}
            className="min-h-12 w-full max-w-60 rounded-full bg-blue-600 px-6 text-base font-semibold text-white hover:bg-blue-700 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600"
          >
            {mainLabel}
          </button>
          <div className="grid w-full max-w-60 grid-cols-2 gap-2">
            <Button variant="secondary" onClick={reset} disabled={atPhaseStart} className="min-h-11 sm:min-h-9">
              Reset
            </Button>
            <Button
              variant="secondary"
              onClick={skip}
              title={`Przejdź do: ${PHASE_LABELS[nextPhase]}`}
              aria-label={`Pomiń, przejdź do: ${PHASE_LABELS[nextPhase]}`}
              className="min-h-11 sm:min-h-9"
            >
              Pomiń
            </Button>
          </div>
          {!atCycleStart && (
            <Button variant="ghost" onClick={restartCycle} className="min-h-11 sm:min-h-0">
              Zacznij cykl od nowa
            </Button>
          )}
        </div>

        <div className="w-full rounded-lg bg-slate-50 px-4 py-3 text-sm text-slate-600">
          <p>
            {settings.workMinutes} min pracy, {settings.shortBreakMinutes} min przerwy, a po {settings.sessionsBeforeLongBreak}{' '}
            sesjach {settings.longBreakMinutes} min długiej przerwy. Przerwy i praca zmieniają się same.
          </p>
          <p className="mt-1">Timer działa dalej, gdy przejdziesz do notatek, przełączysz kartę albo odświeżysz stronę.</p>
        </div>
      </div>
    </div>
  )
}
