import { useState } from 'react'
import { Alert } from '../../components/ui/Alert'
import { Button } from '../../components/ui/Button'
import { Dialog } from '../../components/ui/Dialog'
import { getCurrentUser } from '../../lib/authToken'
import { ApiError } from '../../lib/http'

interface LeaveDialogProps {
  /** Np. „Opuścić harmonogram „Sesja zimowa”?” */
  title: string
  /** Np. „Opuść harmonogram” – przycisk potwierdzenia. */
  confirmLabel: string
  /** Usuwa wpis bieżącego użytkownika (DELETE …/members/{własne id}). */
  leave: (userId: string) => Promise<void>
  onClose: () => void
  onLeft: () => void
}

/** Potwierdzenie opuszczenia cudzego, udostępnionego zasobu. */
export function LeaveDialog({ title, confirmLabel, leave, onClose, onLeft }: LeaveDialogProps) {
  const [leaving, setLeaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleLeave = async () => {
    const me = getCurrentUser()
    if (!me) {
      setError('Nie udało się odczytać Twojego konta. Zaloguj się ponownie.')
      return
    }

    setLeaving(true)
    setError(null)
    try {
      await leave(me.id)
      onLeft()
    } catch (err) {
      // Dostęp już odebrany albo zasób usunięty – efekt jest ten sam.
      if (err instanceof ApiError && err.status === 404) {
        onLeft()
        return
      }
      setError('Nie udało się zrezygnować z dostępu.')
      setLeaving(false)
    }
  }

  return (
    <Dialog
      confirm
      title={title}
      description="Stracisz dostęp. Właściciel może zaprosić Cię ponownie."
      onClose={onClose}
      busy={leaving}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={leaving} className="min-h-11 sm:min-h-0" data-autofocus>
            Anuluj
          </Button>
          <Button variant="danger" onClick={() => void handleLeave()} disabled={leaving} className="min-h-11 sm:min-h-0">
            {leaving ? 'Opuszczanie…' : confirmLabel}
          </Button>
        </>
      }
    >
      {error && <Alert tone="error">{error}</Alert>}
    </Dialog>
  )
}
