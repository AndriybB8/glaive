import { useId, useState } from 'react'
import { Button } from '../../components/ui/Button'
import { SelectField } from '../../components/ui/SelectField'
import { usePomodoro } from './pomodoroContext'
import { PomodoroSettingsDialog } from './PomodoroSettingsDialog'
import { POMODORO_SOUNDS, type PomodoroSoundId } from './pomodoroSound'
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
  const {
    state,
    settings,
    settingsStatus,
    remainingMs,
    phaseDurationMs,
    running,
    atPhaseStart,
    nextPhase,
    start,
    pause,
    reset,
    skip,
    restartCycle,
    soundSettings,
    updateSoundSettings,
    previewSound,
  } = usePomodoro()
  const volumeId = useId()
  const timesId = useId()
  const [settingsOpen, setSettingsOpen] = useState(false)

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

        <section aria-labelledby={timesId} className="flex w-full flex-col gap-3 rounded-lg border border-slate-200 p-4">
          <div className="flex items-center justify-between gap-3">
            <h2 id={timesId} className="text-sm font-semibold text-slate-900">
              Czasy cyklu
            </h2>
            <Button variant="secondary" onClick={() => setSettingsOpen(true)} className="min-h-11 sm:min-h-9">
              Zmień
            </Button>
          </div>
          <dl className="grid grid-cols-2 gap-x-4 gap-y-2 text-sm">
            <div>
              <dt className="text-slate-500">Praca</dt>
              <dd className="font-medium text-slate-900 tabular-nums">{settings.workMinutes} min</dd>
            </div>
            <div>
              <dt className="text-slate-500">Sesje w cyklu</dt>
              <dd className="font-medium text-slate-900 tabular-nums">{settings.sessionsBeforeLongBreak}</dd>
            </div>
            <div>
              <dt className="text-slate-500">Krótka przerwa</dt>
              <dd className="font-medium text-slate-900 tabular-nums">{settings.shortBreakMinutes} min</dd>
            </div>
            <div>
              <dt className="text-slate-500">Długa przerwa</dt>
              <dd className="font-medium text-slate-900 tabular-nums">{settings.longBreakMinutes} min</dd>
            </div>
          </dl>
          {settingsStatus === 'error' && (
            <p className="text-sm text-amber-800">
              Nie udało się wczytać ustawień z konta. Timer używa ostatnio zapisanych na tym urządzeniu albo domyślnych.
            </p>
          )}
        </section>

        <section aria-labelledby={`${volumeId}-heading`} className="flex w-full flex-col gap-4 rounded-lg border border-slate-200 p-4">
          <div className="flex items-center justify-between gap-3">
            <h2 id={`${volumeId}-heading`} className="text-sm font-semibold text-slate-900">
              Dźwięk na koniec fazy
            </h2>
            <button
              type="button"
              role="switch"
              aria-checked={!soundSettings.muted}
              onClick={() => updateSoundSettings({ muted: !soundSettings.muted })}
              className="flex min-h-11 items-center gap-2 rounded-full px-1 text-sm text-slate-700 focus-visible:outline-2 focus-visible:outline-blue-600 sm:min-h-8"
            >
              <span
                aria-hidden
                className={`relative h-6 w-11 shrink-0 rounded-full transition-colors ${soundSettings.muted ? 'bg-slate-300' : 'bg-blue-600'}`}
              >
                <span
                  className={`absolute top-0.5 size-5 rounded-full bg-white shadow transition-[left] ${soundSettings.muted ? 'left-0.5' : 'left-5.5'}`}
                />
              </span>
              {soundSettings.muted ? 'Wyciszony' : 'Włączony'}
            </button>
          </div>
          <SelectField
            label="Dźwięk"
            options={POMODORO_SOUNDS}
            value={soundSettings.soundId}
            onChange={(event) => updateSoundSettings({ soundId: event.target.value as PomodoroSoundId })}
          />
          <div className="flex flex-col gap-1.5">
            <div className="flex items-baseline justify-between">
              <label htmlFor={volumeId} className="text-sm font-medium text-slate-700">
                Głośność
              </label>
              <span className="text-sm text-slate-600 tabular-nums">{soundSettings.volume}%</span>
            </div>
            <input
              id={volumeId}
              type="range"
              min={0}
              max={100}
              step={5}
              value={soundSettings.volume}
              onChange={(event) => updateSoundSettings({ volume: Number(event.target.value) })}
              className="h-11 w-full accent-blue-600 sm:h-8"
            />
          </div>
          <Button variant="secondary" onClick={previewSound} className="min-h-11 self-start sm:min-h-9">
            Odsłuchaj
          </Button>
        </section>

        <div className="w-full rounded-lg bg-slate-50 px-4 py-3 text-sm text-slate-600">
          <p>Przerwy i praca zmieniają się same. Timer działa dalej, gdy przejdziesz do notatek, przełączysz kartę albo odświeżysz stronę.</p>
          <p className="mt-1">
            Koniec fazy zawsze widać też na ekranie i w tytule karty (🔔) – także przy wyciszonym dźwięku. Przeglądarka nie
            zna trybu wyciszenia telefonu, dlatego dźwięk wyciszasz tutaj.
          </p>
        </div>
      </div>
      {settingsOpen && <PomodoroSettingsDialog onClose={() => setSettingsOpen(false)} />}
    </div>
  )
}
