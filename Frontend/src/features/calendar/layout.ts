import { addDays, minutesFromDayStart, toIsoDate } from './dates'
import type { CalendarItem } from './types'

/** Kolory elementów bez własnego koloru: harmonogram – morski pasek, zadanie – fioletowe (zrobione – szare). */
export const SCHEDULE_COLOR = '#0D9488'
export const TASK_COLOR = '#7C3AED'
export const DONE_TASK_COLOR = '#94A3B8'

export function itemColor(item: CalendarItem): string {
  if (item.kind === 'schedule') return SCHEDULE_COLOR
  if (item.kind === 'task') return item.taskStatus === 'done' ? DONE_TASK_COLOR : TASK_COLOR
  return item.color ?? '#2563EB'
}

const KIND_ORDER = { schedule: 0, task: 1, event: 2 } as const

/** Kolejność w obrębie dnia: harmonogramy, zadania, wydarzenia; potem wg godziny i tytułu. */
function compareItems(a: CalendarItem, b: CalendarItem): number {
  return (
    KIND_ORDER[a.kind] - KIND_ORDER[b.kind] ||
    (a.startsAt ?? '').localeCompare(b.startsAt ?? '') ||
    a.title.localeCompare(b.title, 'pl')
  )
}

/** Dzień (lokalnie) ostatniej chwili wydarzenia w godzinach – koniec o północy nie zahacza o kolejny dzień. */
function lastDayOfTimed(item: CalendarItem): string {
  return toIsoDate(new Date(new Date(item.endsAt!).getTime() - 1))
}

/** Czy element jest widoczny danego dnia (całodniowy – w zakresie dni; w godzinach – choć częściowo). */
export function occursOn(item: CalendarItem, day: string): boolean {
  if (item.isAllDay) return item.startDate! <= day && day <= item.endDate!
  return toIsoDate(new Date(item.startsAt!)) <= day && day <= lastDayOfTimed(item)
}

/** Wszystkie elementy dnia, posortowane – do widoku miesiąca. */
export function itemsOn(items: readonly CalendarItem[], day: string): CalendarItem[] {
  return items.filter((item) => occursOn(item, day)).sort(compareItems)
}

/** Elementy całodniowe dnia – do paska nad siatką godzin. */
export function allDayItemsOn(items: readonly CalendarItem[], day: string): CalendarItem[] {
  return items.filter((item) => item.isAllDay && occursOn(item, day)).sort(compareItems)
}

/** Wydarzenie w godzinach przycięte do jednego dnia i ustawione w kolumnie, gdy nakłada się na inne. */
export interface TimedSegment {
  item: CalendarItem
  /** Minuty od północy, 0–1440. */
  top: number
  bottom: number
  /** Kolumna w grupie nakładających się wydarzeń i liczba kolumn tej grupy. */
  column: number
  columns: number
  /** Zaczęło się poprzedniego dnia / kończy następnego. */
  continuesBefore: boolean
  continuesAfter: boolean
}

/** Najkrótsza wysokość wydarzenia na siatce, żeby 5-minutowe dało się kliknąć. */
const MIN_DURATION = 20

/**
 * Wydarzenia w godzinach danego dnia rozłożone jak w Google Calendar: nakładające się stoją obok siebie,
 * a grupa (wydarzenia połączone nakładaniem) dzieli szerokość na tyle kolumn, ile potrzebuje.
 */
export function timedSegmentsOn(items: readonly CalendarItem[], day: string): TimedSegment[] {
  const dayLength = minutesFromDayStart(`${addDays(day, 1)}T00:00:00`, day)
  const segments = items
    .filter((item) => !item.isAllDay && occursOn(item, day))
    .map((item) => {
      const start = minutesFromDayStart(item.startsAt!, day)
      const end = minutesFromDayStart(item.endsAt!, day)
      const top = Math.max(0, start)
      return {
        item,
        top,
        bottom: Math.min(dayLength, Math.max(end, top + MIN_DURATION)),
        column: 0,
        columns: 1,
        continuesBefore: start < 0,
        continuesAfter: end > dayLength,
      }
    })
    .sort((a, b) => a.top - b.top || b.bottom - a.bottom)

  let group: TimedSegment[] = []
  let groupEnd = -1
  const closeGroup = () => {
    const columns = Math.max(1, ...group.map((s) => s.column + 1))
    for (const segment of group) segment.columns = columns
    group = []
  }

  for (const segment of segments) {
    if (segment.top >= groupEnd) closeGroup()
    // Pierwsza kolumna, w której poprzednie wydarzenie już się skończyło.
    const taken = new Set(group.filter((s) => s.bottom > segment.top).map((s) => s.column))
    let column = 0
    while (taken.has(column)) column++
    segment.column = column
    group.push(segment)
    groupEnd = Math.max(groupEnd, segment.bottom)
  }
  closeGroup()
  return segments
}
