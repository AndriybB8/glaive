import { ConflictError, request } from '../../lib/http'
import type { Tag, TagDraft } from './types'

/** Tagi należą do harmonogramu: każdy harmonogram ma własny zestaw (SCRUM-160). */
const tagsUrl = (scheduleId: string) => `/api/schedules/${scheduleId}/tags`

/** Tag zmieniono w innym miejscu (np. w drugiej karcie albo przez inną osobę z dostępem do harmonogramu). */
export function isTagConflict(error: unknown): error is ConflictError<Tag> {
  return error instanceof ConflictError
}

/** Tagi harmonogramu posortowane alfabetycznie (sortuje backend). Widzi je każdy z dostępem do harmonogramu. */
export function getTags(scheduleId: string): Promise<Tag[]> {
  return request(tagsUrl(scheduleId))
}

/** Tworzenie, zmiana i usuwanie – tylko przy uprawnieniu do edycji harmonogramu (inaczej 403). */
export function createTag(scheduleId: string, draft: TagDraft): Promise<Tag> {
  return request(tagsUrl(scheduleId), { method: 'POST', body: JSON.stringify(draft) })
}

export function updateTag(scheduleId: string, id: string, draft: TagDraft, version: number): Promise<Tag> {
  return request(`${tagsUrl(scheduleId)}/${id}`, { method: 'PUT', body: JSON.stringify({ ...draft, version }) })
}

export function deleteTag(scheduleId: string, id: string): Promise<void> {
  return request(`${tagsUrl(scheduleId)}/${id}`, { method: 'DELETE' })
}
