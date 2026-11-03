import { ConflictError, request } from '../../lib/http'
import type { Task, TaskDraft, TaskStatus } from './types'

/** Zadanie zmieniono w innym miejscu (np. w drugiej karcie). */
export function isTaskConflict(error: unknown): error is ConflictError<Task> {
  return error instanceof ConflictError
}

/**
 * Zadania harmonogramu. Z `tagIds` backend zwraca tylko zadania, które mają WSZYSTKIE podane tagi
 * (parametr `tagId` powtórzony dla każdego tagu: ?tagId=a&tagId=b).
 */
export function getTasks(scheduleId: string, tagIds: readonly string[] = []): Promise<Task[]> {
  const query = new URLSearchParams(tagIds.map((id) => ['tagId', id])).toString()
  return request(`/api/schedules/${scheduleId}/tasks${query ? `?${query}` : ''}`)
}

export function createTask(scheduleId: string, draft: TaskDraft): Promise<Task> {
  return request(`/api/schedules/${scheduleId}/tasks`, { method: 'POST', body: JSON.stringify(draft) })
}

export function updateTask(id: string, draft: TaskDraft, version: number): Promise<Task> {
  return request(`/api/tasks/${id}`, {
    method: 'PUT',
    body: JSON.stringify({ ...draft, version }),
  })
}

export function changeTaskStatus(id: string, status: TaskStatus): Promise<Task> {
  return request(`/api/tasks/${id}/status`, { method: 'PATCH', body: JSON.stringify({ status }) })
}

export function deleteTask(id: string): Promise<void> {
  return request(`/api/tasks/${id}`, { method: 'DELETE' })
}
