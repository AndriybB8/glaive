import { useEffect, useState, type ChangeEvent, type SubmitEvent } from 'react'
import { Alert } from '../../components/ui/Alert'
import { Button } from '../../components/ui/Button'
import { Dialog } from '../../components/ui/Dialog'
import { SelectField } from '../../components/ui/SelectField'
import { TextAreaField } from '../../components/ui/TextAreaField'
import { TextField } from '../../components/ui/TextField'
import { ApiError } from '../../lib/http'
import { TaskNotesSection } from '../noteTasks/TaskNotesSection'
import { formatDate } from '../schedules/scheduleDates'
import type { Schedule } from '../schedules/types'
import { getTags } from '../tags/api'
import type { Tag } from '../tags/types'
import { getTaskTags, syncTaskTags } from '../taskTags/api'
import { TaskTagPicker } from '../taskTags/TaskTagPicker'
import { createTask, isTaskConflict, updateTask } from './api'
import { priorityOptions, statusOptions } from './taskMeta'
import type { Task, TaskDraft, TaskPriority, TaskStatus } from './types'

const TITLE_MAX_LENGTH = 200
const DESCRIPTION_MAX_LENGTH = 2000

interface FieldErrors {
  title?: string
  description?: string
  dueDate?: string
}

// Te same reguły sprawdza backend (TaskService); tu tylko po to, żeby nie czekać na serwer.
function validate(draft: TaskDraft, schedule: Schedule): FieldErrors {
  const errors: FieldErrors = {}
  if (!draft.title.trim()) errors.title = 'Podaj tytuł zadania.'
  if (draft.description.trim().length > DESCRIPTION_MAX_LENGTH) {
    errors.description = `Opis może mieć maksymalnie ${DESCRIPTION_MAX_LENGTH} znaków.`
  }
  if (draft.dueDate && (draft.dueDate < schedule.startDate || draft.dueDate > schedule.endDate)) {
    errors.dueDate = 'Termin musi mieścić się w harmonogramie.'
  }
  return errors
}

function toDraft(task: Task): TaskDraft {
  return { title: task.title, description: task.description ?? '', dueDate: task.dueDate, priority: task.priority, status: task.status }
}

interface TaskFormDialogProps {
  schedule: Schedule
  /** Brak = dodawanie nowego zadania. */
  task?: Task
  onClose: () => void
  /**
   * Zadanie zapisane. `tags`: aktualne tagi zadania; null = tagi się nie zmieniły (np. nie udało się ich wczytać).
   * `tagsFailed`: zadanie zapisano, ale tagów nie – lista powinna pobrać je od nowa i pokazać błąd.
   */
  onSaved: (task: Task, tags: Tag[] | null, tagsFailed?: boolean) => void
  /** Serwer zgłosił, że zadanie zmieniono lub usunięto w innym miejscu – lista powinna to odzwierciedlić. */
  onChangedElsewhere: (current: Task | { deletedId: string }) => void
}

export function TaskFormDialog({ schedule, task, onClose, onSaved, onChangedElsewhere }: TaskFormDialogProps) {
  const [draft, setDraft] = useState<TaskDraft>(() =>
    task ? toDraft(task) : { title: '', description: '', dueDate: null, priority: 'medium', status: 'toDo' },
  )
  const [version, setVersion] = useState(task?.version ?? 0)
  /** Błędy pokazujemy dopiero po pierwszej próbie zapisu, potem na bieżąco. */
  const [showErrors, setShowErrors] = useState(false)
  const [saving, setSaving] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)
  const [conflict, setConflict] = useState<Task | null>(null)

  // Tagi: wszystkie tagi użytkownika, tagi zadania przy otwarciu okna i bieżący wybór.
  const [allTags, setAllTags] = useState<Tag[] | null>(null)
  const [initialTagIds, setInitialTagIds] = useState<string[] | null>(task ? null : [])
  const [selectedTagIds, setSelectedTagIds] = useState<ReadonlySet<string>>(new Set())
  const [tagsLoadError, setTagsLoadError] = useState(false)

  useEffect(() => {
    let ignore = false
    Promise.all([getTags(), task ? getTaskTags(task.id) : Promise.resolve([])])
      .then(([tags, current]) => {
        if (ignore) return
        const ids = current.map((t) => t.id)
        setAllTags(tags)
        setInitialTagIds(ids)
        setSelectedTagIds(new Set(ids))
      })
      .catch(() => {
        if (!ignore) setTagsLoadError(true)
      })
    return () => {
      ignore = true
    }
  }, [task])

  const toggleTag = (tagId: string) =>
    setSelectedTagIds((current) => {
      const next = new Set(current)
      if (next.has(tagId)) next.delete(tagId)
      else next.add(tagId)
      return next
    })

  /** Po zapisie zadania: zapis tagów. Tagi mają osobne API, więc to drugi krok (dla nowego zadania znamy już jego id). */
  const saveTags = async (saved: Task) => {
    if (allTags === null || initialTagIds === null) {
      onSaved(saved, null)
      return
    }
    const selected = [...selectedTagIds]
    try {
      await syncTaskTags(saved.id, initialTagIds, selected)
      onSaved(saved, allTags.filter((t) => selectedTagIds.has(t.id)))
    } catch {
      onSaved(saved, null, true)
    }
  }

  const errors = showErrors ? validate(draft, schedule) : {}
  const rangeHint = `Opcjonalnie, od ${formatDate(schedule.startDate)} do ${formatDate(schedule.endDate)}.`

  const change = <K extends keyof TaskDraft>(field: K, value: TaskDraft[K]) => {
    setDraft((current) => ({ ...current, [field]: value }))
  }

  const save = async (versionToSend: number) => {
    setSaving(true)
    setSaveError(null)
    setConflict(null)
    try {
      const saved = task ? await updateTask(task.id, draft, versionToSend) : await createTask(schedule.id, draft)
      await saveTags(saved)
    } catch (error) {
      if (isTaskConflict(error)) {
        setConflict(error.current)
        onChangedElsewhere(error.current)
      } else if (task && error instanceof ApiError && error.status === 404) {
        setSaveError('To zadanie zostało w międzyczasie usunięte.')
        onChangedElsewhere({ deletedId: task.id })
      } else if (error instanceof ApiError && (error.status === 400 || error.status === 403 || error.status === 404)) {
        // 403: harmonogram wspólny, a właściciel zmienił nam uprawnienia na „Podgląd”.
        setSaveError(error.message)
      } else {
        setSaveError('Nie udało się zapisać zadania. Spróbuj ponownie.')
      }
      setSaving(false)
    }
  }

  const handleSubmit = (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault()
    setShowErrors(true)
    if (Object.keys(validate(draft, schedule)).length > 0) return
    void save(version)
  }

  /** Konflikt: nadpisz wersję z serwera tym, co jest w formularzu. */
  const keepMine = () => {
    if (!conflict) return
    setVersion(conflict.version)
    void save(conflict.version)
  }

  const loadCurrent = () => {
    if (!conflict) return
    setDraft(toDraft(conflict))
    setVersion(conflict.version)
    setConflict(null)
  }

  return (
    <Dialog
      title={task ? 'Edytuj zadanie' : 'Nowe zadanie'}
      onClose={onClose}
      onSubmit={handleSubmit}
      busy={saving}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={saving} className="min-h-11 sm:min-h-0">
            Anuluj
          </Button>
          <Button type="submit" disabled={saving} className="min-h-11 sm:min-h-0">
            {saving ? 'Zapisywanie…' : task ? 'Zapisz' : 'Dodaj'}
          </Button>
        </>
      }
    >
      <TextField
        label="Tytuł"
        value={draft.title}
        onChange={(event: ChangeEvent<HTMLInputElement>) => change('title', event.target.value)}
        maxLength={TITLE_MAX_LENGTH}
        placeholder="np. Powtórzyć rozdział 3"
        autoComplete="off"
        error={errors.title}
        data-autofocus
      />
      <TextAreaField
        label="Opis"
        value={draft.description}
        onChange={(event) => change('description', event.target.value)}
        maxLength={DESCRIPTION_MAX_LENGTH}
        placeholder="np. Rozdziały 1–3, zadania z listy 2"
        hint="Opcjonalnie."
        error={errors.description}
      />
      <TextField
        type="date"
        label="Termin"
        value={draft.dueDate ?? ''}
        min={schedule.startDate}
        max={schedule.endDate}
        onChange={(event: ChangeEvent<HTMLInputElement>) => change('dueDate', event.target.value || null)}
        error={errors.dueDate}
        hint={errors.dueDate ? undefined : rangeHint}
      />
      <div className="grid gap-4 sm:grid-cols-2 sm:gap-3">
        <SelectField
          label="Priorytet"
          options={priorityOptions}
          value={draft.priority}
          onChange={(event) => change('priority', event.target.value as TaskPriority)}
        />
        <SelectField
          label="Status"
          options={statusOptions}
          value={draft.status}
          onChange={(event) => change('status', event.target.value as TaskStatus)}
        />
      </div>

      <TaskTagPicker
        tags={allTags}
        selectedIds={selectedTagIds}
        onToggle={toggleTag}
        loadError={tagsLoadError}
        disabled={saving}
      />

      {/* Szczegóły istniejącego zadania: podgląd przypisanych notatek. */}
      {task && <TaskNotesSection taskId={task.id} />}

      {conflict && (
        <Alert
          tone="warning"
          title="To zadanie zostało zmienione w innym miejscu."
          actions={
            <>
              <Button variant="warning" onClick={keepMine} disabled={saving}>
                Zapisz mimo to
              </Button>
              <Button variant="warning-outline" onClick={loadCurrent} disabled={saving}>
                Wczytaj aktualną wersję
              </Button>
            </>
          }
        >
          Wersja na serwerze: „{conflict.title}”. Wybierz, którą wersję zachować.
        </Alert>
      )}
      {saveError && <Alert tone="error">{saveError}</Alert>}
    </Dialog>
  )
}
