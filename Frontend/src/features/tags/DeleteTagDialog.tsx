import { useState } from 'react'
import { Alert } from '../../components/ui/Alert'
import { Button } from '../../components/ui/Button'
import { Dialog } from '../../components/ui/Dialog'
import { ApiError } from '../../lib/http'
import { deleteTag } from './api'
import type { Tag } from './types'

interface DeleteTagDialogProps {
  tag: Tag
  onClose: () => void
  onDeleted: (id: string) => void
}

export function DeleteTagDialog({ tag, onClose, onDeleted }: DeleteTagDialogProps) {
  const [deleting, setDeleting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleDelete = async () => {
    setDeleting(true)
    setError(null)
    try {
      await deleteTag(tag.id)
      onDeleted(tag.id)
    } catch (err) {
      // Już usunięty (np. w drugiej karcie) – efekt jest ten sam.
      if (err instanceof ApiError && err.status === 404) {
        onDeleted(tag.id)
        return
      }
      setError('Nie udało się usunąć tagu.')
      setDeleting(false)
    }
  }

  return (
    <Dialog
      confirm
      title={`Usunąć tag „${tag.name}”?`}
      description="Tej operacji nie można cofnąć."
      onClose={onClose}
      busy={deleting}
      footer={
        <>
          {/* Fokus na „Anuluj”, żeby Enter nie usuwał przypadkiem. */}
          <Button variant="secondary" onClick={onClose} disabled={deleting} className="min-h-11 sm:min-h-0" data-autofocus>
            Anuluj
          </Button>
          <Button variant="danger" onClick={() => void handleDelete()} disabled={deleting} className="min-h-11 sm:min-h-0">
            {deleting ? 'Usuwanie…' : 'Usuń tag'}
          </Button>
        </>
      }
    >
      {error && <Alert tone="error">{error}</Alert>}
    </Dialog>
  )
}
