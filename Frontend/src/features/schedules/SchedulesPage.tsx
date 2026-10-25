import { useCallback, useEffect, useState } from 'react'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { IconButton } from '../../components/ui/IconButton'
import { PencilIcon, TrashIcon } from '../../components/ui/icons'
import { ScheduleTasksPage } from '../tasks/ScheduleTasksPage'
import { getSchedules } from './api'
import { DeleteScheduleDialog } from './DeleteScheduleDialog'
import { ScheduleFormDialog } from './ScheduleFormDialog'
import { countDays, formatDateRange, formatDays, getStatus, sortSchedules, todayIso } from './scheduleDates'
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

type OpenDialog = { kind: 'create' } | { kind: 'edit'; schedule: Schedule } | { kind: 'delete'; schedule: Schedule } | null

export function SchedulesPage() {
  const [schedules, setSchedules] = useState<Schedule[] | null>(null)
  const [listError, setListError] = useState<string | null>(null)
  const [dialog, setDialog] = useState<OpenDialog>(null)
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

  const closeDialog = () => setDialog(null)

  const handleSaved = (saved: Schedule) => {
    setSchedules((current) => [...(current ?? []).filter((s) => s.id !== saved.id), saved])
    setDialog(null)
  }

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
          <ScheduleTasksPage key={opened.id} schedule={opened} onBack={() => setOpenedId(null)} />
        ) : (
          <>
            {/* Nagłówek jak w zakładce Notatki (NotesPage): tytuł text-lg font-semibold i ten sam przycisk. */}
            <header className="flex items-center justify-between gap-2 pt-5 pb-3">
              <h1 className="text-lg font-semibold">Harmonogramy</h1>
              <Button onClick={openCreate}>Nowy harmonogram</Button>
            </header>

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
                          <span className="mt-0.5 sm:hidden">
                            <StatusBadge status={status} />
                          </span>
                        </button>
                        <span className="hidden shrink-0 sm:block">
                          <StatusBadge status={status} />
                        </span>
                        <div className="relative z-10 flex shrink-0 sm:gap-0.5">
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
      {dialog?.kind === 'delete' && <DeleteScheduleDialog schedule={dialog.schedule} onClose={closeDialog} onDeleted={handleDeleted} />}
    </div>
  )
}
