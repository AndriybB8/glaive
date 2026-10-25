import { useState } from 'react'
import { Alert } from '../../components/ui/Alert'
import { Button } from '../../components/ui/Button'
import { Dialog } from '../../components/ui/Dialog'
import { ApiError } from '../../lib/http'
import { deleteSchedule } from './api'
import type { Schedule } from './types'

interface DeleteScheduleDialogProps {
  schedule: Schedule
  onClose: () => void
  onDeleted: (id: string) => void
}

export function DeleteScheduleDialog({ schedule, onClose, onDeleted }: DeleteScheduleDialogProps) {
  const [deleting, setDeleting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleDelete = async () => {
    setDeleting(true)
    setError(null)
    try {
      await deleteSchedule(schedule.id)
      onDeleted(schedule.id)
    } catch (err) {
      // Już usunięty (np. w drugiej karcie) – efekt jest ten sam.
      if (err instanceof ApiError && err.status === 404) {
        onDeleted(schedule.id)
        return
      }
      setError('Nie udało się usunąć harmonogramu.')
      setDeleting(false)
    }
  }

  return (
    <Dialog
      confirm
      title={`Usunąć harmonogram „${schedule.name}”?`}
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
