/**
 * Co bieżący użytkownik może zrobić z listą (z backendu, TodoListAccess):
 * `owner` – wszystko, `editor` – punkty, `viewer` – tylko podgląd.
 */
export type TodoListAccess = 'owner' | 'editor' | 'viewer'

/** Uprawnienia nadawane przy udostępnianiu (TodoListMemberRole). Właściciela nie da się nadać. */
export type TodoListMemberRole = 'viewer' | 'editor'

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

/** Osoba z dostępem do listy albo z zaproszeniem, na które jeszcze nie odpowiedziała (`acceptedAt` = null). */
export interface TodoListMember {
  userId: string
  email: string
  role: TodoListMemberRole
  createdAt: string
  acceptedAt: string | null
}

/** Zaproszenie bieżącego użytkownika do cudzej listy. */
export interface TodoListInvitation {
  listId: string
  listTitle: string
  ownerEmail: string
  role: TodoListMemberRole
  invitedAt: string
}

/** Wynik wyszukania konta po adresie email (POST /api/users/lookup). */
export interface UserLookup {
  id: string
  email: string
}
