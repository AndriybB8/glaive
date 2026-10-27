import { useEffect, useId, useState } from 'react'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { IconButton } from '../../components/ui/IconButton'
import { CloseIcon } from '../../components/ui/icons'
import { ApiError } from '../../lib/http'
import { formatDate } from '../schedules/scheduleDates'
import { getNoteTasks, unlinkNoteFromTask } from './api'
import { AssignTaskDialog } from './AssignTaskDialog'
import { statusBadges } from './taskStatusBadge'
import type { LinkedTask } from './types'

/** Zadania przypisane do notatki – pod tytułem notatki, obok wyboru grupy. */
export function NoteTasksSection({ noteId }: { noteId: string }) {
  const headingId = useId()
  const [tasks, setTasks] = useState<LinkedTask[] | null>(null)
  const [loadError, setLoadError] = useState(false)
  const [actionError, setActionError] = useState<string | null>(null)
  const [removingId, setRemovingId] = useState<string | null>(null)
  const [dialogOpen, setDialogOpen] = useState(false)

  useEffect(() => {
    let ignore = false
    getNoteTasks(noteId)
      .then((result) => {
        if (!ignore) setTasks(result)
      })
      .catch(() => {
        if (!ignore) setLoadError(true)
      })
    return () => {
      ignore = true
    }
  }, [noteId])

  const handleLinked = (task: LinkedTask) =>
    setTasks((current) => [...(current ?? []).filter((t) => t.id !== task.id), task])

  const handleUnlinked = (taskId: string) => setTasks((current) => current?.filter((t) => t.id !== taskId) ?? current)

  const unlink = async (task: LinkedTask) => {
    setActionError(null)
    setRemovingId(task.id)
    try {
      await unlinkNoteFromTask(noteId, task.id)
      handleUnlinked(task.id)
    } catch (error) {
      setActionError(
        error instanceof ApiError && error.status === 404 ? 'Ta notatka została w międzyczasie usunięta.' : 'Nie udało się odłączyć zadania.',
      )
    } finally {
      setRemovingId(null)
    }
  }

  return (
    <section aria-labelledby={headingId} className="mb-4 text-sm">
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
        <h2 id={headingId} className="text-slate-500">
          Zadania
        </h2>
        {loadError ? (
          <span className="text-red-700">Nie udało się wczytać przypisanych zadań.</span>
        ) : tasks === null ? (
          <span className="text-slate-500">Wczytywanie…</span>
        ) : (
          tasks.length === 0 && <span className="text-slate-500">Brak</span>
        )}
        <Button variant="ghost" onClick={() => setDialogOpen(true)} disabled={tasks === null} className="min-h-11 sm:min-h-0">
          Przypisz do zadania
        </Button>
      </div>

      {tasks && tasks.length > 0 && (
        <ul className="mt-2 flex flex-col gap-1.5">
          {tasks.map((task) => {
            const badge = statusBadges[task.status]
            return (
              <li key={task.id} className="flex items-center gap-2 rounded-md border border-slate-200 py-1 pr-1 pl-3">
                <div className="min-w-0 flex-1 py-0.5">
                  <p className={`truncate ${task.status === 'done' ? 'text-slate-500' : 'text-slate-800'}`}>{task.title}</p>
                  <p className="truncate text-xs text-slate-500 tabular-nums">
                    {task.scheduleName}
                    {task.dueDate && ` · termin ${formatDate(task.dueDate)}`}
                  </p>
                </div>
                <Badge tone={badge.tone}>{badge.label}</Badge>
                <IconButton
                  label={`Odłącz od zadania „${task.title}”`}
                  title="Odłącz od zadania"
                  touch
                  disabled={removingId === task.id}
                  onClick={() => void unlink(task)}
                >
                  <CloseIcon />
                </IconButton>
              </li>
            )
          })}
        </ul>
      )}

      {actionError && (
        <p role="alert" className="mt-2 text-red-700">
          {actionError}
        </p>
      )}

      {dialogOpen && tasks && (
        <AssignTaskDialog
          noteId={noteId}
          linked={tasks}
          onLinked={handleLinked}
          onUnlinked={handleUnlinked}
          onClose={() => setDialogOpen(false)}
        />
      )}
    </section>
  )
}
