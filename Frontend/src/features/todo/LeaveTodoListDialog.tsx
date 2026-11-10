import { useState } from 'react'
import { Alert } from '../../components/ui/Alert'
import { Button } from '../../components/ui/Button'
import { Dialog } from '../../components/ui/Dialog'
import { getCurrentUser } from '../../lib/authToken'
import { ApiError } from '../../lib/http'
import { removeTodoListMember } from './sharingApi'
import type { TodoListSummary } from './types'

interface LeaveTodoListDialogProps {
  list: TodoListSummary
  onClose: () => void
  onLeft: (id: string) => void
}

/** Potwierdzenie opuszczenia cudzej, udostępnionej listy. */
export function LeaveTodoListDialog({ list, onClose, onLeft }: LeaveTodoListDialogProps) {
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
      await removeTodoListMember(list.id, me.id)
      onLeft(list.id)
    } catch (err) {
      // Dostęp już odebrany albo lista usunięta – efekt jest ten sam.
      if (err instanceof ApiError && err.status === 404) {
        onLeft(list.id)
        return
      }
      setError('Nie udało się opuścić listy.')
      setLeaving(false)
    }
  }

  return (
    <Dialog
      confirm
      title={`Opuścić listę „${list.title}”?`}
      description="Stracisz dostęp do tej listy. Właściciel może zaprosić Cię ponownie."
      onClose={onClose}
      busy={leaving}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={leaving} className="min-h-11 sm:min-h-0" data-autofocus>
            Anuluj
          </Button>
          <Button variant="danger" onClick={() => void handleLeave()} disabled={leaving} className="min-h-11 sm:min-h-0">
            {leaving ? 'Opuszczanie…' : 'Opuść listę'}
          </Button>
        </>
      }
    >
      {error && <Alert tone="error">{error}</Alert>}
    </Dialog>
  )
}
