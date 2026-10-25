import { ConflictError, request } from '../../lib/http'
import type { NoteGroup } from './types'

const BASE_URL = '/api/note-groups'

/** Grupę zmieniono w innym miejscu (np. w drugiej karcie). */
export function isNoteGroupConflict(error: unknown): error is ConflictError<NoteGroup> {
  return error instanceof ConflictError
}

/** Grupy użytkownika posortowane alfabetycznie (sortuje backend). */
export function getNoteGroups(): Promise<NoteGroup[]> {
  return request(BASE_URL)
}

export function createNoteGroup(name: string): Promise<NoteGroup> {
  return request(BASE_URL, { method: 'POST', body: JSON.stringify({ name }) })
}

export function renameNoteGroup(id: string, name: string, version: number): Promise<NoteGroup> {
  return request(`${BASE_URL}/${id}`, { method: 'PUT', body: JSON.stringify({ name, version }) })
}

/** Usuwa grupę. Jej notatki nie są usuwane – trafiają do „Bez grupy”. */
export function deleteNoteGroup(id: string): Promise<void> {
  return request(`${BASE_URL}/${id}`, { method: 'DELETE' })
}
