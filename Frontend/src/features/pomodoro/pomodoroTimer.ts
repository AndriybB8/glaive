/**
 * Logika timera Pomodoro – czyste funkcje, bez Reacta i bez zegara.
 * Każda funkcja dostaje aktualny czas (`now`, ms od epoki) z zewnątrz, więc łatwo ją przetestować.
 *
 * Czas liczymy od znaczników czasu (`endsAt`), a nie zliczając tyknięcia: przeglądarka spowalnia
 * `setInterval` w kartach w tle, a uśpiony laptop nie tyka wcale. Po powrocie wystarczy porównać `now` z `endsAt`.
 */

export type PomodoroPhase = 'work' | 'shortBreak' | 'longBreak'

/** Czasy cyklu. Zapisane na koncie użytkownika (SCRUM-43); dopóki nic nie zapisze – {@link DEFAULT_POMODORO_SETTINGS}. */
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

/** Dozwolone zakresy – te same sprawdza backend (PomodoroSettings w warstwie Domain). */
export const POMODORO_LIMITS: Record<keyof PomodoroSettings, { min: number; max: number }> = {
  workMinutes: { min: 1, max: 120 },
  shortBreakMinutes: { min: 1, max: 30 },
  longBreakMinutes: { min: 1, max: 60 },
  sessionsBeforeLongBreak: { min: 1, max: 10 },
}

export function sameSettings(a: PomodoroSettings, b: PomodoroSettings): boolean {
  return (
    a.workMinutes === b.workMinutes &&
    a.shortBreakMinutes === b.shortBreakMinutes &&
    a.longBreakMinutes === b.longBreakMinutes &&
    a.sessionsBeforeLongBreak === b.sessionsBeforeLongBreak
  )
}

/** Ustawienia odczytane z przeglądarki – sprawdzone jak stan timera. Zwraca null, gdy coś się nie zgadza. */
export function parseSettings(value: unknown): PomodoroSettings | null {
  if (typeof value !== 'object' || value === null) return null
  const v = value as Record<string, unknown>
  const keys = Object.keys(POMODORO_LIMITS) as (keyof PomodoroSettings)[]
  const valid = keys.every((key) => {
    const n = v[key]
    return Number.isInteger(n) && (n as number) >= POMODORO_LIMITS[key].min && (n as number) <= POMODORO_LIMITS[key].max
  })
  if (!valid) return null
  return {
    workMinutes: v.workMinutes as number,
    shortBreakMinutes: v.shortBreakMinutes as number,
    longBreakMinutes: v.longBreakMinutes as number,
    sessionsBeforeLongBreak: v.sessionsBeforeLongBreak as number,
  }
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

/**
 * Nowe ustawienia w trakcie cyklu. Czas, który już upłynął w bieżącej fazie, się liczy:
 * po 10 minutach pracy zmiana z 25 na 30 minut zostawia 20 minut, a zmiana na 8 minut kończy fazę od razu.
 * Faza i postęp w cyklu zostają; licznik sesji jest przycinany do nowej liczby sesji w cyklu.
 */
export function applySettings(
  state: PomodoroState,
  previous: PomodoroSettings,
  next: PomodoroSettings,
  now: number,
): PomodoroState {
  const elapsed = Math.max(0, phaseDurationMs(state.phase, previous) - getRemainingMs(state, now))
  const remaining = Math.max(0, phaseDurationMs(state.phase, next) - elapsed)
  const completedWorkSessions = Math.min(state.completedWorkSessions, next.sessionsBeforeLongBreak)

  return state.endsAt === null
    ? { ...state, completedWorkSessions, remainingMs: remaining }
    : { ...state, completedWorkSessions, endsAt: now + remaining, remainingMs: 0 }
}

export interface AdvanceResult {
  state: PomodoroState
  /** Fazy, które skończyły się od poprzedniego wywołania (w kolejności). Pusta lista = nic się nie zmieniło. */
  endedPhases: PomodoroPhase[]
  /** Moment końca ostatniej zakończonej fazy (ms od epoki); null, gdy nic się nie skończyło. */
  lastEndedAt: number | null
}

/**
 * Automatyczne przełączanie praca ↔ przerwa. Wywoływane co tyknięcie i po powrocie do karty.
 * Każda następna faza startuje dokładnie w chwili końca poprzedniej (a nie „teraz”), więc po dłuższej
 * nieobecności timer nadrabia wszystkie przejścia i pokazuje to, co pokazywałby, gdyby cały czas tykał.
 */
export function advance(state: PomodoroState, settings: PomodoroSettings, now: number): AdvanceResult {
  const endedPhases: PomodoroPhase[] = []
  let lastEndedAt: number | null = null
  let current = state

  while (current.endsAt !== null && now >= current.endsAt && endedPhases.length < MAX_CATCH_UP_TRANSITIONS) {
    endedPhases.push(current.phase)
    lastEndedAt = current.endsAt
    const next = nextPhase(current, settings)
    current = { ...next, endsAt: current.endsAt + phaseDurationMs(next.phase, settings), remainingMs: 0 }
  }

  return { state: current, endedPhases, lastEndedAt }
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
