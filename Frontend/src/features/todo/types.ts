import type { MemberRole, SharedAccess } from '../sharing/types'

/**
 * Co bieżący użytkownik może zrobić z listą (z backendu, AccessRole) – wspólne z harmonogramami:
 * `owner` – wszystko, `editor` – punkty, `viewer` – tylko podgląd. Szczegóły: `can()` w sharing/permissions.ts.
 */
export type TodoListAccess = SharedAccess

/** Lista na liście list – z licznikami do paska postępu („3 z 7”). */
export interface TodoListSummary {
  id: string
  title: string
  itemCount: number
  doneCount: number
  createdAt: string
  updatedAt: string
  version: number
  access: TodoListAccess
}

/** Lista bez liczników – odpowiedź na utworzenie i zmianę nazwy. */
export interface TodoList {
  id: string
  title: string
  createdAt: string
  updatedAt: string
  version: number
  access: TodoListAccess
}

/** Punkt listy. Kolejność na liście wyznacza `position` (od 0). */
export interface TodoItem {
  id: string
  listId: string
  title: string
  isDone: boolean
  completedAt: string | null
  position: number
  createdAt: string
  updatedAt: string
  version: number
}

/** Zaproszenie bieżącego użytkownika do cudzej listy (GET /api/todo-list-invitations). */
export interface TodoListInvitation {
  listId: string
  listTitle: string
  ownerEmail: string
  role: MemberRole
  invitedAt: string
}
