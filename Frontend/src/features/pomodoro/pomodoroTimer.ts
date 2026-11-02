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

/** Zabezpieczenie przed nieskończoną pętlą przy nadrabianiu (np. po tygodniu uśpienia komputera). */
const MAX_CATCH_UP_TRANSITIONS = 1000

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

/** Numer bieżącej sesji pracy do wyświetlenia („Sesja 2 z 4”). W przerwie – numer sesji, która się właśnie skończyła. */
export function currentSessionNumber(state: PomodoroState, settings: PomodoroSettings): number {
  const session = state.phase === 'work' ? state.completedWorkSessions + 1 : state.completedWorkSessions
  return Math.min(Math.max(session, 1), settings.sessionsBeforeLongBreak)
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

/** Faza, która następuje po bieżącej, i licznik sesji po przejściu. */
function nextPhase(
  state: Pick<PomodoroState, 'phase' | 'completedWorkSessions'>,
  settings: PomodoroSettings,
): Pick<PomodoroState, 'phase' | 'completedWorkSessions'> {
  if (state.phase === 'work') {
    const completed = state.completedWorkSessions + 1
    return completed >= settings.sessionsBeforeLongBreak
      ? { phase: 'longBreak', completedWorkSessions: completed }
      : { phase: 'shortBreak', completedWorkSessions: completed }
  }
  // Po długiej przerwie zaczyna się nowy cykl.
  return { phase: 'work', completedWorkSessions: state.phase === 'longBreak' ? 0 : state.completedWorkSessions }
}

/**
 * Kończy bieżącą fazę od razu i przechodzi do następnej (przycisk „Pomiń”).
 * Pominięta sesja pracy liczy się jak ukończona. Jeśli timer działał, następna faza startuje od razu.
 */
export function skip(state: PomodoroState, settings: PomodoroSettings, now: number): PomodoroState {
  const next = nextPhase(state, settings)
  const duration = phaseDurationMs(next.phase, settings)
  return state.endsAt === null
    ? { ...next, endsAt: null, remainingMs: duration }
    : { ...next, endsAt: now + duration, remainingMs: 0 }
}

export interface AdvanceResult {
  state: PomodoroState
  /** Fazy, które skończyły się od poprzedniego wywołania (w kolejności). Pusta lista = nic się nie zmieniło. */
  endedPhases: PomodoroPhase[]
}

/**
 * Automatyczne przełączanie praca ↔ przerwa. Wywoływane co tyknięcie i po powrocie do karty.
 * Każda następna faza startuje dokładnie w chwili końca poprzedniej (a nie „teraz”), więc po dłuższej
 * nieobecności timer nadrabia wszystkie przejścia i pokazuje to, co pokazywałby, gdyby cały czas tykał.
 */
export function advance(state: PomodoroState, settings: PomodoroSettings, now: number): AdvanceResult {
  const endedPhases: PomodoroPhase[] = []
  let current = state

  while (current.endsAt !== null && now >= current.endsAt && endedPhases.length < MAX_CATCH_UP_TRANSITIONS) {
    endedPhases.push(current.phase)
    const next = nextPhase(current, settings)
    current = { ...next, endsAt: current.endsAt + phaseDurationMs(next.phase, settings), remainingMs: 0 }
  }

  return { state: current, endedPhases }
}

/** „mm:ss”. Zaokrąglamy w górę, żeby przez pierwszą sekundę było widać pełne „25:00”, a „00:00” dopiero na końcu. */
export function formatRemaining(ms: number): string {
  const totalSeconds = Math.ceil(ms / 1000)
  const minutes = Math.floor(totalSeconds / 60)
  const seconds = totalSeconds % 60
  return `${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`
}

/**
 * Odczytany z przeglądarki stan – sprawdzony, bo mógł zostać zmieniony ręcznie albo przez starszą wersję aplikacji.
 * Zwraca null, gdy coś się nie zgadza.
 */
export function parseStoredState(value: unknown, settings: PomodoroSettings): PomodoroState | null {
  if (typeof value !== 'object' || value === null) return null
  const v = value as Record<string, unknown>
  const phases: PomodoroPhase[] = ['work', 'shortBreak', 'longBreak']

  if (!phases.includes(v.phase as PomodoroPhase)) return null
  if (!Number.isInteger(v.completedWorkSessions) || (v.completedWorkSessions as number) < 0) return null
  if (v.endsAt !== null && !Number.isFinite(v.endsAt)) return null
  if (!Number.isFinite(v.remainingMs) || (v.remainingMs as number) < 0) return null

  const phase = v.phase as PomodoroPhase
  return {
    phase,
    completedWorkSessions: Math.min(v.completedWorkSessions as number, settings.sessionsBeforeLongBreak),
    endsAt: v.endsAt as number | null,
    remainingMs: Math.min(v.remainingMs as number, phaseDurationMs(phase, settings)),
  }
}
