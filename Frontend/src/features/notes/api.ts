import type { Note, NoteDraft, NoteSummary } from './types'

const BASE_URL = '/api/notes'

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

/** Notatkę zmieniono w innym miejscu (np. w drugiej karcie). */
export class NoteConflictError extends ApiError {
  readonly current: Note

  constructor(message: string, current: Note) {
    super(409, message)
    this.name = 'NoteConflictError'
    this.current = current
  }
}

interface ProblemDetails {
  detail?: string
  title?: string
  current?: Note
}

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...init?.headers },
  })

  if (!response.ok) {
    const problem = (await response.json().catch(() => ({}))) as ProblemDetails
    const message = problem.detail ?? problem.title ?? `Błąd serwera (${response.status}).`

    if (response.status === 409 && problem.current) {
      throw new NoteConflictError(message, problem.current)
    }
    throw new ApiError(response.status, message)
  }

  return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
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
  void fetch(`${BASE_URL}/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
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
