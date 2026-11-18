import { useHubConnection, type LiveStatus } from '../../lib/realtime/useHubConnection'

/** Adres huba z backendu (ScheduleHub.Path). Vite przepuszcza go jako WebSocket (ws: true w proxy /api). */
const HUB_URL = '/api/hubs/schedules'

/** Rodzaj zmiany (ScheduleChangeKind z backendu – enum wysyłany jako tekst camelCase). */
export type ScheduleChangeKind =
  | 'taskCreated'
  | 'taskUpdated'
  | 'taskDeleted'
  | 'taskTagsChanged'
  | 'scheduleUpdated'
  | 'scheduleDeleted'
  | 'memberJoined'
  | 'memberRoleChanged'
  | 'memberRemoved'

/** „Coś się zmieniło w harmonogramie” (ScheduleChangedMessage). Aktualny stan pobieramy zwykłym API. */
export interface ScheduleChangedMessage {
  scheduleId: string
  change: ScheduleChangeKind
  taskId: string | null
  /** Tytuł zadania – także po jego usunięciu. */
  taskTitle: string | null
  /** Nazwa harmonogramu przy zmianach harmonogramu i dostępu – także po usunięciu albo utracie dostępu. */
  scheduleName: string | null
  /** Przy zmianach dostępu: osoba, której dotyczą. */
  memberUserId: string | null
  actorUserId: string
  actorEmail: string
  occurredAt: string
}

/** „Ktoś zaprosił Cię do harmonogramu” (ScheduleInvitationReceivedMessage). */
export interface ScheduleInvitationReceivedMessage {
  scheduleId: string
  scheduleName: string
  actorUserId: string
  actorEmail: string
  occurredAt: string
}

interface ScheduleHubHandlers {
  onScheduleChanged: (message: ScheduleChangedMessage) => void
  onInvitationReceived: (message: ScheduleInvitationReceivedMessage) => void
  /** Po przerwie w połączeniu – pobierz wszystko od nowa. */
  onResync: () => void
}

/** Połączenie z hubem harmonogramów na czas, gdy zakładka „Harmonogramy” jest otwarta. */
export function useScheduleHub(handlers: ScheduleHubHandlers): LiveStatus {
  return useHubConnection(HUB_URL, {
    events: {
      // Nazwy zdarzeń = nazwy metod IScheduleHubClient w backendzie.
      ScheduleChanged: (message: ScheduleChangedMessage) => handlers.onScheduleChanged(message),
      ScheduleInvitationReceived: (message: ScheduleInvitationReceivedMessage) => handlers.onInvitationReceived(message),
    },
    onResync: handlers.onResync,
  })
}
