import { useState } from 'react'
import { Alert } from '../../components/ui/Alert'
import { Button } from '../../components/ui/Button'
import { Dialog } from '../../components/ui/Dialog'
import { ApiError } from '../../lib/http'
import { deleteTask } from './api'
import type { Task } from './types'

interface DeleteTaskDialogProps {
  task: Task
  onClose: () => void
  onDeleted: (id: string) => void
}

export function DeleteTaskDialog({ task, onClose, onDeleted }: DeleteTaskDialogProps) {
  const [deleting, setDeleting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleDelete = async () => {
    setDeleting(true)
    setError(null)
    try {
      await deleteTask(task.id)
      onDeleted(task.id)
    } catch (err) {
      // Już usunięte (np. w drugiej karcie) – efekt jest ten sam.
      if (err instanceof ApiError && err.status === 404) {
        onDeleted(task.id)
        return
      }
      setError('Nie udało się usunąć zadania.')
      setDeleting(false)
    }
  }

  return (
    <Dialog
      confirm
      title={`Usunąć zadanie „${task.title}”?`}
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
            {deleting ? 'Usuwanie…' : 'Usuń'}
          </Button>
        </>
      }
    >
      {error && <Alert tone="error">{error}</Alert>}
    </Dialog>
  )
}
