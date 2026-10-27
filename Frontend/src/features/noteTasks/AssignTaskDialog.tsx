import { useEffect, useState } from 'react'
import { Alert } from '../../components/ui/Alert'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Dialog } from '../../components/ui/Dialog'
import { SelectField } from '../../components/ui/SelectField'
import { ApiError } from '../../lib/http'
import { getSchedules } from '../schedules/api'
import { formatDate, getStatus, todayIso } from '../schedules/scheduleDates'
import type { Schedule } from '../schedules/types'
import { getTasks } from '../tasks/api'
import type { Task, TaskStatus } from '../tasks/types'
import { linkNoteToTask, unlinkNoteFromTask } from './api'
import { statusBadges } from './taskStatusBadge'
import type { LinkedTask } from './types'

const statusOrder: Record<TaskStatus, number> = { toDo: 0, inProgress: 1, done: 2 }

/** Najpierw niezrobione, potem wg terminu (bez terminu na końcu), potem alfabetycznie. */
function sortTasks(tasks: Task[]): Task[] {
  return [...tasks].sort(
    (a, b) =>
      statusOrder[a.status] - statusOrder[b.status] ||
      (a.dueDate ?? '9999').localeCompare(b.dueDate ?? '9999') ||
      a.title.localeCompare(b.title, 'pl'),
  )
}

/** Domyślny harmonogram: ten, z którego notatka ma już zadanie, potem trwający, potem pierwszy z listy. */
function pickDefaultSchedule(schedules: Schedule[], linked: LinkedTask[]): string {
  const today = todayIso()
  return (
    schedules.find((s) => linked.some((t) => t.scheduleId === s.id))?.id ??
    schedules.find((s) => getStatus(s, today) === 'ongoing')?.id ??
    schedules[0]?.id ??
    ''
  )
}

interface AssignTaskDialogProps {
  noteId: string
  /** Zadania już przypisane – zaznaczone na liście. */
  linked: LinkedTask[]
  onLinked: (task: LinkedTask) => void
  onUnlinked: (taskId: string) => void
  onClose: () => void
}

/** Wybór zadań z harmonogramu. Zaznaczenie od razu przypisuje notatkę, odznaczenie – odłącza. */
export function AssignTaskDialog({ noteId, linked, onLinked, onUnlinked, onClose }: AssignTaskDialogProps) {
  const [schedules, setSchedules] = useState<Schedule[] | null>(null)
  const [scheduleId, setScheduleId] = useState('')
  const [loadError, setLoadError] = useState<string | null>(null)
  // Domyślny harmonogram wybieramy raz, wg zadań przypisanych w chwili otwarcia okna.
  const [initialLinked] = useState(linked)

  useEffect(() => {
    let ignore = false
    getSchedules()
      .then((result) => {
        if (ignore) return
        setSchedules(result)
        setScheduleId(pickDefaultSchedule(result, initialLinked))
      })
      .catch(() => {
        if (!ignore) setLoadError('Nie udało się wczytać harmonogramów.')
      })
    return () => {
      ignore = true
    }
  }, [initialLinked])

  return (
    <Dialog
      title="Przypisz do zadania"
      description="Zaznacz zadania, których dotyczy ta notatka."
      onClose={onClose}
      footer={
        <Button onClick={onClose} className="col-span-2 min-h-11 sm:min-h-0" data-autofocus>
          Gotowe
        </Button>
      }
    >
      {loadError ? (
        <Alert tone="error">{loadError}</Alert>
      ) : schedules === null ? (
        <p role="status" className="text-sm text-slate-500">
          Wczytywanie…
        </p>
      ) : schedules.length === 0 ? (
        <p className="text-sm text-slate-600">
          Nie masz jeszcze harmonogramów. Zadania dodasz w zakładce{' '}
          <a href="#harmonogramy" className="font-medium text-blue-700 hover:underline">
            Harmonogramy
          </a>
          .
        </p>
      ) : (
        <>
          <SelectField
            label="Harmonogram"
            options={schedules.map((s) => ({ value: s.id, label: s.name }))}
            value={scheduleId}
            onChange={(event) => setScheduleId(event.target.value)}
          />
          {/* key: przy zmianie harmonogramu lista zadań wczytuje się od nowa. */}
          <ScheduleTaskPicker key={scheduleId} noteId={noteId} scheduleId={scheduleId} linked={linked} onLinked={onLinked} onUnlinked={onUnlinked} />
        </>
      )}
    </Dialog>
  )
}

interface ScheduleTaskPickerProps {
  noteId: string
  scheduleId: string
  linked: LinkedTask[]
  onLinked: (task: LinkedTask) => void
  onUnlinked: (taskId: string) => void
}

function ScheduleTaskPicker({ noteId, scheduleId, linked, onLinked, onUnlinked }: ScheduleTaskPickerProps) {
  const [tasks, setTasks] = useState<Task[] | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [pending, setPending] = useState<ReadonlySet<string>>(new Set())
  const linkedIds = new Set(linked.map((t) => t.id))

  useEffect(() => {
    let ignore = false
    getTasks(scheduleId)
      .then((result) => {
        if (!ignore) setTasks(sortTasks(result))
      })
      .catch(() => {
        if (!ignore) setLoadError('Nie udało się wczytać zadań tego harmonogramu.')
      })
    return () => {
      ignore = true
    }
  }, [scheduleId])

  const setTaskPending = (taskId: string, value: boolean) =>
    setPending((current) => {
      const next = new Set(current)
      if (value) next.add(taskId)
      else next.delete(taskId)
      return next
    })

  const toggle = async (task: Task, assign: boolean) => {
    setActionError(null)
    setTaskPending(task.id, true)
    try {
      if (assign) onLinked(await linkNoteToTask(noteId, task.id))
      else {
        await unlinkNoteFromTask(noteId, task.id)
        onUnlinked(task.id)
      }
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) {
        setActionError(error.message)
        if (assign) setTasks((current) => current?.filter((t) => t.id !== task.id) ?? current)
      } else {
        setActionError(assign ? 'Nie udało się przypisać notatki do zadania.' : 'Nie udało się odłączyć notatki od zadania.')
      }
    } finally {
      setTaskPending(task.id, false)
    }
  }

  if (loadError) return <Alert tone="error">{loadError}</Alert>
  if (tasks === null) {
    return (
      <p role="status" className="text-sm text-slate-500">
        Wczytywanie zadań…
      </p>
    )
  }
  if (tasks.length === 0) return <p className="text-sm text-slate-600">Ten harmonogram nie ma jeszcze zadań.</p>

  return (
    <>
      <fieldset className="min-w-0">
        <legend className="mb-1.5 text-sm font-medium text-slate-700">Zadania</legend>
        <ul className="max-h-72 divide-y divide-slate-200 overflow-y-auto rounded-lg border border-slate-200">
          {tasks.map((task) => {
            const badge = statusBadges[task.status]
            return (
              <li key={task.id}>
                <label className="flex cursor-pointer items-center gap-3 px-3 py-2.5 hover:bg-slate-50 has-disabled:cursor-wait">
                  <input
                    type="checkbox"
                    checked={linkedIds.has(task.id)}
                    disabled={pending.has(task.id)}
                    onChange={(event) => void toggle(task, event.target.checked)}
                    className="size-4 shrink-0 accent-blue-600"
                  />
                  <span className="flex min-w-0 flex-1 flex-col">
                    <span className={`text-sm wrap-break-word ${task.status === 'done' ? 'text-slate-500' : 'text-slate-900'}`}>{task.title}</span>
                    {task.dueDate && <span className="text-xs text-slate-500 tabular-nums">Termin: {formatDate(task.dueDate)}</span>}
                  </span>
                  <Badge tone={badge.tone}>{badge.label}</Badge>
                </label>
              </li>
            )
          })}
        </ul>
      </fieldset>
      {actionError && <Alert tone="error">{actionError}</Alert>}
    </>
  )
}
