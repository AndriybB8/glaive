import { request } from '../../lib/http'
import type { Invitation, Member, MemberRole, MembersApi } from '../sharing/types'
import type { ScheduleInvitation } from './types'

const SCHEDULES_URL = '/api/schedules'
const INVITATIONS_URL = '/api/schedule-invitations'

/** Dostęp do harmonogramu: lista osób, zaproszenie, zmiana uprawnień, odebranie dostępu (tylko właściciel). */
export function scheduleMembersApi(scheduleId: string): MembersApi {
  const url = `${SCHEDULES_URL}/${scheduleId}/members`
  return {
    getMembers: () => request<Member[]>(url),
    invite: (email: string, role: MemberRole) => request<Member>(url, { method: 'POST', body: JSON.stringify({ email, role }) }),
    changeRole: (userId: string, role: MemberRole) =>
      request<Member>(`${url}/${userId}`, { method: 'PUT', body: JSON.stringify({ role }) }),
    remove: (userId: string) => request<void>(`${url}/${userId}`, { method: 'DELETE' }),
  }
}

/** Opuszczenie wspólnego harmonogramu = usunięcie własnego wpisu. */
export function leaveSchedule(scheduleId: string, userId: string): Promise<void> {
  return request(`${SCHEDULES_URL}/${scheduleId}/members/${userId}`, { method: 'DELETE' })
}

/** Zaproszenia bieżącego użytkownika, na które jeszcze nie odpowiedział – w kształcie panelu zaproszeń. */
export async function getScheduleInvitations(): Promise<Invitation[]> {
  const invitations = await request<ScheduleInvitation[]>(INVITATIONS_URL)
  return invitations.map((i) => ({ id: i.scheduleId, title: i.scheduleName, ownerEmail: i.ownerEmail, role: i.role }))
}

export function acceptScheduleInvitation(scheduleId: string): Promise<void> {
  return request(`${INVITATIONS_URL}/${scheduleId}/accept`, { method: 'POST' })
}

export function declineScheduleInvitation(scheduleId: string): Promise<void> {
  return request(`${INVITATIONS_URL}/${scheduleId}/decline`, { method: 'POST' })
}

/** Teksty okna udostępniania harmonogramu. */
export function scheduleShareTexts(name: string) {
  return {
    title: `Udostępnij „${name}”`,
    description: 'Harmonogram wspólny: zaproś osoby po adresie email. Zobaczą go, gdy przyjmą zaproszenie.',
    roleDescriptions: {
      viewer: 'Widzi harmonogram i zadania, ale nie może niczego zmieniać.',
      editor: 'Może dodawać, edytować, zmieniać status i usuwać zadania. Nazwę i daty zmienia tylko właściciel.',
    },
    empty: 'Harmonogram nie jest jeszcze nikomu udostępniony.',
    afterInvite: 'Harmonogram pojawi się u tej osoby po przyjęciu zaproszenia.',
    alreadyMember: 'Ta osoba ma już dostęp do harmonogramu.',
    ownAddress: 'To Twój adres – jako właściciel masz już pełny dostęp do harmonogramu.',
  }
}
