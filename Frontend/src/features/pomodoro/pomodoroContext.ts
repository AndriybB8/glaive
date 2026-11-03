import { createContext, useContext } from 'react'
import type { PomodoroSoundSettings } from './pomodoroSound'
import type { PomodoroPhase, PomodoroSettings, PomodoroState } from './pomodoroTimer'

/** Klucz w localStorage (jak `SP.token`). Stan timera przetrwa odświeżenie strony i jest wspólny dla kart. */
export const POMODORO_STORAGE_KEY = 'SP.pomodoro'

/**
 * Ostatnio znane ustawienia użytkownika (kopia z serwera). Dzięki niej po odświeżeniu strony timer od razu
 * liczy właściwy czas, zanim przyjdzie odpowiedź z API, a inne karty dowiadują się o zmianie ustawień.
 */
export const POMODORO_SETTINGS_STORAGE_KEY = 'SP.pomodoroSettings'

/** Stan ustawień z serwera: wczytywanie, wczytane albo błąd (wtedy obowiązuje kopia z przeglądarki lub domyślne). */
export type PomodoroSettingsStatus = 'loading' | 'ready' | 'error'

/** Komunikat o końcu fazy – pokazywany zawsze, także bez dźwięku. */
export interface PomodoroAlert {
  endedPhase: PomodoroPhase
  nextPhase: PomodoroPhase
  /** Moment końca fazy (ms od epoki). */
  endedAt: number
  /** Dźwięk miał zagrać, ale przeglądarka go zablokowała. */
  soundBlocked: boolean
}

export interface PomodoroContextValue {
  state: PomodoroState
  /** Czasy cyklu użytkownika (z serwera, do czasu wczytania – kopia z przeglądarki albo domyślne). */
  settings: PomodoroSettings
  settingsStatus: PomodoroSettingsStatus
  /** Zapisuje ustawienia na koncie i od razu stosuje je w timerze. Błąd zapisu rzuca dalej (ApiError). */
  saveSettings: (settings: PomodoroSettings) => Promise<void>
  remainingMs: number
  /** Pełna długość bieżącej fazy (ms) – do paska postępu. */
  phaseDurationMs: number
  running: boolean
  /** Faza nie była jeszcze uruchomiona (pełny czas). */
  atPhaseStart: boolean
  /** Faza, która przyjdzie po bieżącej. */
  nextPhase: PomodoroPhase
  start: () => void
  pause: () => void
  /** Zatrzymuje i przywraca pełny czas bieżącej fazy. */
  reset: () => void
  /** Kończy bieżącą fazę i przechodzi do następnej. */
  skip: () => void
  /** Wraca do 1. sesji pracy. */
  restartCycle: () => void
  /** Dźwięk na koniec fazy: wybrany dźwięk i głośność (zapamiętane na tym urządzeniu). */
  soundSettings: PomodoroSoundSettings
  updateSoundSettings: (change: Partial<PomodoroSoundSettings>) => void
  /** Odtwarza wybrany dźwięk od razu („Odsłuchaj”). */
  previewSound: () => void
  /** Ostatni koniec fazy do pokazania na ekranie; null = brak. */
  alert: PomodoroAlert | null
  dismissAlert: () => void
}

export const PomodoroContext = createContext<PomodoroContextValue | null>(null)

/** Stan i akcje timera. Działa tylko wewnątrz `PomodoroProvider`. */
export function usePomodoro(): PomodoroContextValue {
  const context = useContext(PomodoroContext)
  if (!context) {
    throw new Error('usePomodoro działa tylko wewnątrz <PomodoroProvider>.')
  }
  return context
}

/** Wywoływane przy wylogowaniu, żeby następna osoba na tym komputerze nie dostała cudzego timera. */
export function clearStoredPomodoro(): void {
  try {
    localStorage.removeItem(POMODORO_STORAGE_KEY)
    localStorage.removeItem(POMODORO_SETTINGS_STORAGE_KEY)
  } catch {
    // Zablokowany localStorage (np. tryb prywatny) – nie ma czego czyścić.
  }
}
