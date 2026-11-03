import { ApiError, request } from '../../lib/http'
import type { Tag } from '../tags/types'

function sortByName(tags: Tag[]): Tag[] {
  return [...tags].sort((a, b) => a.name.localeCompare(b.name, 'pl', { sensitivity: 'base' }))
}

/** Tagi jednego zadania, alfabetycznie. */
export async function getTaskTags(taskId: string): Promise<Tag[]> {
  return sortByName(await request<Tag[]>(`/api/tasks/${taskId}/tags`))
}

/** Przypisuje tag. Ponowne przypisanie tej samej pary jest bezpieczne (backend je pomija). */
export function assignTag(taskId: string, tagId: string): Promise<void> {
  return request(`/api/tasks/${taskId}/tags/${tagId}`, { method: 'POST' })
}

export function unassignTag(taskId: string, tagId: string): Promise<void> {
  return request(`/api/tasks/${taskId}/tags/${tagId}`, { method: 'DELETE' })
}

/**
 * Tagi wielu zadań naraz (do listy w harmonogramie).
 *
 * TODO: backend nie zwraca tagów w liście zadań, więc wysyłamy osobne zapytanie na każde zadanie (problem N+1).
 * Gdy TaskDto dostanie pole z tagami, wystarczy podmienić tę funkcję – reszta frontu korzysta tylko z niej.
 * Zadanie, którego tagów nie udało się pobrać, nie trafia do wyniku (lista pokaże je bez tagów).
 */
export async function getTagsForTasks(taskIds: readonly string[]): Promise<Record<string, Tag[]>> {
  const entries = await Promise.all(
    taskIds.map(async (id) => {
      try {
        return [id, await getTaskTags(id)] as const
      } catch {
        return null
      }
    }),
  )
  return Object.fromEntries(entries.filter((entry) => entry !== null))
}

/**
 * Doprowadza tagi zadania do wybranego zestawu: przypisuje brakujące, odłącza zbędne.
 * 404 pomijamy: tag (albo zadanie) usunięto w międzyczasie, więc nie ma czego przypisywać ani odłączać.
 */
export async function syncTaskTags(taskId: string, current: readonly string[], selected: readonly string[]): Promise<void> {
  const ignoreNotFound = (error: unknown) => {
    if (!(error instanceof ApiError && error.status === 404)) throw error
  }

  await Promise.all([
    ...selected.filter((id) => !current.includes(id)).map((tagId) => assignTag(taskId, tagId).catch(ignoreNotFound)),
    ...current.filter((id) => !selected.includes(id)).map((tagId) => unassignTag(taskId, tagId).catch(ignoreNotFound)),
  ])
}
