import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { POMODORO_STORAGE_KEY, PomodoroContext, type PomodoroContextValue } from './pomodoroContext'
import {
  DEFAULT_POMODORO_SETTINGS,
  PHASE_LABELS,
  advance,
  createInitialState,
  formatRemaining,
  getRemainingMs,
  isAtPhaseStart,
  parseStoredState,
  pause,
  phaseDurationMs,
  resetPhase,
  skip,
  start,
  type PomodoroSettings,
  type PomodoroState,
} from './pomodoroTimer'

/** Jak często odświeżamy licznik. Sam czas liczy się ze znaczników czasu, więc to wpływa tylko na płynność. */
const TICK_MS = 250

function readStoredState(settings: PomodoroSettings): PomodoroState | null {
  try {
    const raw = localStorage.getItem(POMODORO_STORAGE_KEY)
    return raw ? parseStoredState(JSON.parse(raw), settings) : null
  } catch {
    return null
  }
}

interface PomodoroProviderProps {
  /** Czasy cyklu. Gdy powstanie zapis ustawień użytkownika (SCRUM-43), wystarczy je tu przekazać. */
  settings?: PomodoroSettings
  children: ReactNode
}

/**
 * Timer Pomodoro dla całej aplikacji. Siedzi nad zakładkami (w App), więc działa dalej po przejściu
 * do notatek czy harmonogramów. Stan zapisuje w localStorage: przetrwa odświeżenie strony,
 * a druga karta z aplikacją pokazuje ten sam timer.
 */
export function PomodoroProvider({ settings = DEFAULT_POMODORO_SETTINGS, children }: PomodoroProviderProps) {
  // Po odświeżeniu strony od razu nadrabiamy fazy, które skończyły się w międzyczasie.
  const [state, setState] = useState<PomodoroState>(() => {
    const stored = readStoredState(settings) ?? createInitialState(settings)
    return advance(stored, settings, Date.now()).state
  })
  const [now, setNow] = useState(() => Date.now())
  const running = state.endsAt !== null

  /** Aktualizuje zegar i przełącza fazy, które już się skończyły. */
  const sync = useCallback(() => {
    const time = Date.now()
    setNow(time)
    setState((current) => {
      const result = advance(current, settings, time)
      // Miejsce na sygnał dźwiękowy końca fazy (SCRUM-90): result.endedPhases.
      return result.endedPhases.length > 0 ? result.state : current
    })
  }, [settings])

  /** Akcja użytkownika: najpierw nadrabiamy zaległe przejścia, potem wykonujemy akcję na aktualnym stanie. */
  const act = useCallback(
    (action: (current: PomodoroState, time: number) => PomodoroState) => {
      const time = Date.now()
      setNow(time)
      setState((current) => action(advance(current, settings, time).state, time))
    },
    [settings],
  )

  // Tyka tylko, gdy timer działa. W karcie w tle przeglądarka spowalnia interwał – nie szkodzi, liczy się endsAt.
  useEffect(() => {
    if (!running) return
    const id = window.setInterval(sync, TICK_MS)
    return () => window.clearInterval(id)
  }, [running, sync])

  // Powrót do karty: od razu pokazujemy aktualny czas, bez czekania na spowolnione tyknięcie.
  useEffect(() => {
    document.addEventListener('visibilitychange', sync)
    return () => document.removeEventListener('visibilitychange', sync)
  }, [sync])

  useEffect(() => {
    try {
      localStorage.setItem(POMODORO_STORAGE_KEY, JSON.stringify(state))
    } catch {
      // Zablokowany localStorage – timer działa dalej, tylko nie przetrwa odświeżenia.
    }
  }, [state])

  // Zmiana w innej karcie (start, pauza, wylogowanie) – przyjmujemy jej stan.
  useEffect(() => {
    const handleStorage = (event: StorageEvent) => {
      if (event.key !== POMODORO_STORAGE_KEY) return
      let next: PomodoroState | null
      try {
        next = event.newValue ? parseStoredState(JSON.parse(event.newValue), settings) : createInitialState(settings)
      } catch {
        return
      }
      if (next) {
        setNow(Date.now())
        setState(next)
      }
    }
    window.addEventListener('storage', handleStorage)
    return () => window.removeEventListener('storage', handleStorage)
  }, [settings])

  const remainingMs = getRemainingMs(state, now)
  const remainingText = formatRemaining(remainingMs)

  // Czas w tytule karty – widać go także wtedy, gdy aplikacja jest w tle.
  useEffect(() => {
    if (!running) return
    const originalTitle = document.title
    document.title = `${remainingText} · ${PHASE_LABELS[state.phase]}`
    return () => {
      document.title = originalTitle
    }
  }, [running, remainingText, state.phase])

  const value = useMemo<PomodoroContextValue>(
    () => ({
      state,
      settings,
      remainingMs,
      phaseDurationMs: phaseDurationMs(state.phase, settings),
      running,
      atPhaseStart: isAtPhaseStart(state, settings),
      nextPhase: skip(state, settings, 0).phase,
      start: () => act((current, time) => start(current, time)),
      pause: () => act((current, time) => pause(current, time)),
      reset: () => act((current) => resetPhase(current, settings)),
      skip: () => act((current, time) => skip(current, settings, time)),
      restartCycle: () => act(() => createInitialState(settings)),
    }),
    [state, settings, remainingMs, running, act],
  )

  return <PomodoroContext.Provider value={value}>{children}</PomodoroContext.Provider>
}
