import { request } from '../../lib/http'
import type { Invitation, Member, MemberRole, MembersApi } from '../sharing/types'
import type { TodoListInvitation } from './types'

const LISTS_URL = '/api/todo-lists'
const INVITATIONS_URL = '/api/todo-list-invitations'

/**
 * Dostęp do listy dla wspólnego okna udostępniania: osoby z dostępem i oczekujące zaproszenia, zaproszenie po adresie email
 * (409, gdy osoba ma już dostęp lub zaproszenie), zmiana uprawnień, odebranie dostępu. Tylko właściciel listy (inaczej 403).
 */
export function todoListMembersApi(listId: string): MembersApi {
  const url = `${LISTS_URL}/${listId}/members`
  return {
    getMembers: () => request<Member[]>(url),
    invite: (email: string, role: MemberRole) => request<Member>(url, { method: 'POST', body: JSON.stringify({ email, role }) }),
    changeRole: (userId: string, role: MemberRole) =>
      request<Member>(`${url}/${userId}`, { method: 'PUT', body: JSON.stringify({ role }) }),
    remove: (userId: string) => request<void>(`${url}/${userId}`, { method: 'DELETE' }),
  }
}

/** Opuszczenie udostępnionej (cudzej) listy = usunięcie własnego wpisu. */
export function leaveTodoList(listId: string, userId: string): Promise<void> {
  return request(`${LISTS_URL}/${listId}/members/${userId}`, { method: 'DELETE' })
}

/** Zaproszenia bieżącego użytkownika, na które jeszcze nie odpowiedział (od najnowszych) – w kształcie panelu zaproszeń. */
export async function getTodoListInvitations(): Promise<Invitation[]> {
  const invitations = await request<TodoListInvitation[]>(INVITATIONS_URL)
  return invitations.map((i) => ({ id: i.listId, title: i.listTitle, ownerEmail: i.ownerEmail, role: i.role }))
}

export function acceptTodoListInvitation(listId: string): Promise<void> {
  return request(`${INVITATIONS_URL}/${listId}/accept`, { method: 'POST' })
}

export function declineTodoListInvitation(listId: string): Promise<void> {
  return request(`${INVITATIONS_URL}/${listId}/decline`, { method: 'POST' })
}

/** Teksty okna udostępniania listy. */
export function todoListShareTexts(title: string) {
  return {
    title: `Udostępnij „${title}”`,
    description: 'Zaproś osobę po adresie email. Zobaczy listę, gdy przyjmie zaproszenie.',
    roleDescriptions: {
      viewer: 'Widzi listę i jej punkty, ale nie może niczego zmieniać.',
      editor: 'Może dodawać, odhaczać, edytować, przestawiać i usuwać punkty. Nazwę listy zmienia tylko właściciel.',
    },
    empty: 'Lista nie jest jeszcze nikomu udostępniona.',
    afterInvite: 'Lista pojawi się u tej osoby po przyjęciu zaproszenia.',
    alreadyMember: 'Ta osoba ma już dostęp do listy.',
    ownAddress: 'To Twój adres – jako właściciel masz już pełny dostęp do listy.',
  }
}
