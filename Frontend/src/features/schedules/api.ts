import { ConflictError, request } from '../../lib/http'
import type { Schedule, ScheduleDraft } from './types'

const BASE_URL = '/api/schedules'

/** Harmonogram zmieniono w innym miejscu (np. w drugiej karcie). */
export function isScheduleConflict(error: unknown): error is ConflictError<Schedule> {
  return error instanceof ConflictError
}

export function getSchedules(): Promise<Schedule[]> {
  return request(BASE_URL)
}

export function createSchedule(draft: ScheduleDraft): Promise<Schedule> {
  return request(BASE_URL, { method: 'POST', body: JSON.stringify(draft) })
}

export function updateSchedule(id: string, draft: ScheduleDraft, version: number): Promise<Schedule> {
  return request(`${BASE_URL}/${id}`, {
    method: 'PUT',
    body: JSON.stringify({ ...draft, version }),
  })
}

export function deleteSchedule(id: string): Promise<void> {
  return request(`${BASE_URL}/${id}`, { method: 'DELETE' })
}
