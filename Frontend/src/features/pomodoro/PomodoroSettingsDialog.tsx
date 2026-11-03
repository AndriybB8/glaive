import { useState, type SubmitEvent } from 'react'
import { Alert } from '../../components/ui/Alert'
import { Button } from '../../components/ui/Button'
import { Dialog } from '../../components/ui/Dialog'
import { TextField } from '../../components/ui/TextField'
import { ApiError } from '../../lib/http'
import { usePomodoro } from './pomodoroContext'
import { DEFAULT_POMODORO_SETTINGS, POMODORO_LIMITS, PHASE_LABELS, type PomodoroSettings } from './pomodoroTimer'

type Field = keyof PomodoroSettings
type Draft = Record<Field, string>

const FIELDS: { key: Field; label: string; unit: string; missing: string }[] = [
  { key: 'workMinutes', label: 'Czas pracy', unit: 'min', missing: 'Podaj czas pracy.' },
  { key: 'shortBreakMinutes', label: 'Krótka przerwa', unit: 'min', missing: 'Podaj czas krótkiej przerwy.' },
  { key: 'longBreakMinutes', label: 'Długa przerwa', unit: 'min', missing: 'Podaj czas długiej przerwy.' },
  { key: 'sessionsBeforeLongBreak', label: 'Sesje pracy w cyklu', unit: '', missing: 'Podaj liczbę sesji.' },
]

function toDraft(settings: PomodoroSettings): Draft {
  return {
    workMinutes: String(settings.workMinutes),
    shortBreakMinutes: String(settings.shortBreakMinutes),
    longBreakMinutes: String(settings.longBreakMinutes),
    sessionsBeforeLongBreak: String(settings.sessionsBeforeLongBreak),
  }
}

function rangeText(key: Field, unit: string): string {
  const { min, max } = POMODORO_LIMITS[key]
  return unit ? `Od ${min} do ${max} ${unit}.` : `Od ${min} do ${max}.`
}

/** Te same reguły sprawdza backend (PomodoroSettingsService); tu tylko po to, żeby nie czekać na serwer. */
function validate(draft: Draft): Partial<Record<Field, string>> {
  const errors: Partial<Record<Field, string>> = {}
  for (const { key, unit, missing } of FIELDS) {
    const raw = draft[key].trim()
    const value = Number(raw)
    const { min, max } = POMODORO_LIMITS[key]
    if (!raw) errors[key] = missing
    else if (!Number.isInteger(value)) errors[key] = 'Podaj liczbę całkowitą.'
    else if (value < min || value > max) errors[key] = rangeText(key, unit)
  }
  return errors
}

const defaultsText = `${DEFAULT_POMODORO_SETTINGS.workMinutes} / ${DEFAULT_POMODORO_SETTINGS.shortBreakMinutes} / ${DEFAULT_POMODORO_SETTINGS.longBreakMinutes} min, ${DEFAULT_POMODORO_SETTINGS.sessionsBeforeLongBreak} sesje`

export function PomodoroSettingsDialog({ onClose }: { onClose: () => void }) {
  const { settings, saveSettings, atPhaseStart, state } = usePomodoro()
  const [draft, setDraft] = useState<Draft>(() => toDraft(settings))
  /** Błędy pokazujemy dopiero po pierwszej próbie zapisu, potem na bieżąco. */
  const [showErrors, setShowErrors] = useState(false)
  const [saving, setSaving] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)

  const errors = validate(draft)
  const hasErrors = Object.keys(errors).length > 0

  const handleSubmit = async (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault()
    setShowErrors(true)
    if (hasErrors) return

    setSaving(true)
    setSaveError(null)
    try {
      await saveSettings({
        workMinutes: Number(draft.workMinutes),
        shortBreakMinutes: Number(draft.shortBreakMinutes),
        longBreakMinutes: Number(draft.longBreakMinutes),
        sessionsBeforeLongBreak: Number(draft.sessionsBeforeLongBreak),
      })
      onClose()
    } catch (error) {
      setSaveError(
        error instanceof ApiError && error.status === 400 ? error.message : 'Nie udało się zapisać ustawień. Spróbuj ponownie.',
      )
      setSaving(false)
    }
  }

  return (
    <Dialog
      title="Czasy cyklu"
      description="Ustawienia zapisują się na Twoim koncie, więc działają na każdym urządzeniu."
      onClose={onClose}
      onSubmit={(event) => void handleSubmit(event)}
      busy={saving}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={saving} className="min-h-11 sm:min-h-0">
            Anuluj
          </Button>
          <Button type="submit" disabled={saving} className="min-h-11 sm:min-h-0">
            {saving ? 'Zapisywanie…' : 'Zapisz'}
          </Button>
        </>
      }
    >
      <div className="grid gap-4 sm:grid-cols-2">
        {FIELDS.map(({ key, label, unit }, index) => (
          <TextField
            key={key}
            label={unit ? `${label} (${unit})` : label}
            type="number"
            inputMode="numeric"
            min={POMODORO_LIMITS[key].min}
            max={POMODORO_LIMITS[key].max}
            step={1}
            value={draft[key]}
            onChange={(event) => setDraft((current) => ({ ...current, [key]: event.target.value }))}
            hint={rangeText(key, unit)}
            error={showErrors ? errors[key] : undefined}
            data-autofocus={index === 0 ? true : undefined}
          />
        ))}
      </div>

      <Button
        variant="ghost"
        onClick={() => setDraft(toDraft(DEFAULT_POMODORO_SETTINGS))}
        disabled={saving}
        className="min-h-11 self-start sm:min-h-0"
      >
        Przywróć domyślne ({defaultsText})
      </Button>

      {!atPhaseStart && (
        <p className="rounded-md bg-slate-50 px-3 py-2 text-sm text-slate-600">
          Trwa faza „{PHASE_LABELS[state.phase]}”. Nowy czas obejmie też ją, a czas, który już upłynął, się liczy. Jeśli nowy czas
          jest krótszy od tego, co już minęło, faza skończy się od razu.
        </p>
      )}

      {saveError && <Alert tone="error">{saveError}</Alert>}
    </Dialog>
  )
}
