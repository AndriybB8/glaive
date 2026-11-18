import type { ScheduleChangedMessage } from './realtime'

/** Wpis historii zmian jednego harmonogramu. */
export interface HistoryEntry {
  /** Klucz Reacta i do scalania serii (zob. SERIES_MS). */
  id: string
  message: ScheduleChangedMessage
}

/** Ile wpisów trzymamy na harmonogram. */
const MAX_ENTRIES = 50
/** Ta sama osoba, ta sama zmiana tego samego zadania w tym czasie = jeden wpis (np. zapis zadania z kilkoma tagami). */
export const SERIES_MS = 4000

function seriesKey(m: ScheduleChangedMessage): string {
  return `${m.actorUserId}|${m.change}|${m.taskId ?? ''}`
}

/** Czy `next` należy do tej samej serii co `previous` (wiadomości przychodzą po kolei). */
export function sameSeries(previous: ScheduleChangedMessage, next: ScheduleChangedMessage): boolean {
  return (
    seriesKey(previous) === seriesKey(next) &&
    Math.abs(Date.parse(next.occurredAt) - Date.parse(previous.occurredAt)) <= SERIES_MS
  )
}

/**
 * Dopisuje wiadomość na początek historii (najnowsze pierwsze). Seria (np. przypisanie trzech tagów naraz,
 * czyli trzy wiadomości) zostaje jednym wpisem z czasem ostatniej wiadomości.
 */
export function addToHistory(history: readonly HistoryEntry[], message: ScheduleChangedMessage, id: string): HistoryEntry[] {
  const [latest, ...rest] = history
  if (latest && sameSeries(latest.message, message)) return [{ ...latest, message }, ...rest]
  return [{ id, message }, ...history].slice(0, MAX_ENTRIES)
}
