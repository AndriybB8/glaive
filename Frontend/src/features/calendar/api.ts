import { request } from '../../lib/http'
import type { CalendarEvent, CalendarItem, EditScope, EventFields } from './types'

/** Strefa przeglądarki (IANA) – backend liczy w niej, które godziny należą do widocznych dni. */
export const TIME_ZONE = Intl.DateTimeFormat().resolvedOptions().timeZone

/** Elementy kalendarza w zakresie dni (włącznie). Backend przyjmuje maksymalnie 62 dni. */
export function getCalendarItems(from: string, to: string): Promise<CalendarItem[]> {
  const query = new URLSearchParams({ from, to, timeZone: TIME_ZONE })
  return request(`/api/calendar?${query.toString()}`)
}

/** Wydarzenie albo cała seria – m.in. po to, żeby przy edycji znać jej powtarzanie. */
export function getCalendarEvent(id: string): Promise<CalendarEvent> {
  return request(`/api/calendar-events/${id}`)
}

export function createCalendarEvent(fields: EventFields): Promise<CalendarEvent> {
  return request('/api/calendar-events', { method: 'POST', body: JSON.stringify(fields) })
}

interface UpdateOptions {
  version: number
  /** Tylko dla serii. */
  scope?: EditScope
  /** Dla serii: dzień wystąpienia, z którego otwarto edycję. */
  occurrenceDate?: string | null
}

export function updateCalendarEvent(id: string, fields: EventFields, { version, scope, occurrenceDate }: UpdateOptions): Promise<CalendarEvent> {
  return request(`/api/calendar-events/${id}`, {
    method: 'PUT',
    body: JSON.stringify({ ...fields, version, scope: scope ?? null, occurrenceDate: occurrenceDate ?? null }),
  })
}

export function deleteCalendarEvent(id: string, scope?: EditScope, occurrenceDate?: string | null): Promise<void> {
  const query = new URLSearchParams()
  if (scope) query.set('scope', scope)
  if (scope && occurrenceDate) query.set('occurrenceDate', occurrenceDate)
  const suffix = query.size > 0 ? `?${query.toString()}` : ''
  return request(`/api/calendar-events/${id}${suffix}`, { method: 'DELETE' })
}
