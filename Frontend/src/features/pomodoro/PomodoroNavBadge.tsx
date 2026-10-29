import { usePomodoro } from './pomodoroContext'
import { PHASE_LABELS, formatRemaining } from './pomodoroTimer'

/** Mały licznik w pasku nawigacji, widoczny na innych zakładkach, gdy timer jest w toku. Prowadzi do #pomodoro. */
export function PomodoroNavBadge() {
  const { state, remainingMs, running, atPhaseStart } = usePomodoro()

  if (atPhaseStart) return null

  const label = running ? PHASE_LABELS[state.phase] : 'Pauza'
  const dot = running ? (state.phase === 'work' ? 'bg-blue-600' : state.phase === 'shortBreak' ? 'bg-emerald-600' : 'bg-violet-600') : 'bg-slate-400'

  return (
    <a
      href="#pomodoro"
      title="Przejdź do timera Pomodoro"
      className="flex items-center gap-2 rounded-full bg-slate-100 px-3 py-1 text-sm whitespace-nowrap text-slate-700 hover:bg-slate-200 focus-visible:outline-2 focus-visible:outline-blue-600"
    >
      <span className={`size-2 shrink-0 rounded-full ${dot}`} aria-hidden />
      <span className="hidden sm:inline">{label}</span>
      <span className="font-medium tabular-nums">{formatRemaining(remainingMs)}</span>
    </a>
  )
}
