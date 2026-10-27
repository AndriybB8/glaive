import { request } from '../../lib/http'
import type { NoteSummary } from '../notes/types'
import type { LinkedTask } from './types'

// Przypisanie nie zmienia wersji notatki ani zadania – nie koliduje z autozapisem ani z edycją zadania.

export function getNoteTasks(noteId: string): Promise<LinkedTask[]> {
  return request(`/api/notes/${noteId}/tasks`)
}

/** Przypisuje notatkę do zadania. Ponowne przypisanie tej samej pary jest bezpieczne. */
export function linkNoteToTask(noteId: string, taskId: string): Promise<LinkedTask> {
  return request(`/api/notes/${noteId}/tasks/${taskId}`, { method: 'PUT' })
}

export function unlinkNoteFromTask(noteId: string, taskId: string): Promise<void> {
  return request(`/api/notes/${noteId}/tasks/${taskId}`, { method: 'DELETE' })
}

/** Notatki przypisane do zadania (bez treści). */
export function getTaskNotes(taskId: string): Promise<NoteSummary[]> {
  return request(`/api/tasks/${taskId}/notes`)
}
