import type { SharedAccess } from '../sharing/types'
import type { TaskPriority, TaskStatus } from '../tasks/types'

/** Jak często powtarza się wydarzenie (RecurrenceFrequency z backendu). */
export type RecurrenceFrequency = 'none' | 'daily' | 'weekly' | 'monthly'

/** Które wystąpienia serii zmienić albo usunąć – jak w Google Calendar (CalendarEditScope). */
export type EditScope = 'single' | 'following' | 'all'

/** Rodzaj elementu w kalendarzu (CalendarItemKind). */
export type CalendarItemKind = 'event' | 'schedule' | 'task'

/** Element widoku kalendarza (CalendarItemDto) – wydarzenie, harmonogram albo zadanie z terminem. */
export interface CalendarItem {
  /** Unikalny w odpowiedzi, np. "event:{id}:2026-11-23" dla wystąpienia serii. */
  key: string
  kind: CalendarItemKind
  /** Id wydarzenia (dla wystąpienia – id serii), harmonogramu albo zadania. */
  id: string
  title: string
  description: string | null
  isAllDay: boolean
  /** Wydarzenie w godzinach: początek i koniec w UTC (ISO). */
  startsAt: string | null
  endsAt: string | null
  /** Element całodniowy: pierwszy i ostatni dzień (yyyy-MM-dd, włącznie). */
  startDate: string | null
  endDate: string | null
  /** Kolor wydarzenia; harmonogramy i zadania go nie mają – ustala go frontend. */
  color: string | null
  access: SharedAccess
  /** Wystąpienie serii: jego dzień według serii (do zmiany albo usunięcia tylko tego wystąpienia). */
  occurrenceDate: string | null
  isRecurring: boolean
  isException: boolean
  /** Wersja do zmiany samego elementu (wystąpienie bez osobnych zmian = 0). */
  version: number | null
  /** Wersja serii do zmian „to i następne” i „wszystkie”. */
  seriesVersion: number | null
  scheduleId: string | null
  scheduleName: string | null
  taskStatus: TaskStatus | null
  taskPriority: TaskPriority | null
}

/** Wydarzenie albo seria (CalendarEventDto). */
export interface CalendarEvent {
  id: string
  title: string
  description: string | null
  color: string
  isAllDay: boolean
  startsAt: string | null
  endsAt: string | null
  startDate: string | null
  endDate: string | null
  timeZone: string
  recurrence: RecurrenceFrequency
  recurrenceUntil: string | null
  seriesId: string | null
  originalDate: string | null
  createdAt: string
  updatedAt: string
  version: number
}

/** Pola wydarzenia wysyłane przy tworzeniu i zmianie. */
export interface EventFields {
  title: string
  description: string | null
  color: string
  isAllDay: boolean
  startsAt: string | null
  endsAt: string | null
  startDate: string | null
  endDate: string | null
  timeZone: string
  recurrence: RecurrenceFrequency
  recurrenceUntil: string | null
}

export type CalendarView = 'month' | 'week' | 'day'
