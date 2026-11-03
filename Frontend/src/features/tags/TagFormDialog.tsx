import { useState, type SubmitEvent } from 'react'
import { Alert } from '../../components/ui/Alert'
import { Button } from '../../components/ui/Button'
import { Dialog } from '../../components/ui/Dialog'
import { TextField } from '../../components/ui/TextField'
import { ApiError } from '../../lib/http'
import { createTag, isTagConflict, updateTag } from './api'
import { TagChip } from './TagChip'
import { TagColorPicker } from './TagColorPicker'
import { DEFAULT_TAG_COLOR, isValidTagColor } from './tagColors'
import type { Tag } from './types'

/** Ten sam limit sprawdza backend (Tag.NameMaxLength). */
const NAME_MAX_LENGTH = 50

interface TagFormDialogProps {
  /** Brak = tworzenie nowego tagu. */
  tag?: Tag
  /** Pozostałe tagi – żeby od razu podpowiedzieć, że nazwa jest zajęta. */
  existingTags: readonly Tag[]
  onClose: () => void
  onSaved: (tag: Tag) => void
  /** Serwer zgłosił, że tag zmieniono lub usunięto w innym miejscu – lista powinna to odzwierciedlić. */
  onChangedElsewhere: (current: Tag | { deletedId: string }) => void
}

export function TagFormDialog({ tag, existingTags, onClose, onSaved, onChangedElsewhere }: TagFormDialogProps) {
  const [name, setName] = useState(tag?.name ?? '')
  const [color, setColor] = useState(tag?.color ?? DEFAULT_TAG_COLOR)
  const [version, setVersion] = useState(tag?.version ?? 0)
  /** Błędy pokazujemy dopiero po pierwszej próbie zapisu, potem na bieżąco. */
  const [showErrors, setShowErrors] = useState(false)
  const [saving, setSaving] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)

  // Te same reguły sprawdza backend (TagService); tu tylko po to, żeby nie czekać na serwer.
  const trimmed = name.trim()
  const nameTaken = existingTags.some(
    (t) => t.id !== tag?.id && t.name.localeCompare(trimmed, undefined, { sensitivity: 'accent' }) === 0,
  )
  const nameError = !trimmed ? 'Podaj nazwę tagu.' : nameTaken ? 'Tag o tej nazwie już istnieje.' : undefined
  const colorError = isValidTagColor(color) ? undefined : 'Wybierz kolor.'

  const handleSubmit = async (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault()
    setShowErrors(true)
    if (nameError || colorError) return

    setSaving(true)
    setSaveError(null)
    try {
      const draft = { name: trimmed, color }
      const saved = tag ? await updateTag(tag.id, draft, version) : await createTag(draft)
      onSaved(saved)
    } catch (error) {
      if (isTagConflict(error)) {
        // Pokazujemy, co jest na serwerze; kolejne „Zapisz” nadpisze to wartościami z formularza.
        setVersion(error.current.version)
        setSaveError(`W międzyczasie ten tag zmieniono (teraz: „${error.current.name}”). Zapisz ponownie, aby go nadpisać.`)
        onChangedElsewhere(error.current)
      } else if (tag && error instanceof ApiError && error.status === 404) {
        setSaveError('Ten tag został w międzyczasie usunięty.')
        onChangedElsewhere({ deletedId: tag.id })
      } else if (error instanceof ApiError && error.status === 400) {
        setSaveError(error.message)
      } else {
        setSaveError('Nie udało się zapisać tagu. Spróbuj ponownie.')
      }
      setSaving(false)
    }
  }

  return (
    <Dialog
      title={tag ? 'Edytuj tag' : 'Nowy tag'}
      onClose={onClose}
      onSubmit={(event) => void handleSubmit(event)}
      busy={saving}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={saving} className="min-h-11 sm:min-h-0">
            Anuluj
          </Button>
          <Button type="submit" disabled={saving} className="min-h-11 sm:min-h-0">
            {saving ? 'Zapisywanie…' : tag ? 'Zapisz' : 'Utwórz'}
          </Button>
        </>
      }
    >
      <TextField
        label="Nazwa"
        value={name}
        onChange={(event) => setName(event.target.value)}
        maxLength={NAME_MAX_LENGTH}
        placeholder="np. Kolokwium"
        autoComplete="off"
        error={showErrors ? nameError : undefined}
        data-autofocus
      />

      <TagColorPicker value={color} onChange={setColor} />
      {showErrors && colorError && <p className="text-sm text-red-700">{colorError}</p>}

      <div className="flex min-w-0 flex-col gap-1.5">
        <span className="text-sm font-medium text-slate-700">Podgląd</span>
        <div className="flex min-h-10 items-center rounded-md border border-dashed border-slate-300 px-3 py-2">
          <TagChip name={trimmed || 'Nazwa tagu'} color={isValidTagColor(color) ? color : DEFAULT_TAG_COLOR} />
        </div>
      </div>

      {saveError && <Alert tone="error">{saveError}</Alert>}
    </Dialog>
  )
}
