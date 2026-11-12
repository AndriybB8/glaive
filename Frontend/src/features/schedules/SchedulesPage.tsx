import { useCallback, useEffect, useMemo, useState } from 'react'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { IconButton } from '../../components/ui/IconButton'
import { PencilIcon, TrashIcon, UserPlusIcon, UsersIcon } from '../../components/ui/icons'
import { InvitationsPanel } from '../sharing/InvitationsPanel'
import { LeaveDialog } from '../sharing/LeaveDialog'
import { ShareDialog } from '../sharing/ShareDialog'
import { ACCESS_LABELS } from '../sharing/sharing'
import type { Invitation } from '../sharing/types'
import { ScheduleTasksPage } from '../tasks/ScheduleTasksPage'
import { getSchedules } from './api'
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
          />
        ) : (
          <>
            {/* Nagłówek jak w zakładce Notatki (NotesPage): tytuł text-lg font-semibold i ten sam przycisk. */}
            <header className="flex items-center justify-between gap-2 pt-5 pb-3">
              <h1 className="text-lg font-semibold">Harmonogramy</h1>
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
                        {isOwner && (
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
