import type { TokenUser } from '../../lib/authToken'
import type { TodoListChangedMessage, TodoListInvitationReceivedMessage } from './realtime'

/**
 * Opis zmiany do powiadomienia „kto co zmienił”. Czas teraźniejszy („dodaje”, „usuwa”) – nie zależy od płci,
 * a powiadomienie i tak dotyczy chwili obecnej.
 * Zwraca null dla własnych zmian (z tej albo innej karty) – o nich nie powiadamiamy.
 *
 * @param listTitle nazwa listy, jeśli znamy ją z panelu (wiadomości o punktach jej nie niosą)
 * @param isOpenList czy to lista otwarta na ekranie – wtedy nie dopisujemy jej nazwy
 */
export function describeChange(
  message: TodoListChangedMessage,
  me: TokenUser | null,
  listTitle: string | null,
  isOpenList: boolean,
): string | null {
  if (me && message.actorUserId === me.id) return null

  const item = message.itemTitle ? `„${message.itemTitle}”` : ''
  const list = `„${message.listTitle ?? listTitle ?? 'lista'}”`
  const onList = !isOpenList && listTitle ? ` na liście „${listTitle}”` : ''
  const aboutMe = me !== null && message.memberUserId === me.id

  switch (message.change) {
    case 'itemCreated':
      return `dodaje punkt ${item}${onList}`
    case 'itemUpdated':
      return `zmienia punkt ${item}${onList}`
    case 'itemDeleted':
      return `usuwa punkt ${item}${onList}`
    case 'itemsReordered':
      return `zmienia kolejność punktów${onList}`
    case 'listRenamed':
      return `zmienia nazwę listy na ${list}`
    case 'listDeleted':
      return `usuwa listę ${list}`
    case 'memberJoined':
      return `dołącza do listy ${list}`
    case 'memberRoleChanged':
      return aboutMe ? `zmienia Twoje uprawnienia do listy ${list}` : `zmienia uprawnienia do listy ${list}`
    case 'memberRemoved':
      if (message.memberUserId === message.actorUserId) return `opuszcza listę ${list}`
      return aboutMe ? `odbiera Ci dostęp do listy ${list}` : `odbiera dostęp do listy ${list}`
    default:
      return null
  }
}

export function describeInvitation(message: TodoListInvitationReceivedMessage): string {
  return `zaprasza Cię do listy „${message.listTitle}”`
}
