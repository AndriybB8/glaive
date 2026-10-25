import { useCallback, useEffect, useState } from 'react'
import { Alert } from '../../components/ui/Alert'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { IconButton } from '../../components/ui/IconButton'
import { PencilIcon, TrashIcon } from '../../components/ui/icons'
import { ProgressBar } from '../../components/ui/ProgressBar'
import { ApiError } from '../../lib/http'
import { countDays, formatDate, formatDateRange, formatDays, todayIso } from '../schedules/scheduleDates'
import type { Schedule } from '../schedules/types'
import { changeTaskStatus, getTasks } from './api'
import { DeleteTaskDialog } from './DeleteTaskDialog'
import { TaskFormDialog } from './TaskFormDialog'
import { groupTasks, isOverdue, priorityBadges, statusOptions } from './taskMeta'
import type { Task, TaskStatus } from './types'

const LOAD_ERROR = 'Nie udało się wczytać zadań. Sprawdź, czy backend jest uruchomiony.'
const SCHEDULE_GONE = 'Ten harmonogram został usunięty.'

type OpenDialog = { kind: 'create' } | { kind: 'edit'; task: Task } | { kind: 'delete'; task: Task } | null

interface TaskRowProps {
  task: Task
  today: string
  onStatusChange: (status: TaskStatus) => void
  onEdit: () => void
  onDelete: () => void
}

function TaskRow({ task, today, onStatusChange, onEdit, onDelete }: TaskRowProps) {
  const done = task.status === 'done'
  const priority = priorityBadges[task.priority]

  let dueText = <span>Bez terminu</span>
  if (task.dueDate !== null) {
    dueText = isOverdue(task, today) ? (
      <span className="text-red-700">Po terminie · {formatDate(task.dueDate)}</span>
    ) : (
      <span>Termin: {formatDate(task.dueDate)}</span>
    )
  }

  return (
    <li className="flex flex-wrap items-center gap-x-3 gap-y-2 py-3 pr-1 pl-4 hover:bg-slate-50 sm:flex-nowrap sm:pr-3">
      <div className="flex min-w-0 basis-full flex-col items-start gap-1 sm:flex-1 sm:basis-0 sm:gap-0.5">
        <span className={`max-w-full text-sm font-medium wrap-break-word ${done ? 'text-slate-500 line-through' : 'text-slate-900'}`}>
          {task.title}
        </span>
        {/* Na liście najwyżej 2 linie opisu; całość widać w oknie edycji. */}
        {task.description && (
          <p title={task.description} className="line-clamp-2 max-w-full text-sm whitespace-pre-line text-slate-600 wrap-break-word">
            {task.description}
          </p>
        )}
        <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm text-slate-500 tabular-nums">
          {dueText}
          <Badge tone={priority.tone}>{priority.label}</Badge>
        </div>
      </div>
      <div className="ml-auto flex shrink-0 items-center gap-1 sm:gap-0.5">
        <select
          value={task.status}
          onChange={(event) => onStatusChange(event.target.value as TaskStatus)}
          aria-label={`Status zadania „${task.title}”`}
          className="mr-1 h-11 rounded-md border border-slate-300 bg-white px-2 text-sm text-slate-700 focus:border-blue-600 focus:outline-2 focus:-outline-offset-1 focus:outline-blue-600 sm:h-8"
        >
          {statusOptions.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </select>
        <IconButton label={`Edytuj „${task.title}”`} title="Edytuj" touch onClick={onEdit}>
          <PencilIcon />
        </IconButton>
        <IconButton label={`Usuń „${task.title}”`} title="Usuń" tone="danger" touch onClick={onDelete}>
          <TrashIcon />
        </IconButton>
      </div>
    </li>
  )
}

interface ScheduleTasksPageProps {
  schedule: Schedule
  onBack: () => void
}

/** Zadania jednego harmonogramu z paskiem postępu. Postęp = zadania „Zrobione” / wszystkie. */
export function ScheduleTasksPage({ schedule, onBack }: ScheduleTasksPageProps) {
  const [tasks, setTasks] = useState<Task[] | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [dialog, setDialog] = useState<OpenDialog>(null)
  const today = todayIso()

  const describeLoadError = (error: unknown) => (error instanceof ApiError && error.status === 404 ? SCHEDULE_GONE : LOAD_ERROR)

  const loadTasks = useCallback(async () => {
    try {
      setLoadError(null)
      setTasks(await getTasks(schedule.id))
    } catch (error) {
      setLoadError(describeLoadError(error))
    }
  }, [schedule.id])

  useEffect(() => {
    let ignore = false
    getTasks(schedule.id)
      .then((result) => {
        if (!ignore) setTasks(result)
      })
      .catch((error: unknown) => {
        if (!ignore) setLoadError(describeLoadError(error))
      })
    return () => {
      ignore = true
    }
  }, [schedule.id])

  const replaceTask = (updated: Task) => setTasks((current) => current?.map((t) => (t.id === updated.id ? updated : t)) ?? current)
  const removeTask = (id: string) => setTasks((current) => current?.filter((t) => t.id !== id) ?? current)

  const handleSaved = (saved: Task) => {
    setTasks((current) => [...(current ?? []).filter((t) => t.id !== saved.id), saved])
    setDialog(null)
  }

  const handleDeleted = (id: string) => {
    removeTask(id)
    setDialog(null)
  }

  const handleChangedElsewhere = (change: Task | { deletedId: string }) => {
    if ('deletedId' in change) removeTask(change.deletedId)
    else replaceTask(change)
  }

  /** Zmiana od razu widoczna na liście i w pasku postępu; przy błędzie wracamy do poprzedniego stanu. */
  const handleStatusChange = async (task: Task, status: TaskStatus) => {
    setActionError(null)
    replaceTask({ ...task, status })
    try {
      replaceTask(await changeTaskStatus(task.id, status))
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) {
        removeTask(task.id)
        setActionError('To zadanie zostało usunięte w innym miejscu.')
      } else {
        replaceTask(task)
        setActionError('Nie udało się zmienić statusu zadania.')
      }
    }
  }

  const doneCount = tasks?.filter((t) => t.status === 'done').length ?? 0
  const totalCount = tasks?.length ?? 0

  return (
    <>
      <div className="pt-4">
        <Button variant="ghost" onClick={onBack} className="-ml-2">
          Wszystkie harmonogramy
        </Button>
      </div>

      {/* Nagłówek jak w zakładce Notatki: tytuł text-lg font-semibold i główny przycisk. */}
      <header className="flex items-center justify-between gap-2 pt-2 pb-1">
        <h1 className="min-w-0 truncate text-lg font-semibold">{schedule.name}</h1>
        <Button onClick={() => setDialog({ kind: 'create' })} disabled={loadError === SCHEDULE_GONE}>
          Nowe zadanie
        </Button>
      </header>
      <p className="text-sm text-slate-500 tabular-nums">
        {formatDateRange(schedule.startDate, schedule.endDate)} · {formatDays(countDays(schedule.startDate, schedule.endDate))}
      </p>

      {loadError ? (
        <div className="mt-6 text-sm text-slate-600">
          <p>{loadError}</p>
          {loadError === LOAD_ERROR && (
            <button type="button" onClick={loadTasks} className="mt-2 font-medium text-blue-700 hover:underline">
              Spróbuj ponownie
            </button>
          )}
        </div>
      ) : tasks === null ? (
        <p role="status" className="mt-6 text-sm text-slate-500">
          Wczytywanie…
        </p>
      ) : (
        <>
          <section aria-label="Postęp" className="mt-5 rounded-lg border border-slate-200 p-4">
            <div className="mb-2 flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1 text-sm">
              <span className="font-medium text-slate-900">Postęp</span>
              <span className="text-slate-600 tabular-nums">
                {totalCount === 0
                  ? 'Brak zadań'
                  : doneCount === totalCount
                    ? `Wszystkie zadania ukończone · ${totalCount}`
                    : `Ukończone: ${doneCount} z ${totalCount} · ${Math.round((doneCount / totalCount) * 100)}%`}
              </span>
            </div>
            <ProgressBar value={doneCount} max={totalCount} label="Postęp harmonogramu" />
          </section>

          {actionError && (
            <div className="mt-4">
              <Alert tone="error">{actionError}</Alert>
            </div>
          )}

          {tasks.length === 0 ? (
            <p className="mt-6 text-sm text-slate-600">Ten harmonogram nie ma jeszcze zadań. Dodaj pierwsze przyciskiem powyżej.</p>
          ) : (
            groupTasks(tasks).map((group) => {
              const headingId = `tasks-${group.status}`
              return (
                <section key={group.status} aria-labelledby={headingId} className="mt-6">
                  <h2 id={headingId} className="mb-2 text-sm font-medium text-slate-700">
                    {group.label} <span className="font-normal text-slate-500 tabular-nums">({group.tasks.length})</span>
                  </h2>
                  <ul className="divide-y divide-slate-200 overflow-hidden rounded-lg border border-slate-200">
                    {group.tasks.map((task) => (
                      <TaskRow
                        key={task.id}
                        task={task}
                        today={today}
                        onStatusChange={(status) => void handleStatusChange(task, status)}
                        onEdit={() => setDialog({ kind: 'edit', task })}
                        onDelete={() => setDialog({ kind: 'delete', task })}
                      />
                    ))}
                  </ul>
                </section>
              )
            })
          )}
        </>
      )}

      {dialog?.kind === 'create' && (
        <TaskFormDialog schedule={schedule} onClose={() => setDialog(null)} onSaved={handleSaved} onChangedElsewhere={handleChangedElsewhere} />
      )}
      {dialog?.kind === 'edit' && (
        <TaskFormDialog
          schedule={schedule}
          task={dialog.task}
          onClose={() => setDialog(null)}
          onSaved={handleSaved}
          onChangedElsewhere={handleChangedElsewhere}
        />
      )}
      {dialog?.kind === 'delete' && <DeleteTaskDialog task={dialog.task} onClose={() => setDialog(null)} onDeleted={handleDeleted} />}
    </>
  )
}
