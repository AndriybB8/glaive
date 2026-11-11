import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { useEffect, useRef, useState } from 'react'
import { getToken } from '../../lib/authToken'

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

/** Stan połączenia na żywo – do wskaźnika „Na żywo”. */
export type LiveStatus = 'connecting' | 'connected' | 'reconnecting' | 'offline'

interface TodoListHubHandlers {
  onListChanged: (message: TodoListChangedMessage) => void
  onInvitationReceived: (message: TodoListInvitationReceivedMessage) => void
  /**
   * Po ponownym połączeniu: wiadomości z czasu przerwy przepadły, więc trzeba pobrać aktualny stan.
   * Backend nic nie przywraca – odbiorców wyznacza przy każdej zmianie, więc wystarczy się połączyć.
   */
  onResync: () => void
}

/** Odstępy kolejnych prób, gdy połączenie się zerwie (automatycznie) albo nie uda się go nawiązać (ręcznie). */
const RECONNECT_DELAYS_MS = [0, 2000, 5000, 10000, 30000]
const RETRY_DELAYS_MS = [2000, 5000, 10000, 30000]

/**
 * Połączenie z hubem list na czas, gdy komponent jest zamontowany. Zrywa się przy odmontowaniu
 * (wyjście z zakładki „Listy”), a po powrocie strona i tak wczytuje wszystko od nowa.
 * Token idzie w adresie (access_token) – przeglądarka nie pozwala dodać nagłówka do WebSocketa; backend czyta go tylko na ścieżce huba.
 */
export function useTodoListHub(handlers: TodoListHubHandlers): LiveStatus {
  const [status, setStatus] = useState<LiveStatus>('connecting')
  const handlersRef = useRef(handlers)

  useEffect(() => {
    handlersRef.current = handlers
  })

  useEffect(() => {
    let disposed = false
    let retryTimer: number | undefined
    let failedStarts = 0

    const connection = new HubConnectionBuilder()
      .withUrl(HUB_URL, { accessTokenFactory: () => getToken() ?? '' })
      .withAutomaticReconnect(RECONNECT_DELAYS_MS)
      // Problemy z połączeniem pokazuje wskaźnik „Na żywo”; bez tego biblioteka zgłaszałaby w konsoli m.in. przerwany start przy podwójnym montażu w StrictMode.
      .configureLogging(LogLevel.Critical)
      .build()

    // Nazwy zdarzeń = nazwy metod ITodoListHubClient w backendzie.
    connection.on('TodoListChanged', (message: TodoListChangedMessage) => handlersRef.current.onListChanged(message))
    connection.on('TodoListInvitationReceived', (message: TodoListInvitationReceivedMessage) =>
      handlersRef.current.onInvitationReceived(message),
    )

    connection.onreconnecting(() => {
      if (!disposed) setStatus('reconnecting')
    })
    connection.onreconnected(() => {
      if (disposed) return
      setStatus('connected')
      handlersRef.current.onResync()
    })
    // Automatyczne wznawianie się poddało albo serwer zamknął połączenie (np. wygasł token) – próbujemy dalej sami.
    connection.onclose(() => {
      if (disposed) return
      setStatus('offline')
      scheduleStart()
    })

    function scheduleStart() {
      const delay = RETRY_DELAYS_MS[Math.min(failedStarts, RETRY_DELAYS_MS.length - 1)]
      failedStarts++
      retryTimer = window.setTimeout(() => void start(), delay)
    }

    async function start() {
      if (disposed) return
      try {
        await connection.start()
        if (disposed) return
        const recovered = failedStarts > 0
        failedStarts = 0
        setStatus('connected')
        if (recovered) handlersRef.current.onResync()
      } catch {
        // Przerwane przez odmontowanie (np. podwójny montaż w StrictMode) albo serwer niedostępny.
        if (disposed) return
        setStatus('offline')
        scheduleStart()
      }
    }

    void start()

    return () => {
      disposed = true
      window.clearTimeout(retryTimer)
      void connection.stop()
    }
  }, [])

  return status
}
