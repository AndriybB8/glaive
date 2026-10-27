import type { Note, NoteDraft, NoteSummary } from './types'
import { ConflictError, request } from '../../lib/http'
import { getToken } from '../../lib/authToken'

const BASE_URL = '/api/notes'

export function isNoteConflict(error: unknown): error is ConflictError<Note> {
  return error instanceof ConflictError
}

export function getNotes(): Promise<NoteSummary[]> {
  return request(BASE_URL)
}

export function getNote(id: string): Promise<Note> {
  return request(`${BASE_URL}/${id}`)
}

/** Tworzy notatkę; z `groupId` od razu trafia do tej grupy. */
export function createNote(draft: Partial<NoteDraft> = {}, groupId: string | null = null): Promise<Note> {
  return request(BASE_URL, { method: 'POST', body: JSON.stringify({ ...draft, groupId }) })
}

export function updateNote(id: string, draft: NoteDraft, version: number): Promise<Note> {
  return request(`${BASE_URL}/${id}`, {
    method: 'PUT',
    body: JSON.stringify({ ...draft, version }),
  })
}

/**
 * Zapis „na pożegnanie” przy zamykaniu karty lub przełączaniu notatki.
 * Przeglądarka dokończy go nawet po zamknięciu strony. Limit keepalive to ok. 64 KB,
 * więc przy bardzo długich notatkach jest to tylko próba.
 */
export function updateNoteOnExit(id: string, draft: NoteDraft, version: number): void {
  const token = getToken()
  void fetch(`${BASE_URL}/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: JSON.stringify({ ...draft, version }),
    keepalive: true,
  }).catch(() => undefined)
}

/**
 * Przenosi notatkę do grupy (`null` = „Bez grupy”). Nie zmienia wersji notatki,
 * więc nie przeszkadza autozapisowi otwartego edytora.
 */
export function moveNoteToGroup(id: string, groupId: string | null): Promise<void> {
  return request(`${BASE_URL}/${id}/group`, { method: 'PUT', body: JSON.stringify({ groupId }) })
}

export function deleteNote(id: string): Promise<void> {
  return request(`${BASE_URL}/${id}`, { method: 'DELETE' })
}
