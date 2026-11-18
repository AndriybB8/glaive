import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { IconButton } from '../../components/ui/IconButton'
import { PencilIcon, TrashIcon, UserPlusIcon, UsersIcon } from '../../components/ui/icons'
import { LiveStatusBadge } from '../../components/ui/LiveStatusBadge'
import { LiveToasts, type LiveToast } from '../../components/ui/LiveToasts'
import { getCurrentUser } from '../../lib/authToken'
import { InvitationsPanel } from '../sharing/InvitationsPanel'
import { LeaveDialog } from '../sharing/LeaveDialog'
import { can } from '../sharing/permissions'
import { ShareDialog } from '../sharing/ShareDialog'
import { ACCESS_LABELS } from '../sharing/sharing'
import type { Invitation } from '../sharing/types'
import { ScheduleTasksPage } from '../tasks/ScheduleTasksPage'
import { getSchedules } from './api'
import { addToHistory, sameSeries, type HistoryEntry } from './changeHistory'
import { describeScheduleChange } from './describeScheduleChange'
import { useScheduleHub, type ScheduleChangedMessage, type ScheduleInvitationReceivedMessage } from './realtime'
import { DeleteScheduleDialog } from './DeleteScheduleDialog'
import { ScheduleFormDialog } from './ScheduleFormDialog'
import { countDays, formatDateRange, formatDays, getStatus, sortSchedules, todayIso } from './scheduleDates'
import {
  acceptScheduleInvitation,
  declineScheduleInvitation,
  getScheduleInvitations,
  leaveSchedule,
  scheduleMembersApi,
  scheduleShareTexts,
} from './sharingApi'
import type { Schedule, ScheduleStatus } from './types'

const LOAD_ERROR = 'Nie udało się wczytać harmonogramów. Sprawdź, czy backend jest uruchomiony.'

/** Zmiany na żywo: jak długo widać powiadomienie i podświetlone zadanie, ile powiadomień naraz. */
const TOAST_MS = 6000
const HIGHLIGHT_MS = 5000
const MAX_TOASTS = 3
/** Zmiany przychodzą seriami – listę harmonogramów pobieramy raz po chwili ciszy. */
const SCHEDULES_REFRESH_DELAY_MS = 300

const TASK_CHANGES: ReadonlySet<string> = new Set(['taskCreated', 'taskUpdated', 'taskDeleted', 'taskTagsChanged'])

const statusBadges = {
  ongoing: { tone: 'blue', label: 'Trwa' },
  upcoming: { tone: 'neutral', label: 'Nadchodzący' },
  finished: { tone: 'muted', label: 'Zakończony' },
} as const

function StatusBadge({ status }: { status: ScheduleStatus }) {
  const { tone, label } = statusBadges[status]
  return <Badge tone={tone}>{label}</Badge>
}

type OpenDialog =
  | { kind: 'create' }
  | { kind: 'edit'; schedule: Schedule }
  | { kind: 'delete'; schedule: Schedule }
  | { kind: 'share'; schedule: Schedule }
  | { kind: 'leave'; schedule: Schedule }
  | null

/** Oznaczenie harmonogramu wspólnego (cudzego) – z uprawnieniami bieżącego użytkownika. */
function SharedBadge({ schedule }: { schedule: Schedule }) {
  return (
    <span className="inline-flex items-center gap-1 text-slate-500" title={`Harmonogram wspólny · ${ACCESS_LABELS[schedule.access]}`}>
      <UsersIcon />
      <Badge tone="blue">Wspólny · {ACCESS_LABELS[schedule.access]}</Badge>
    </span>
  )
}

export function SchedulesPage() {
  const [schedules, setSchedules] = useState<Schedule[] | null>(null)
  const [listError, setListError] = useState<string | null>(null)
  const [dialog, setDialog] = useState<OpenDialog>(null)
  const [invitations, setInvitations] = useState<Invitation[]>([])
  /** Harmonogram otwarty w widoku zadań (jak otwarta notatka – bez osobnego adresu). */
  const [openedId, setOpenedId] = useState<string | null>(null)
  const opened = schedules?.find((s) => s.id === openedId)
  const today = todayIso()

  const loadSchedules = useCallback(async () => {
    try {
      setListError(null)
      setSchedules(await getSchedules())
    } catch {
      setListError(LOAD_ERROR)
    }
  }, [])

  useEffect(() => {
    let ignore = false
    getSchedules()
      .then((result) => {
        if (!ignore) setSchedules(result)
      })
      .catch(() => {
        if (!ignore) setListError(LOAD_ERROR)
      })
    return () => {
      ignore = true
    }
  }, [])

  /** Zaproszenia to dodatek do listy – błąd wczytania po cichu zostawia poprzedni stan. */
  const loadInvitations = useCallback(async () => {
    try {
      setInvitations(await getScheduleInvitations())
    } catch {
      // Spróbujemy ponownie przy następnym powrocie do karty.
    }
  }, [])

  // Zaproszenia przychodzą od innych osób, więc sprawdzamy je też po powrocie do karty.
  useEffect(() => {
    let ignore = false
    getScheduleInvitations()
      .then((result) => {
        if (!ignore) setInvitations(result)
      })
      .catch(() => undefined)
    const handleVisibility = () => {
      if (document.visibilityState === 'visible') void loadInvitations()
    }
    document.addEventListener('visibilitychange', handleVisibility)
    return () => {
      ignore = true
      document.removeEventListener('visibilitychange', handleVisibility)
    }
  }, [loadInvitations])

  // --- Zmiany na żywo (SignalR, SCRUM-109) ---
  const [me] = useState(getCurrentUser)
  const meId = me?.id ?? null
  const [toasts, setToasts] = useState<LiveToast[]>([])
  /** Sygnał dla otwartego harmonogramu: pobierz zadania od nowa. */
  const [tasksRefreshSignal, setTasksRefreshSignal] = useState(0)
  /** Zadania właśnie zmienione przez inne osoby: id → { email, numer zmiany } (numer, żeby stary timer nie zgasił nowszej zmiany). */
  const [changedTasks, setChangedTasks] = useState<Record<string, { email: string; seq: number }>>({})
  /** Historia zmian wg harmonogramu – na czas otwarcia zakładki (backend jej nie przechowuje). */
  const [histories, setHistories] = useState<Record<string, HistoryEntry[]>>({})
  const liveSeqRef = useRef(0)
  const schedulesRefreshTimerRef = useRef<number | undefined>(undefined)
  /** Ostatnia wiadomość z powiadomieniem – kolejne z tej samej serii (np. kilka tagów naraz) nie dokładają nowych. */
  const lastToastRef = useRef<ScheduleChangedMessage | null>(null)

  /** Lista harmonogramów po zmianie na żywo: po cichu (błąd zostawia poprzedni stan) i raz na serię zmian. */
  const refreshSchedulesQuietly = useCallback(() => {
    window.clearTimeout(schedulesRefreshTimerRef.current)
    schedulesRefreshTimerRef.current = window.setTimeout(() => {
      getSchedules()
        .then(setSchedules)
        .catch(() => undefined)
    }, SCHEDULES_REFRESH_DELAY_MS)
  }, [])

  useEffect(() => () => window.clearTimeout(schedulesRefreshTimerRef.current), [])

  const dismissToast = useCallback((id: number) => setToasts((current) => current.filter((t) => t.id !== id)), [])

  const pushToast = (actor: string, text: string) => {
    const id = ++liveSeqRef.current
    setToasts((current) => [...current, { id, actor: actor || 'Ktoś', text }].slice(-MAX_TOASTS))
    window.setTimeout(() => dismissToast(id), TOAST_MS)
  }

  const highlightTask = (taskId: string, email: string) => {
    const seq = ++liveSeqRef.current
    setChangedTasks((current) => ({ ...current, [taskId]: { email, seq } }))
    window.setTimeout(() => {
      setChangedTasks((current) => {
        if (current[taskId]?.seq !== seq) return current
        const next = { ...current }
        delete next[taskId]
        return next
      })
    }, HIGHLIGHT_MS)
  }

  const handleScheduleChanged = (message: ScheduleChangedMessage) => {
    const isOpen = message.scheduleId === openedId
    const fromSomeoneElse = message.actorUserId !== meId
    const isTaskChange = TASK_CHANGES.has(message.change)

    // Historia: także własne zmiany. Seria tych samych zmian (np. kilka tagów) to jeden wpis.
    const historyId = `h${++liveSeqRef.current}`
    setHistories((current) => ({ ...current, [message.scheduleId]: addToHistory(current[message.scheduleId] ?? [], message, historyId) }))

    // Stan zawsze z API – także przy własnych zmianach, bo mogły przyjść z innej karty.
    if (isTaskChange) {
      if (isOpen) setTasksRefreshSignal((n) => n + 1)
    } else {
      // Nazwa, daty, dostęp albo usunięcie – lista harmonogramów (i uprawnienia otwartego) od nowa.
      // Gdy otwarty harmonogram zniknie z listy, widok sam wraca do listy harmonogramów.
      refreshSchedulesQuietly()
    }

    if (!fromSomeoneElse) return

    if (isOpen && message.taskId && message.change !== 'taskDeleted') highlightTask(message.taskId, message.actorEmail || 'Ktoś')

    const last = lastToastRef.current
    lastToastRef.current = message
    if (last && sameSeries(last, message)) return
    const scheduleName = isOpen ? null : (schedules?.find((s) => s.id === message.scheduleId)?.name ?? null)
    pushToast(message.actorEmail, describeScheduleChange(message, meId, isTaskChange ? scheduleName : null))
  }

  const handleInvitationReceived = (message: ScheduleInvitationReceivedMessage) => {
    void loadInvitations()
    if (message.actorUserId !== meId) pushToast(message.actorEmail, `zaprasza Cię do harmonogramu „${message.scheduleName}”`)
  }

  const liveStatus = useScheduleHub({
    onScheduleChanged: handleScheduleChanged,
    onInvitationReceived: handleInvitationReceived,
    // Po przerwie w połączeniu wiadomości przepadły – pobieramy wszystko od nowa.
    onResync: () => {
      refreshSchedulesQuietly()
      void loadInvitations()
      setTasksRefreshSignal((n) => n + 1)
    },
  })

  const closeDialog = () => setDialog(null)

  /** `share` – w nowym harmonogramie zaznaczono „Harmonogram wspólny”: od razu otwieramy zapraszanie. */
  const handleSaved = (saved: Schedule, share = false) => {
    setSchedules((current) => [...(current ?? []).filter((s) => s.id !== saved.id), saved])
    setDialog(share ? { kind: 'share', schedule: saved } : null)
  }

  /** Przyjęte zaproszenie: harmonogram jest już dostępny – wczytujemy listę i od razu go otwieramy. */
  const handleInvitationAccepted = async (invitation: Invitation) => {
    setInvitations((current) => current.filter((i) => i.id !== invitation.id))
    await loadSchedules()
    setOpenedId(invitation.id)
  }

  /** Uprawnienia zmieniły się w innym miejscu (403) albo harmonogram zniknął – wczytujemy listę od nowa. */
  const handleAccessChanged = useCallback(() => {
    void loadSchedules()
  }, [loadSchedules])

  const handleShareGone = useCallback(() => {
    setDialog(null)
    void loadSchedules()
  }, [loadSchedules])

  const shareSchedule = dialog?.kind === 'share' ? dialog.schedule : null
  // Stały obiekt dla otwartego okna – okno wczytuje osoby w efekcie zależnym od niego.
  const shareApi = useMemo(() => (shareSchedule ? scheduleMembersApi(shareSchedule.id) : null), [shareSchedule])

  const handleDeleted = (id: string) => {
    setSchedules((current) => current?.filter((s) => s.id !== id) ?? null)
    setDialog(null)
  }

  /** Okno edycji zostaje otwarte – aktualizujemy tylko listę pod nim. */
  const handleChangedElsewhere = (change: Schedule | { deletedId: string }) => {
    setSchedules((current) => {
      if (!current) return current
      return 'deletedId' in change
        ? current.filter((s) => s.id !== change.deletedId)
        : current.map((s) => (s.id === change.id ? change : s))
    })
  }

  const openCreate = () => setDialog({ kind: 'create' })

  return (
    <div className="h-full overflow-y-auto bg-white text-slate-900">
      <main className="mx-auto w-full max-w-3xl px-4 pb-16 sm:px-8">
        {opened ? (
          <ScheduleTasksPage
            key={opened.id}
            schedule={opened}
            onBack={() => setOpenedId(null)}
            onShare={() => setDialog({ kind: 'share', schedule: opened })}
            onLeave={() => setDialog({ kind: 'leave', schedule: opened })}
            onAccessChanged={handleAccessChanged}
            liveStatus={liveStatus}
            refreshSignal={tasksRefreshSignal}
            changedTasks={Object.fromEntries(Object.entries(changedTasks).map(([id, change]) => [id, change.email]))}
            history={histories[opened.id] ?? []}
            meId={meId}
          />
        ) : (
          <>
            {/* Nagłówek jak w zakładce Notatki (NotesPage): tytuł text-lg font-semibold i ten sam przycisk. */}
            <header className="flex items-center justify-between gap-2 pt-5 pb-3">
              <div className="flex min-w-0 flex-col gap-0.5">
                <h1 className="text-lg font-semibold">Harmonogramy</h1>
                <LiveStatusBadge status={liveStatus} />
              </div>
              <Button onClick={openCreate}>Nowy harmonogram</Button>
            </header>

            <InvitationsPanel
              invitations={invitations}
              accept={acceptScheduleInvitation}
              decline={declineScheduleInvitation}
              onAccepted={(invitation) => void handleInvitationAccepted(invitation)}
              onRemoved={(id) => setInvitations((current) => current.filter((i) => i.id !== id))}
              className="mb-4"
            />

            <div className="mt-1">
              {listError ? (
                <div className="text-sm text-slate-600">
                  <p>{listError}</p>
                  <button type="button" onClick={loadSchedules} className="mt-2 font-medium text-blue-700 hover:underline">
                    Spróbuj ponownie
                  </button>
                </div>
              ) : schedules === null ? (
                <p role="status" className="text-sm text-slate-500">
                  Wczytywanie…
                </p>
              ) : schedules.length === 0 ? (
                <p className="text-sm text-slate-600">Nie masz jeszcze harmonogramów. Utwórz pierwszy przyciskiem powyżej.</p>
              ) : (
                <ul aria-label="Lista harmonogramów" className="divide-y divide-slate-200 overflow-hidden rounded-lg border border-slate-200">
                  {sortSchedules(schedules, today).map((schedule) => {
                    const status = getStatus(schedule, today)
                    const isOwner = schedule.access === 'owner'
                    const canManage = can(schedule.access, 'manage')
                    return (
                      <li key={schedule.id} className="relative flex items-start gap-2 py-3 pr-1 pl-4 hover:bg-slate-50 sm:items-center sm:gap-3 sm:pr-3">
                        {/* Cały wiersz otwiera zadania harmonogramu (pseudo-element przycisku rozciąga się na <li>). */}
                        <button
                          type="button"
                          onClick={() => setOpenedId(schedule.id)}
                          aria-label={`Otwórz zadania harmonogramu „${schedule.name}”`}
                          className="flex min-w-0 flex-1 flex-col items-start gap-1 text-left after:absolute after:inset-0 after:content-[''] focus-visible:outline-none focus-visible:after:outline-2 focus-visible:after:-outline-offset-2 focus-visible:after:outline-blue-600 sm:gap-0.5"
                        >
                          <span className={`max-w-full truncate text-sm font-medium ${status === 'finished' ? 'text-slate-600' : 'text-slate-900'}`}>
                            {schedule.name}
                          </span>
                          <span className="text-sm text-slate-500 tabular-nums">
                            {formatDateRange(schedule.startDate, schedule.endDate)} · {formatDays(countDays(schedule.startDate, schedule.endDate))}
                          </span>
                          <span className="mt-0.5 flex flex-wrap gap-2 sm:hidden">
                            <StatusBadge status={status} />
                            {!isOwner && <SharedBadge schedule={schedule} />}
                          </span>
                        </button>
                        <span className="hidden shrink-0 items-center gap-2 sm:flex">
                          {!isOwner && <SharedBadge schedule={schedule} />}
                          <StatusBadge status={status} />
                        </span>
                        {/* Nazwę, daty i dostęp zmienia tylko właściciel; członek opuszcza harmonogram z widoku zadań. */}
                        {canManage && (
                          <div className="relative z-10 flex shrink-0 sm:gap-0.5">
                            <IconButton
                              label={`Udostępnij „${schedule.name}”`}
                              title="Udostępnij"
                              touch
                              onClick={() => setDialog({ kind: 'share', schedule })}
                            >
                              <UserPlusIcon />
                            </IconButton>
                            <IconButton label={`Edytuj „${schedule.name}”`} title="Edytuj" touch onClick={() => setDialog({ kind: 'edit', schedule })}>
                              <PencilIcon />
                            </IconButton>
                            <IconButton
                              label={`Usuń „${schedule.name}”`}
                              title="Usuń"
                              tone="danger"
                              touch
                              onClick={() => setDialog({ kind: 'delete', schedule })}
                            >
                              <TrashIcon />
                            </IconButton>
                          </div>
                        )}
                      </li>
                    )
                  })}
                </ul>
              )}
            </div>
          </>
        )}
      </main>

      <LiveToasts toasts={toasts} onDismiss={dismissToast} />

      {dialog?.kind === 'create' && (
        <ScheduleFormDialog onClose={closeDialog} onSaved={handleSaved} onChangedElsewhere={handleChangedElsewhere} />
      )}
      {dialog?.kind === 'edit' && (
        <ScheduleFormDialog
          schedule={dialog.schedule}
          onClose={closeDialog}
          onSaved={handleSaved}
          onChangedElsewhere={handleChangedElsewhere}
        />
      )}
      {dialog?.kind === 'share' && shareApi && (
        <ShareDialog texts={scheduleShareTexts(dialog.schedule.name)} api={shareApi} onClose={closeDialog} onGone={handleShareGone} />
      )}
      {dialog?.kind === 'leave' && (
        <LeaveDialog
          title={`Opuścić harmonogram „${dialog.schedule.name}”?`}
          confirmLabel="Opuść harmonogram"
          leave={(userId) => leaveSchedule(dialog.schedule.id, userId)}
          onClose={closeDialog}
          onLeft={() => {
            const id = dialog.schedule.id
            setSchedules((current) => current?.filter((s) => s.id !== id) ?? null)
            setOpenedId(null)
            setDialog(null)
          }}
        />
      )}
      {dialog?.kind === 'delete' && <DeleteScheduleDialog schedule={dialog.schedule} onClose={closeDialog} onDeleted={handleDeleted} />}
    </div>
  )
}
