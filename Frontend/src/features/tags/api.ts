import { ConflictError, request } from '../../lib/http'
import type { Tag, TagDraft } from './types'

const BASE_URL = '/api/tags'

/** Tag zmieniono w innym miejscu (np. w drugiej karcie). */
export function isTagConflict(error: unknown): error is ConflictError<Tag> {
  return error instanceof ConflictError
}

/** Tagi użytkownika posortowane alfabetycznie (sortuje backend). */
export function getTags(): Promise<Tag[]> {
  return request(BASE_URL)
}

export function createTag(draft: TagDraft): Promise<Tag> {
  return request(BASE_URL, { method: 'POST', body: JSON.stringify(draft) })
}

export function updateTag(id: string, draft: TagDraft, version: number): Promise<Tag> {
  return request(`${BASE_URL}/${id}`, { method: 'PUT', body: JSON.stringify({ ...draft, version }) })
}

export function deleteTag(id: string): Promise<void> {
  return request(`${BASE_URL}/${id}`, { method: 'DELETE' })
}
