import { request } from '../../lib/http'
import type { PomodoroSettings } from './pomodoroTimer'

const URL = '/api/pomodoro-settings'

/** Odpowiedź API: ustawienia i informacja, czy to jeszcze wartości domyślne (użytkownik niczego nie zapisał). */
export interface PomodoroSettingsResponse extends PomodoroSettings {
  isDefault: boolean
}

export function getPomodoroSettings(): Promise<PomodoroSettingsResponse> {
  return request(URL)
}

/** PUT zastępuje cały zestaw ustawień; przy pierwszym zapisie backend je tworzy. */
export function savePomodoroSettings(settings: PomodoroSettings): Promise<PomodoroSettingsResponse> {
  return request(URL, { method: 'PUT', body: JSON.stringify(settings) })
}

/** Same ustawienia, bez pola isDefault. */
export function toSettings({ workMinutes, shortBreakMinutes, longBreakMinutes, sessionsBeforeLongBreak }: PomodoroSettings): PomodoroSettings {
  return { workMinutes, shortBreakMinutes, longBreakMinutes, sessionsBeforeLongBreak }
}
