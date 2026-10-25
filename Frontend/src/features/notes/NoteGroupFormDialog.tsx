import { useState, type SubmitEvent } from 'react'
import { Alert } from '../../components/ui/Alert'
import { Button } from '../../components/ui/Button'
import { Dialog } from '../../components/ui/Dialog'
import { TextField } from '../../components/ui/TextField'
import { ApiError } from '../../lib/http'
import { createNoteGroup, isNoteGroupConflict, renameNoteGroup } from './groupsApi'
import type { NoteGroup } from './types'

/** Ten sam limit sprawdza backend (NoteGroup.NameMaxLength). */
const NAME_MAX_LENGTH = 100

interface NoteGroupFormDialogProps {
  /** Brak = tworzenie nowej grupy. */
  group?: NoteGroup
  /** Pozostałe grupy – żeby od razu podpowiedzieć, że nazwa jest zajęta. */
  existingGroups: readonly NoteGroup[]
  onClose: () => void
  onSaved: (group: NoteGroup) => void
  /** Serwer zgłosił, że grupę zmieniono lub usunięto w innym miejscu – panel grup powinien to odzwierciedlić. */
  onChangedElsewhere: (current: NoteGroup | { deletedId: string }) => void
}

export function NoteGroupFormDialog({ group, existingGroups, onClose, onSaved, onChangedElsewhere }: NoteGroupFormDialogProps) {
  const [name, setName] = useState(group?.name ?? '')
  const [version, setVersion] = useState(group?.version ?? 0)
  /** Błędy pokazujemy dopiero po pierwszej próbie zapisu, potem na bieżąco. */
  const [showErrors, setShowErrors] = useState(false)
  const [saving, setSaving] = useState(false)
  const [saveError, setSaveError] = useState<string | null>(null)

  // Te same reguły sprawdza backend (NoteGroupService); tu tylko po to, żeby nie czekać na serwer.
  const trimmed = name.trim()
  const nameTaken = existingGroups.some(
    (g) => g.id !== group?.id && g.name.localeCompare(trimmed, undefined, { sensitivity: 'accent' }) === 0,
  )
  const nameError = !trimmed ? 'Podaj nazwę grupy.' : nameTaken ? 'Grupa o tej nazwie już istnieje.' : undefined

  const handleSubmit = async (event: SubmitEvent<HTMLFormElement>) => {
    event.preventDefault()
    setShowErrors(true)
    if (nameError) return

    setSaving(true)
    setSaveError(null)
    try {
      const saved = group ? await renameNoteGroup(group.id, trimmed, version) : await createNoteGroup(trimmed)
      onSaved(saved)
    } catch (error) {
      if (isNoteGroupConflict(error)) {
        // Nazwa jest jedynym polem, więc wystarczy pokazać, co jest na serwerze; kolejne „Zapisz” ją nadpisze.
        setVersion(error.current.version)
        setSaveError(`W międzyczasie nazwę zmieniono na „${error.current.name}”. Zapisz ponownie, aby ją nadpisać.`)
        onChangedElsewhere(error.current)
      } else if (group && error instanceof ApiError && error.status === 404) {
        setSaveError('Ta grupa została w międzyczasie usunięta.')
        onChangedElsewhere({ deletedId: group.id })
      } else if (error instanceof ApiError && error.status === 400) {
        setSaveError(error.message)
      } else {
        setSaveError('Nie udało się zapisać grupy. Spróbuj ponownie.')
      }
      setSaving(false)
    }
  }

  return (
    <Dialog
      title={group ? 'Zmień nazwę grupy' : 'Nowa grupa'}
      onClose={onClose}
      onSubmit={(event) => void handleSubmit(event)}
      busy={saving}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={saving} className="min-h-11 sm:min-h-0">
            Anuluj
          </Button>
          <Button type="submit" disabled={saving} className="min-h-11 sm:min-h-0">
            {saving ? 'Zapisywanie…' : group ? 'Zapisz' : 'Utwórz'}
          </Button>
        </>
      }
    >
      <TextField
        label="Nazwa"
        value={name}
        onChange={(event) => setName(event.target.value)}
        maxLength={NAME_MAX_LENGTH}
        placeholder="np. Matematyka"
        autoComplete="off"
        error={showErrors ? nameError : undefined}
        data-autofocus
      />
      {saveError && <Alert tone="error">{saveError}</Alert>}
    </Dialog>
  )
}
