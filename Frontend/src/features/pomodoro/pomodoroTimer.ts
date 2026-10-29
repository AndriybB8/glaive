/**
 * Logika timera Pomodoro – czyste funkcje, bez Reacta i bez zegara.
 * Każda funkcja dostaje aktualny czas (`now`, ms od epoki) z zewnątrz, więc łatwo ją przetestować.
 *
 * Czas liczymy od znaczników czasu (`endsAt`), a nie zliczając tyknięcia: przeglądarka spowalnia
 * `setInterval` w kartach w tle, a uśpiony laptop nie tyka wcale. Po powrocie wystarczy porównać `now` z `endsAt`.
 */

export type PomodoroPhase = 'work' | 'shortBreak' | 'longBreak'

/** Czasy cyklu. Na razie stałe ({@link DEFAULT_POMODORO_SETTINGS}); zapis własnych ustawień to SCRUM-43. */
export interface PomodoroSettings {
  workMinutes: number
  shortBreakMinutes: number
  longBreakMinutes: number
  /** Po tylu sesjach pracy zamiast krótkiej jest długa przerwa. */
  sessionsBeforeLongBreak: number
}

export const DEFAULT_POMODORO_SETTINGS: PomodoroSettings = {
  workMinutes: 25,
  shortBreakMinutes: 5,
  longBreakMinutes: 15,
  sessionsBeforeLongBreak: 4,
}

export interface PomodoroState {
  phase: PomodoroPhase
  /** Ukończone sesje pracy w bieżącym cyklu (zerowane po długiej przerwie). */
  completedWorkSessions: number
  /** Timer działa: moment końca bieżącej fazy (ms od epoki). Zatrzymany: null. */
  endsAt: number | null
  /** Timer zatrzymany: ile zostało do końca fazy (ms). Gdy działa, liczy się `endsAt`. */
  remainingMs: number
}

export const PHASE_LABELS: Record<PomodoroPhase, string> = {
  work: 'Praca',
  shortBreak: 'Krótka przerwa',
  longBreak: 'Długa przerwa',
}

const MINUTE_MS = 60_000

export function phaseDurationMs(phase: PomodoroPhase, settings: PomodoroSettings): number {
  switch (phase) {
    case 'work':
      return settings.workMinutes * MINUTE_MS
    case 'shortBreak':
      return settings.shortBreakMinutes * MINUTE_MS
    case 'longBreak':
      return settings.longBreakMinutes * MINUTE_MS
  }
}

/** Początek cyklu: pierwsza sesja pracy, zatrzymana, pełny czas. */
export function createInitialState(settings: PomodoroSettings): PomodoroState {
  return { phase: 'work', completedWorkSessions: 0, endsAt: null, remainingMs: phaseDurationMs('work', settings) }
}

export function isRunning(state: PomodoroState): boolean {
  return state.endsAt !== null
}

export function getRemainingMs(state: PomodoroState, now: number): number {
  return state.endsAt === null ? state.remainingMs : Math.max(0, state.endsAt - now)
}

/** Faza nie została jeszcze ruszona (pełny czas, zatrzymana). */
export function isAtPhaseStart(state: PomodoroState, settings: PomodoroSettings): boolean {
  return state.endsAt === null && state.remainingMs === phaseDurationMs(state.phase, settings)
}

export function start(state: PomodoroState, now: number): PomodoroState {
  if (state.endsAt !== null) return state
  return { ...state, endsAt: now + state.remainingMs, remainingMs: 0 }
}

export function pause(state: PomodoroState, now: number): PomodoroState {
  if (state.endsAt === null) return state
  return { ...state, endsAt: null, remainingMs: Math.max(0, state.endsAt - now) }
}

/** Zatrzymuje timer i przywraca pełny czas bieżącej fazy. Licznik sesji zostaje. */
export function resetPhase(state: PomodoroState, settings: PomodoroSettings): PomodoroState {
  return { ...state, endsAt: null, remainingMs: phaseDurationMs(state.phase, settings) }
}

/** „mm:ss”. Zaokrąglamy w górę, żeby przez pierwszą sekundę było widać pełne „25:00”, a „00:00” dopiero na końcu. */
export function formatRemaining(ms: number): string {
  const totalSeconds = Math.ceil(ms / 1000)
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`
}
