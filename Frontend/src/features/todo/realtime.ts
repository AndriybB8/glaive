import { useHubConnection, type LiveStatus } from '../../lib/realtime/useHubConnection'

/** Adres huba z backendu (TodoListHub.Path). Vite przepuszcza go do backendu jako WebSocket (ws: true w proxy). */
const HUB_URL = '/api/hubs/todo-lists'

/** Rodzaj zmiany (TodoListChangeKind z backendu – enum wysyłany jako tekst camelCase). */
export type TodoListChangeKind =
  | 'itemCreated'
  | 'itemUpdated'
  | 'itemDeleted'
  | 'itemsReordered'
  | 'listRenamed'
  | 'listDeleted'
  | 'memberJoined'
  | 'memberRoleChanged'
  | 'memberRemoved'

/** Wiadomość „coś się zmieniło na liście” (TodoListChangedMessage). Aktualny stan pobieramy zwykłym API. */
export interface TodoListChangedMessage {
  listId: string
  change: TodoListChangeKind
  itemId: string | null
  /** Treść punktu – także po jego usunięciu. */
  itemTitle: string | null
  /** Nazwa listy przy zmianach listy i dostępu – także po usunięciu listy albo utracie dostępu. */
  listTitle: string | null
  /** Przy zmianach dostępu: osoba, której dotyczą. */
  memberUserId: string | null
  actorUserId: string
  actorEmail: string
  occurredAt: string
}

/** Wiadomość „ktoś zaprosił Cię do listy” (TodoListInvitationReceivedMessage). */
export interface TodoListInvitationReceivedMessage {
  listId: string
  listTitle: string
  actorUserId: string
  actorEmail: string
  occurredAt: string
}

interface TodoListHubHandlers {
  onListChanged: (message: TodoListChangedMessage) => void
  onInvitationReceived: (message: TodoListInvitationReceivedMessage) => void
  /**
   * Po ponownym połączeniu: wiadomości z czasu przerwy przepadły, więc trzeba pobrać aktualny stan.
   * Backend nic nie przywraca – odbiorców wyznacza przy każdej zmianie, więc wystarczy się połączyć.
   */
  onResync: () => void
}

/**
 * Połączenie z hubem list na czas, gdy zakładka „Listy” jest otwarta. Po powrocie strona i tak wczytuje wszystko od nowa.
 * Mechanika połączenia (token, wznawianie) jest wspólna z harmonogramami – useHubConnection.
 */
export function useTodoListHub(handlers: TodoListHubHandlers): LiveStatus {
  return useHubConnection(HUB_URL, {
    events: {
      // Nazwy zdarzeń = nazwy metod ITodoListHubClient w backendzie.
      TodoListChanged: (message: TodoListChangedMessage) => handlers.onListChanged(message),
      TodoListInvitationReceived: (message: TodoListInvitationReceivedMessage) => handlers.onInvitationReceived(message),
    },
    onResync: handlers.onResync,
  })
}
