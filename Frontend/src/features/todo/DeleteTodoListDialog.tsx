import { useState } from 'react'
import { Alert } from '../../components/ui/Alert'
import { Button } from '../../components/ui/Button'
import { Dialog } from '../../components/ui/Dialog'
import { ApiError } from '../../lib/http'
import { deleteTodoList } from './api'
import type { TodoListSummary } from './types'

interface DeleteTodoListDialogProps {
  list: TodoListSummary
  onClose: () => void
  onDeleted: (id: string) => void
}

function describeItems(count: number): string {
  if (count === 0) return 'Lista jest pusta. Tej operacji nie można cofnąć.'
  if (count === 1) return 'Razem z nią zostanie usunięty 1 punkt. Tej operacji nie można cofnąć.'
  return `Razem z nią zostaną usunięte wszystkie punkty (${count}). Tej operacji nie można cofnąć.`
}

export function DeleteTodoListDialog({ list, onClose, onDeleted }: DeleteTodoListDialogProps) {
  const [deleting, setDeleting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleDelete = async () => {
    setDeleting(true)
    setError(null)
    try {
      await deleteTodoList(list.id)
      onDeleted(list.id)
    } catch (err) {
      // Już usunięta (np. w drugiej karcie) – efekt jest ten sam.
      if (err instanceof ApiError && err.status === 404) {
        onDeleted(list.id)
        return
      }
      setError('Nie udało się usunąć listy.')
      setDeleting(false)
    }
  }

  return (
    <Dialog
      confirm
      title={`Usunąć listę „${list.title}”?`}
      description={describeItems(list.itemCount)}
      onClose={onClose}
      busy={deleting}
      footer={
        <>
          {/* Fokus na „Anuluj”, żeby Enter nie usuwał przypadkiem. */}
          <Button variant="secondary" onClick={onClose} disabled={deleting} className="min-h-11 sm:min-h-0" data-autofocus>
            Anuluj
          </Button>
          <Button variant="danger" onClick={() => void handleDelete()} disabled={deleting} className="min-h-11 sm:min-h-0">
            {deleting ? 'Usuwanie…' : 'Usuń listę'}
          </Button>
        </>
      }
    >
      {error && <Alert tone="error">{error}</Alert>}
    </Dialog>
  )
}
