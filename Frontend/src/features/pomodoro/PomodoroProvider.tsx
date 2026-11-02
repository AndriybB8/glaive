import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { POMODORO_STORAGE_KEY, PomodoroContext, type PomodoroAlert, type PomodoroContextValue } from './pomodoroContext'
import {
  POMODORO_SOUND_STORAGE_KEY,
  parseSoundSettings,
  playPomodoroSound,
  unlockAudio,
  type PomodoroSoundSettings,
} from './pomodoroSound'
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
  type AdvanceResult,
  type PomodoroSettings,
  type PomodoroState,
} from './pomodoroTimer'

/** Jak często odświeżamy licznik. Sam czas liczy się ze znaczników czasu, więc to wpływa tylko na płynność. */
const TICK_MS = 250

/**
 * Dźwięk gra tylko dla fazy, która skończyła się niedawno. Karta w tle tyka rzadziej (nawet raz na minutę),
 * więc zostawiamy zapas; ale po powrocie do uśpionego komputera nie gramy o fazie sprzed godziny.
 */
const ALERT_FRESH_MS = 90_000

/**
 * Wspólny dla kart znacznik „o końcu tej fazy już zagrano”. Każda otwarta karta tyka sama, ale dźwięk
 * ma zagrać raz. Wartością jest moment końca fazy – taki sam we wszystkich kartach.
 */
const LAST_ALERT_STORAGE_KEY = 'SP.pomodoroLastAlert'

/** Komunikat o końcu fazy znika sam po tym czasie, liczonym od chwili, gdy karta jest widoczna. */
const ALERT_VISIBLE_MS = 20_000

/** Krótki tekst do tytułu karty – widać go nawet wtedy, gdy aplikacja jest w tle i dźwięk jest wyciszony. */
function alertTitle(alert: PomodoroAlert): string {
  return alert.endedPhase === 'work' ? 'Czas na przerwę' : 'Czas wracać do pracy'
}

function readStoredState(settings: PomodoroSettings): PomodoroState | null {
  try {
    const raw = localStorage.getItem(POMODORO_STORAGE_KEY)
    return raw ? parseStoredState(JSON.parse(raw), settings) : null
  } catch {
    return null
  }
}

function readSoundSettings(): PomodoroSoundSettings {
  try {
    const raw = localStorage.getItem(POMODORO_SOUND_STORAGE_KEY)
    return parseSoundSettings(raw ? JSON.parse(raw) : null)
  } catch {
    return parseSoundSettings(null)
  }
}

/** Zwraca true, jeśli ta karta jako pierwsza ogłasza koniec fazy, która skończyła się w `endedAt`. */
function claimAlert(endedAt: number): boolean {
  try {
    if (localStorage.getItem(LAST_ALERT_STORAGE_KEY) === String(endedAt)) return false
    localStorage.setItem(LAST_ALERT_STORAGE_KEY, String(endedAt))
  } catch {
    // Bez localStorage nie zsynchronizujemy kart – lepiej zagrać niż nie zagrać.
  }
  return true
}

interface PomodoroProviderProps {
  /** Czasy cyklu. Gdy powstanie zapis ustawień użytkownika (SCRUM-43), wystarczy je tu przekazać. */
  settings?: PomodoroSettings
  children: ReactNode
}

/**
 * Timer Pomodoro dla całej aplikacji. Siedzi nad zakładkami (w App), więc działa dalej po przejściu
 * do notatek czy harmonogramów. Stan zapisuje w localStorage: przetrwa odświeżenie strony,
 * a druga karta z aplikacją pokazuje ten sam timer. Na koniec fazy pokazuje komunikat i (bez wyciszenia) gra dźwięk.
 */
export function PomodoroProvider({ settings = DEFAULT_POMODORO_SETTINGS, children }: PomodoroProviderProps) {
  // Po odświeżeniu strony od razu nadrabiamy fazy, które skończyły się w międzyczasie (bez dźwięku).
  const [state, setState] = useState<PomodoroState>(() => {
    const stored = readStoredState(settings) ?? createInitialState(settings)
    return advance(stored, settings, Date.now()).state
  })
  const [now, setNow] = useState(() => Date.now())
  const [soundSettings, setSoundSettings] = useState<PomodoroSoundSettings>(readSoundSettings)
  const [alert, setAlert] = useState<PomodoroAlert | null>(null)
  const running = state.endsAt !== null

  // Aktualny stan także poza renderowaniem (tyknięcia, zdarzenia). Każda zmiana stanu przechodzi przez commit(),
  // więc ref i stan Reacta są zawsze zgodne, a efekty uboczne (dźwięk) wykonujemy raz, poza aktualizacją stanu.
  const stateRef = useRef(state)
  const soundRef = useRef(soundSettings)
  useEffect(() => {
    soundRef.current = soundSettings
  }, [soundSettings])

  const commit = useCallback((next: PomodoroState) => {
    stateRef.current = next
    setState(next)
  }, [])

  /**
   * Koniec fazy: komunikat na ekranie pokazuje każda karta, a dźwięk gra jedna (i tylko bez wyciszenia).
   * Gdy przeglądarka zablokuje dźwięk, komunikat to mówi – sygnał wizualny jest zawsze.
   */
  const announcePhaseEnd = useCallback((result: AdvanceResult, time: number) => {
    const endedAt = result.lastEndedAt
    if (endedAt === null || time - endedAt > ALERT_FRESH_MS) return

    setAlert({
      endedPhase: result.endedPhases[result.endedPhases.length - 1],
      nextPhase: result.state.phase,
      endedAt,
      soundBlocked: false,
    })

    const sound = soundRef.current
    if (sound.muted || !claimAlert(endedAt)) return
    void playPomodoroSound(sound.soundId, sound.volume).then((played) => {
      if (!played) {
        setAlert((current) => (current?.endedAt === endedAt ? { ...current, soundBlocked: true } : current))
      }
    })
  }, [])

  /** Aktualizuje zegar i przełącza fazy, które już się skończyły. */
  const sync = useCallback(() => {
    const time = Date.now()
    setNow(time)
    const result = advance(stateRef.current, settings, time)
    if (result.endedPhases.length > 0) {
      commit(result.state)
      announcePhaseEnd(result, time)
    }
  }, [settings, commit, announcePhaseEnd])

  /** Akcja użytkownika: najpierw nadrabiamy zaległe przejścia, potem wykonujemy akcję na aktualnym stanie. */
  const act = useCallback(
    (action: (current: PomodoroState, time: number) => PomodoroState) => {
      unlockAudio()
      setAlert(null)
      const time = Date.now()
      setNow(time)
      commit(action(advance(stateRef.current, settings, time).state, time))
    },
    [settings, commit],
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

  // Każde kliknięcie lub klawisz w aplikacji odblokowuje dźwięk (np. po odświeżeniu strony z działającym timerem).
  useEffect(() => {
    document.addEventListener('pointerdown', unlockAudio)
    document.addEventListener('keydown', unlockAudio)
    return () => {
      document.removeEventListener('pointerdown', unlockAudio)
      document.removeEventListener('keydown', unlockAudio)
    }
  }, [])

  useEffect(() => {
    try {
      localStorage.setItem(POMODORO_STORAGE_KEY, JSON.stringify(state))
    } catch {
      // Zablokowany localStorage – timer działa dalej, tylko nie przetrwa odświeżenia.
    }
  }, [state])

  useEffect(() => {
    try {
      localStorage.setItem(POMODORO_SOUND_STORAGE_KEY, JSON.stringify(soundSettings))
    } catch {
      // Jak wyżej – ustawienie działa do odświeżenia strony.
    }
  }, [soundSettings])

  // Zmiana w innej karcie (start, pauza, wylogowanie, ustawienia dźwięku) – przyjmujemy jej stan.
  useEffect(() => {
    const handleStorage = (event: StorageEvent) => {
      if (event.key === POMODORO_SOUND_STORAGE_KEY) {
        try {
          setSoundSettings(parseSoundSettings(event.newValue ? JSON.parse(event.newValue) : null))
        } catch {
          // Uszkodzona wartość – zostajemy przy swoich ustawieniach.
        }
        return
      }
      if (event.key !== POMODORO_STORAGE_KEY) return
      let next: PomodoroState | null
      try {
        next = event.newValue ? parseStoredState(JSON.parse(event.newValue), settings) : createInitialState(settings)
      } catch {
        return
      }
      if (next) {
        setNow(Date.now())
        commit(next)
      }
    }
    window.addEventListener('storage', handleStorage)
    return () => window.removeEventListener('storage', handleStorage)
  }, [settings, commit])

  const remainingMs = getRemainingMs(state, now)
  const remainingText = formatRemaining(remainingMs)

  // Czas w tytule karty – widać go także wtedy, gdy aplikacja jest w tle. Po końcu fazy z dzwonkiem 🔔.
  useEffect(() => {
    if (!running && !alert) return
    const originalTitle = document.title
    document.title = alert ? `🔔 ${alertTitle(alert)} · ${remainingText}` : `${remainingText} · ${PHASE_LABELS[state.phase]}`
    return () => {
      document.title = originalTitle
    }
  }, [running, remainingText, state.phase, alert])

  // Komunikat znika sam dopiero wtedy, gdy użytkownik mógł go zobaczyć (karta widoczna przez ALERT_VISIBLE_MS).
  useEffect(() => {
    if (!alert) return
    let timer: number | undefined
    const schedule = () => {
      window.clearTimeout(timer)
      if (document.visibilityState === 'visible') {
        timer = window.setTimeout(() => setAlert(null), ALERT_VISIBLE_MS)
      }
    }
    schedule()
    document.addEventListener('visibilitychange', schedule)
    return () => {
      window.clearTimeout(timer)
      document.removeEventListener('visibilitychange', schedule)
    }
  }, [alert])

  const dismissAlert = useCallback(() => setAlert(null), [])

  const updateSoundSettings = useCallback((change: Partial<PomodoroSoundSettings>) => {
    setSoundSettings((current) => parseSoundSettings({ ...current, ...change }))
  }, [])

  const previewSound = useCallback(() => {
    unlockAudio()
    void playPomodoroSound(soundRef.current.soundId, soundRef.current.volume)
  }, [])

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
      soundSettings,
      updateSoundSettings,
      previewSound,
      alert,
      dismissAlert,
    }),
    [state, settings, remainingMs, running, act, soundSettings, updateSoundSettings, previewSound, alert, dismissAlert],
  )

  return <PomodoroContext.Provider value={value}>{children}</PomodoroContext.Provider>
}
