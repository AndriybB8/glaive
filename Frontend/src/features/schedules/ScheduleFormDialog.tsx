import { useId, useState, type ChangeEvent, type SubmitEvent } from 'react'
import { Alert } from '../../components/ui/Alert'
import { Button } from '../../components/ui/Button'
import { Dialog } from '../../components/ui/Dialog'
import { TextField } from '../../components/ui/TextField'
import { ApiError } from '../../lib/http'
import { createSchedule, isScheduleConflict, updateSchedule } from './api'
import { countDays, formatDateRange, formatDays, todayIso } from './scheduleDates'
import type { Schedule, ScheduleDraft } from './types'

const NAME_MAX_LENGTH = 200

interface FieldErrors {
  name?: string
  startDate?: string
  endDate?: string
  /** Błąd zakresu dotyczy obu dat, więc pokazujemy go pod nimi. */
  range?: string
}

// Te same reguły sprawdza backend (ScheduleService); tu tylko po to, żeby nie czekać na serwer.
function validate(draft: ScheduleDraft): FieldErrors {
  const errors: FieldErrors = {}
  if (!draft.name.trim()) errors.name = 'Podaj nazwę harmonogramu.'
  if (!draft.startDate) errors.startDate = 'Podaj datę rozpoczęcia.'
  if (!draft.endDate) errors.endDate = 'Podaj datę zakończenia.'
  if (draft.startDate && draft.endDate && draft.endDate < draft.startDate) {
    errors.range = 'Data zakończenia nie może być wcześniejsza niż data rozpoczęcia.'
  }
  return errors
}

function toDraft(schedule: Schedule): ScheduleDraft {
  return { name: schedule.name, startDate: schedule.startDate, endDate: schedule.endDate }
}

interface ScheduleFormDialogProps {
  /** Brak = tworzenie nowego harmonogramu. */
  schedule?: Schedule
  onClose: () => void
  onSaved: (schedule: Schedule) => void
  /** Serwer zgłosił, że harmonogram zmieniono lub usunięto w innym miejscu – lista powinna to odzwierciedlić. */
  onChangedElsewhere: (current: Schedule | { deletedId: string }) => void
}

export function ScheduleFormDialog({ schedule, onClose, onSaved, onChangedElsewhere }: ScheduleFormDialogProps) {
  const [draft, setDraft] = useState<ScheduleDraft>(() =>
    schedule ? toDraft(schedule) : { name: '', startDate: todayIso(), endDate: '' },
  )
  const [version, setVersion] = useState(schedule?.version ?? 0)
  /** Błędy pokazujemy dopiero po pierwszej próbie zapisu, potem na bieżąco. */
  const [showErrors, setShowErrors] = useState(false)
  const [saving, setSaving] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)
  const [conflict, setConflict] = useState<Schedule | null>(null)
  const rangeErrorId = useId()

  const errors = showErrors ? validate(draft) : {}
  const days = draft.startDate && draft.endDate && draft.endDate >= draft.startDate ? countDays(draft.startDate, draft.endDate) : null

  const change = (field: keyof ScheduleDraft) => (event: ChangeEvent<HTMLInputElement>) => {
    const value = event.target.value
    setDraft((current) => ({ ...current, [field]: value }))
  }

  const save = async (versionToSend: number) => {
    setSaving(true)
    setSaveError(null)
    setConflict(null)
    try {
      const saved = schedule ? await updateSchedule(schedule.id, draft, versionToSend) : await createSchedule(draft)
      onSaved(saved)
    } catch (error) {
      if (isScheduleConflict(error)) {
        setConflict(error.current)
        onChangedElsewhere(error.current)
      } else if (schedule && error instanceof ApiError && error.status === 404) {
        setSaveError('Ten harmonogram został w międzyczasie usunięty.')
        onChangedElsewhere({ deletedId: schedule.id })
      } else if (error instanceof ApiError && error.status === 400) {
        setSaveError(error.message)
      } else {
        setSaveError('Nie udało się zapisać harmonogramu. Spróbuj ponownie.')
      }
      setSaving(false)
    }
  }

  const handleSubmit = (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault()
    setShowErrors(true)
    if (Object.keys(validate(draft)).length > 0) return
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
      title={schedule ? 'Edytuj harmonogram' : 'Nowy harmonogram'}
      onClose={onClose}
      onSubmit={handleSubmit}
      busy={saving}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={saving} className="min-h-11 sm:min-h-0">
            Anuluj
          </Button>
          <Button type="submit" disabled={saving} className="min-h-11 sm:min-h-0">
            {saving ? 'Zapisywanie…' : schedule ? 'Zapisz' : 'Utwórz'}
          </Button>
        </>
      }
    >
      <TextField
        label="Nazwa"
        value={draft.name}
        onChange={change('name')}
        maxLength={NAME_MAX_LENGTH}
        placeholder="np. Sesja zimowa"
        autoComplete="off"
        error={errors.name}
        data-autofocus
      />

      <div className="flex flex-col gap-1.5">
        <div className="grid gap-4 sm:grid-cols-2 sm:gap-3">
          <TextField type="date" label="Data rozpoczęcia" value={draft.startDate} onChange={change('startDate')} error={errors.startDate} />
          <TextField
            type="date"
            label="Data zakończenia"
            value={draft.endDate}
            min={draft.startDate || undefined}
            onChange={change('endDate')}
            error={errors.endDate}
            invalid={Boolean(errors.range)}
            aria-describedby={errors.range ? rangeErrorId : undefined}
          />
        </div>
        {errors.range ? (
          <p id={rangeErrorId} className="text-sm text-red-700">
            {errors.range}
          </p>
        ) : (
          days !== null && <p className="text-sm text-slate-500">Czas trwania: {formatDays(days)}</p>
        )}
      </div>

      {conflict && (
        <Alert
          tone="warning"
          title="Ten harmonogram został zmieniony w innym miejscu."
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
          Wersja na serwerze: „{conflict.name}”, {formatDateRange(conflict.startDate, conflict.endDate)}. Wybierz, którą wersję zachować.
        </Alert>
      )}
      {saveError && <Alert tone="error">{saveError}</Alert>}
    </Dialog>
  )
}
