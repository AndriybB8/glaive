import { useState } from 'react'
import { Alert } from '../../components/ui/Alert'
import { Button } from '../../components/ui/Button'
import { Dialog } from '../../components/ui/Dialog'
import { ApiError } from '../../lib/http'
import { deleteNoteGroup } from './groupsApi'
import type { NoteGroup } from './types'

interface DeleteNoteGroupDialogProps {
  group: NoteGroup
  /** Ile notatek jest w grupie – trafią do „Bez grupy”. */
  noteCount: number
  onClose: () => void
  onDeleted: (id: string) => void
}

function describeNotes(count: number): string {
  if (count === 0) return 'Grupa jest pusta.'
  if (count === 1) return 'Jej jedyna notatka nie zostanie usunięta – trafi do „Bez grupy”.'
  return `Jej notatki (${count}) nie zostaną usunięte – trafią do „Bez grupy”.`
}

export function DeleteNoteGroupDialog({ group, noteCount, onClose, onDeleted }: DeleteNoteGroupDialogProps) {
  const [deleting, setDeleting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleDelete = async () => {
    setDeleting(true)
    setError(null)
    try {
      await deleteNoteGroup(group.id)
      onDeleted(group.id)
    } catch (err) {
      // Już usunięta (np. w drugiej karcie) – efekt jest ten sam.
      if (err instanceof ApiError && err.status === 404) {
        onDeleted(group.id)
        return
      }
      setError('Nie udało się usunąć grupy.')
      setDeleting(false)
    }
  }

  return (
    <Dialog
      confirm
      title={`Usunąć grupę „${group.name}”?`}
      description={describeNotes(noteCount)}
      onClose={onClose}
      busy={deleting}
      footer={
        <>
          {/* Fokus na „Anuluj”, żeby Enter nie usuwał przypadkiem. */}
          <Button variant="secondary" onClick={onClose} disabled={deleting} className="min-h-11 sm:min-h-0" data-autofocus>
            Anuluj
          </Button>
          <Button variant="danger" onClick={() => void handleDelete()} disabled={deleting} className="min-h-11 sm:min-h-0">
            {deleting ? 'Usuwanie…' : 'Usuń grupę'}
          </Button>
        </>
      }
    >
      {error && <Alert tone="error">{error}</Alert>}
    </Dialog>
  )
}
