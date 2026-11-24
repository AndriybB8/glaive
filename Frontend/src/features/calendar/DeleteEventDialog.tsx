import { useState } from 'react'
import { Alert } from '../../components/ui/Alert'
import { Button } from '../../components/ui/Button'
import { Dialog } from '../../components/ui/Dialog'
import { ApiError } from '../../lib/http'
import { deleteCalendarEvent } from './api'
import { ScopePicker } from './ScopePicker'
import type { CalendarItem, EditScope } from './types'

interface DeleteEventDialogProps {
  item: CalendarItem
  onClose: () => void
  onDeleted: () => void
}

export function DeleteEventDialog({ item, onClose, onDeleted }: DeleteEventDialogProps) {
  const [scope, setScope] = useState<EditScope>('single')
  const [deleting, setDeleting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleDelete = async () => {
    setDeleting(true)
    setError(null)
    try {
      if (item.isRecurring) await deleteCalendarEvent(item.id, scope, item.occurrenceDate)
      else await deleteCalendarEvent(item.id)
      onDeleted()
    } catch (err) {
      // Już usunięte (np. w drugiej karcie) – efekt jest ten sam.
      if (err instanceof ApiError && err.status === 404) {
        onDeleted()
        return
      }
      setError(err instanceof ApiError && err.status === 400 ? err.message : 'Nie udało się usunąć wydarzenia.')
      setDeleting(false)
    }
  }

  return (
    <Dialog
      confirm
      title={`Usunąć wydarzenie „${item.title}”?`}
      description={item.isRecurring ? 'To wydarzenie się powtarza.' : 'Tej operacji nie można cofnąć.'}
      onClose={onClose}
      busy={deleting}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={deleting} className="min-h-11 sm:min-h-0" data-autofocus>
            Anuluj
          </Button>
          <Button variant="danger" onClick={() => void handleDelete()} disabled={deleting} className="min-h-11 sm:min-h-0">
            {deleting ? 'Usuwanie…' : 'Usuń'}
          </Button>
        </>
      }
    >
      {item.isRecurring && <ScopePicker legend="Usuń" value={scope} onChange={setScope} disabled={deleting} />}
      {error && <Alert tone="error">{error}</Alert>}
    </Dialog>
  )
}
