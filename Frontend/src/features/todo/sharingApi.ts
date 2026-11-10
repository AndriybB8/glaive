import { request } from '../../lib/http'
import type { TodoListAccess, TodoListInvitation, TodoListMember, TodoListMemberRole, UserLookup } from './types'

const LISTS_URL = '/api/todo-lists'
const INVITATIONS_URL = '/api/todo-list-invitations'

/** Uprawnienia do wyboru w oknie udostępniania – kolejność jak na liście wyboru. */
export const MEMBER_ROLE_OPTIONS: readonly { value: TodoListMemberRole; label: string }[] = [
  { value: 'viewer', label: 'Podgląd' },
  { value: 'editor', label: 'Edycja' },
]

export const ACCESS_LABELS: Record<TodoListAccess, string> = {
  owner: 'Właściciel',
  editor: 'Edycja',
  viewer: 'Podgląd',
}

/** Krótki opis uprawnień – pod listą wyboru i w zaproszeniu. */
export const ROLE_DESCRIPTIONS: Record<TodoListMemberRole, string> = {
  viewer: 'Widzi listę i jej punkty, ale nie może niczego zmieniać.',
  editor: 'Może dodawać, odhaczać, edytować, przestawiać i usuwać punkty.',
}

/**
 * Wyszukanie aktywnego konta po dokładnym adresie email. 404, gdy nie ma takiego konta;
 * 429 po zbyt wielu próbach (limit na serwerze, bo odpowiedź zdradza, czy adres ma konto).
 */
export function lookupUser(email: string): Promise<UserLookup> {
  return request('/api/users/lookup', { method: 'POST', body: JSON.stringify({ email }) })
}

/** Osoby z dostępem i oczekujące zaproszenia, od najstarszych – tylko dla właściciela listy (inaczej 403). */
export function getTodoListMembers(listId: string): Promise<TodoListMember[]> {
  return request(`${LISTS_URL}/${listId}/members`)
}

/** Zaproszenie po adresie email. Dostęp pojawi się dopiero po przyjęciu. 409, gdy osoba ma już dostęp lub zaproszenie. */
export function inviteTodoListMember(listId: string, email: string, role: TodoListMemberRole): Promise<TodoListMember> {
  return request(`${LISTS_URL}/${listId}/members`, { method: 'POST', body: JSON.stringify({ email, role }) })
}

export function changeTodoListMemberRole(listId: string, userId: string, role: TodoListMemberRole): Promise<TodoListMember> {
  return request(`${LISTS_URL}/${listId}/members/${userId}`, { method: 'PUT', body: JSON.stringify({ role }) })
}

/** Właściciel odbiera dostęp albo cofa zaproszenie. Z własnym id – opuszczenie udostępnionej listy. */
export function removeTodoListMember(listId: string, userId: string): Promise<void> {
  return request(`${LISTS_URL}/${listId}/members/${userId}`, { method: 'DELETE' })
}

/** Zaproszenia bieżącego użytkownika, na które jeszcze nie odpowiedział (od najnowszych). */
export function getTodoListInvitations(): Promise<TodoListInvitation[]> {
  return request(INVITATIONS_URL)
}

export function acceptTodoListInvitation(listId: string): Promise<void> {
  return request(`${INVITATIONS_URL}/${listId}/accept`, { method: 'POST' })
}

export function declineTodoListInvitation(listId: string): Promise<void> {
  return request(`${INVITATIONS_URL}/${listId}/decline`, { method: 'POST' })
}
