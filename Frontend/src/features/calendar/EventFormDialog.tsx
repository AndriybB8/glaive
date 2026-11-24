import { useEffect, useState, type SubmitEvent } from 'react'
import { Alert } from '../../components/ui/Alert'
import { Button } from '../../components/ui/Button'
import { Dialog } from '../../components/ui/Dialog'
import { SelectField } from '../../components/ui/SelectField'
import { TextAreaField } from '../../components/ui/TextAreaField'
import { TextField } from '../../components/ui/TextField'
import { ApiError, ConflictError } from '../../lib/http'
import { TagColorPicker } from '../tags/TagColorPicker'
import { createCalendarEvent, getCalendarEvent, TIME_ZONE, updateCalendarEvent } from './api'
import { addDays, toIsoDate, toTimeInput, toUtcIso } from './dates'
import { ScopePicker } from './ScopePicker'
import type { CalendarItem, EditScope, EventFields, RecurrenceFrequency } from './types'

/** Te same limity sprawdza backend (CalendarEvent). */
const TITLE_MAX = 200
const DESCRIPTION_MAX = 2000
const DEFAULT_COLOR = '#2563EB'
const DEFAULT_DURATION = 60

const RECURRENCE_OPTIONS: { value: RecurrenceFrequency; label: string }[] = [
  { value: 'none', label: 'Nie powtarza się' },
  { value: 'daily', label: 'Codziennie' },
  { value: 'weekly', label: 'Co tydzień' },
  { value: 'monthly', label: 'Co miesiąc' },
]

/** Nowe wydarzenie: dzień i (dla siatki godzin) minuta, w którą kliknięto. Bez minuty – całodniowe. */
export interface NewEventSlot {
  day: string
  minutes?: number
}

interface EventFormDialogProps {
  /** Edytowane wydarzenie (albo wystąpienie serii); brak = nowe w `slot`. */
  item?: CalendarItem
  slot?: NewEventSlot
  onClose: () => void
  onSaved: () => void
  onDelete: (item: CalendarItem) => void
  /** Wydarzenie zmieniono albo usunięto w innym miejscu – kalendarz trzeba pobrać od nowa. */
  onChangedElsewhere: () => void
}

interface Draft {
  title: string
  description: string
  color: string
  isAllDay: boolean
  startDate: string
  startTime: string
  endDate: string
  endTime: string
  recurrence: RecurrenceFrequency
  recurrenceUntil: string
}

function minutesToTime(minutes: number): string {
  return `${String(Math.floor(minutes / 60) % 24).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}`
}

function newDraft(slot: NewEventSlot): Draft {
  const start = slot.minutes ?? 9 * 60
  const end = start + DEFAULT_DURATION
  return {
    title: '',
    description: '',
    color: DEFAULT_COLOR,
    isAllDay: slot.minutes === undefined,
    startDate: slot.day,
    startTime: minutesToTime(start),
    endDate: end >= 24 * 60 ? addDays(slot.day, 1) : slot.day,
    endTime: minutesToTime(end % (24 * 60)),
    recurrence: 'none',
    recurrenceUntil: '',
  }
}

/** Formularz z klikniętego elementu – czasy tego wystąpienia. Powtarzanie dochodzi z serii (GET wydarzenia). */
function draftFromItem(item: CalendarItem): Draft {
  const startsAt = item.startsAt ? new Date(item.startsAt) : null
  const endsAt = item.endsAt ? new Date(item.endsAt) : null
  return {
    title: item.title,
    description: item.description ?? '',
    color: item.color ?? DEFAULT_COLOR,
    isAllDay: item.isAllDay,
    startDate: item.isAllDay ? item.startDate! : toIsoDate(startsAt!),
    startTime: item.isAllDay ? '09:00' : toTimeInput(item.startsAt!),
    endDate: item.isAllDay ? item.endDate! : toIsoDate(endsAt!),
    endTime: item.isAllDay ? '10:00' : toTimeInput(item.endsAt!),
    recurrence: 'none',
    recurrenceUntil: '',
  }
}

/** Czas trwania w minutach (do przesuwania końca razem z początkiem). */
function durationOf(draft: Draft): number {
  if (draft.isAllDay) return 0
  const minutes = (Date.parse(toUtcIso(draft.endDate, draft.endTime)) - Date.parse(toUtcIso(draft.startDate, draft.startTime))) / 60000
  return minutes > 0 ? minutes : DEFAULT_DURATION
}

/** Te same reguły sprawdza backend (CalendarEventService.Parse). */
function validate(draft: Draft, recurrenceVisible: boolean): string | null {
  const title = draft.title.trim()
  if (!title) return 'Podaj tytuł wydarzenia.'
  if (title.length > TITLE_MAX) return `Tytuł może mieć maksymalnie ${TITLE_MAX} znaków.`
  if (draft.description.trim().length > DESCRIPTION_MAX) return `Opis może mieć maksymalnie ${DESCRIPTION_MAX} znaków.`
  if (!draft.startDate) return 'Podaj dzień rozpoczęcia wydarzenia.'
  if (draft.isAllDay) {
    if (draft.endDate && draft.endDate < draft.startDate) return 'Dzień zakończenia nie może być wcześniejszy niż dzień rozpoczęcia.'
  } else {
    if (!draft.startTime || !draft.endDate || !draft.endTime) return 'Podaj początek i koniec wydarzenia.'
    if (toUtcIso(draft.endDate, draft.endTime) <= toUtcIso(draft.startDate, draft.startTime)) return 'Koniec wydarzenia musi być późniejszy niż jego początek.'
  }
  if (recurrenceVisible && draft.recurrence !== 'none' && draft.recurrenceUntil && draft.recurrenceUntil < draft.startDate) {
    return 'Powtarzanie nie może kończyć się przed pierwszym wystąpieniem.'
  }
  return null
}

function toFields(draft: Draft, recurrenceVisible: boolean): EventFields {
  const description = draft.description.trim()
  const recurrence = recurrenceVisible ? draft.recurrence : 'none'
  return {
    title: draft.title.trim(),
    description: description || null,
    color: draft.color,
    isAllDay: draft.isAllDay,
    startsAt: draft.isAllDay ? null : toUtcIso(draft.startDate, draft.startTime),
    endsAt: draft.isAllDay ? null : toUtcIso(draft.endDate, draft.endTime),
    startDate: draft.isAllDay ? draft.startDate : null,
    endDate: draft.isAllDay ? draft.endDate || draft.startDate : null,
    timeZone: TIME_ZONE,
    recurrence,
    recurrenceUntil: recurrence !== 'none' && draft.recurrenceUntil ? draft.recurrenceUntil : null,
  }
}

export function EventFormDialog({ item, slot, onClose, onSaved, onDelete, onChangedElsewhere }: EventFormDialogProps) {
  const isSeries = item?.isRecurring === true
  const [draft, setDraft] = useState<Draft>(() => (item ? draftFromItem(item) : newDraft(slot!)))
  const [scope, setScope] = useState<EditScope>('single')
  /** Seria wczytywana, żeby znać jej powtarzanie (CalendarItem go nie podaje). */
  const [loadingSeries, setLoadingSeries] = useState(isSeries)
  const [showErrors, setShowErrors] = useState(false)
  const [saving, setSaving] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)

  useEffect(() => {
    if (!item || !isSeries) return
    let ignore = false
    getCalendarEvent(item.id)
      .then((series) => {
        if (ignore) return
        setDraft((current) => ({ ...current, recurrence: series.recurrence, recurrenceUntil: series.recurrenceUntil ?? '' }))
        setLoadingSeries(false)
      })
      .catch(() => {
        if (ignore) return
        setLoadingSeries(false)
        setSaveError('Nie udało się wczytać ustawień powtarzania. Zmiana „to wydarzenie” nadal działa.')
      })
    return () => {
      ignore = true
    }
  }, [item, isSeries])

  /** Pojedyncze wystąpienie nie może mieć własnego powtarzania (backend to odrzuca), więc wtedy pola znikają. */
  const recurrenceVisible = !isSeries || scope !== 'single'
  const error = validate(draft, recurrenceVisible)

  const update = (change: Partial<Draft>) => setDraft((current) => ({ ...current, ...change }))

  /** Zmiana początku przesuwa koniec o tyle samo – jak w Google Calendar. */
  const moveStart = (change: Pick<Partial<Draft>, 'startDate' | 'startTime'>) =>
    setDraft((current) => {
      const next = { ...current, ...change }
      if (!next.startDate) return next
      if (current.isAllDay) {
        const length = current.endDate && current.startDate ? Math.round((Date.parse(current.endDate) - Date.parse(current.startDate)) / 86_400_000) : 0
        return { ...next, endDate: addDays(next.startDate, Math.max(0, length)) }
      }
      const end = new Date(Date.parse(toUtcIso(next.startDate, next.startTime || '00:00')) + durationOf(current) * 60000)
      return { ...next, endDate: toIsoDate(end), endTime: toTimeInput(end.toISOString()) }
    })

  const handleSubmit = async (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault()
    setShowErrors(true)
    if (error || loadingSeries) return

    setSaving(true)
    setSaveError(null)
    try {
      const fields = toFields(draft, recurrenceVisible)
      if (!item) await createCalendarEvent(fields)
      else if (!isSeries) await updateCalendarEvent(item.id, fields, { version: item.version ?? 0 })
      else
        await updateCalendarEvent(item.id, fields, {
          // „To wydarzenie” – wersja wystąpienia (0, gdy nie było osobno zmieniane); pozostałe – wersja serii.
          version: (scope === 'single' ? item.version : item.seriesVersion) ?? 0,
          scope,
          occurrenceDate: item.occurrenceDate,
        })
      onSaved()
    } catch (err) {
      if (err instanceof ConflictError) {
        setSaveError('Wydarzenie zmieniono w innym miejscu. Kalendarz jest już odświeżony – zamknij okno i otwórz wydarzenie ponownie.')
        onChangedElsewhere()
      } else if (err instanceof ApiError && err.status === 404) {
        setSaveError('To wydarzenie zostało w międzyczasie usunięte.')
        onChangedElsewhere()
      } else if (err instanceof ApiError && err.status === 400) {
        setSaveError(err.message)
      } else {
        setSaveError('Nie udało się zapisać wydarzenia. Spróbuj ponownie.')
      }
      setSaving(false)
    }
  }

  return (
    <Dialog
      title={item ? 'Edytuj wydarzenie' : 'Nowe wydarzenie'}
      onClose={onClose}
      onSubmit={(event) => void handleSubmit(event)}
      busy={saving}
      footer={
        <>
          {item && (
            <Button variant="ghost-danger" onClick={() => onDelete(item)} disabled={saving} className="col-span-2 min-h-11 sm:mr-auto sm:min-h-0">
              Usuń
            </Button>
          )}
          <Button variant="secondary" onClick={onClose} disabled={saving} className="min-h-11 sm:min-h-0">
            Anuluj
          </Button>
          <Button type="submit" disabled={saving || loadingSeries} className="min-h-11 sm:min-h-0">
            {saving ? 'Zapisywanie…' : item ? 'Zapisz' : 'Utwórz'}
          </Button>
        </>
      }
    >
      {isSeries && <ScopePicker legend="Zmień" value={scope} onChange={setScope} disabled={saving} />}

      <TextField
        label="Tytuł"
        value={draft.title}
        onChange={(e) => update({ title: e.target.value })}
        maxLength={TITLE_MAX}
        placeholder="np. Wykład z baz danych"
        autoComplete="off"
        error={showErrors && error === 'Podaj tytuł wydarzenia.' ? error : undefined}
        data-autofocus
      />

      <label className="flex cursor-pointer items-center gap-2 text-sm text-slate-800">
        <input type="checkbox" checked={draft.isAllDay} onChange={(e) => update({ isAllDay: e.target.checked })} className="size-4 accent-blue-600" />
        Cały dzień
      </label>

      <div className="grid grid-cols-2 gap-3">
        <TextField label="Początek" type="date" value={draft.startDate} onChange={(e) => moveStart({ startDate: e.target.value })} required />
        {!draft.isAllDay && <TextField label="Godzina" type="time" value={draft.startTime} onChange={(e) => moveStart({ startTime: e.target.value })} required />}
        <TextField label="Koniec" type="date" value={draft.endDate} min={draft.startDate} onChange={(e) => update({ endDate: e.target.value })} required={!draft.isAllDay} />
        {!draft.isAllDay && <TextField label="Godzina" type="time" value={draft.endTime} onChange={(e) => update({ endTime: e.target.value })} required />}
      </div>

      {recurrenceVisible && (
        <div className="grid grid-cols-2 gap-3">
          <SelectField
            label="Powtarzanie"
            options={RECURRENCE_OPTIONS}
            value={draft.recurrence}
            onChange={(e) => update({ recurrence: e.target.value as RecurrenceFrequency })}
            disabled={loadingSeries}
          />
          {draft.recurrence !== 'none' && (
            <TextField
              label="Powtarzaj do"
              type="date"
              value={draft.recurrenceUntil}
              min={draft.startDate}
              onChange={(e) => update({ recurrenceUntil: e.target.value })}
              hint="Puste = bez końca"
            />
          )}
        </div>
      )}
      {draft.recurrence === 'monthly' && recurrenceVisible && Number(draft.startDate.slice(8)) > 28 && (
        <p className="-mt-2 text-xs text-slate-500">Miesiące bez tego dnia (np. 31.) zostaną pominięte.</p>
      )}

      <TextAreaField label="Opis" value={draft.description} onChange={(e) => update({ description: e.target.value })} maxLength={DESCRIPTION_MAX} rows={2} />

      <TagColorPicker value={draft.color} onChange={(color) => update({ color })} />

      {showErrors && error && error !== 'Podaj tytuł wydarzenia.' && <Alert tone="error">{error}</Alert>}
      {saveError && <Alert tone="error">{saveError}</Alert>}
    </Dialog>
  )
}
