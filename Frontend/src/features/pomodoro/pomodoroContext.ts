import { createContext, useContext } from 'react'
import type { PomodoroPhase, PomodoroSettings, PomodoroState } from './pomodoroTimer'

/** Klucz w localStorage (jak `SP.token`). Stan timera przetrwa odświeżenie strony i jest wspólny dla kart. */
export const POMODORO_STORAGE_KEY = 'SP.pomodoro'

export interface PomodoroContextValue {
  state: PomodoroState
  settings: PomodoroSettings
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
  } catch {
    // Zablokowany localStorage (np. tryb prywatny) – nie ma czego czyścić.
  }
}
