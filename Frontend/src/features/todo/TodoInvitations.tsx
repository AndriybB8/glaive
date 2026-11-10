import { useState } from 'react'
import { Button } from '../../components/ui/Button'
import { ApiError } from '../../lib/http'
import { ACCESS_LABELS, acceptTodoListInvitation, declineTodoListInvitation } from './sharingApi'
import type { TodoListInvitation } from './types'

interface TodoInvitationsProps {
  invitations: TodoListInvitation[]
  /** Zaproszenie przyjęte – lista jest już dostępna. */
  onAccepted: (invitation: TodoListInvitation) => void
  /** Zaproszenie odrzucone albo już nieaktualne – znika z panelu. */
  onRemoved: (listId: string) => void
}

/** Zaproszenia do cudzych list nad panelem list: przyjęcie albo odrzucenie. Nic nie pokazuje, gdy ich nie ma. */
export function TodoInvitations({ invitations, onAccepted, onRemoved }: TodoInvitationsProps) {
  const [busyId, setBusyId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  if (invitations.length === 0) return null

  const respond = async (invitation: TodoListInvitation, accept: boolean) => {
    setBusyId(invitation.listId)
    setError(null)
    try {
      if (accept) {
        await acceptTodoListInvitation(invitation.listId)
        onAccepted(invitation)
      } else {
        await declineTodoListInvitation(invitation.listId)
        onRemoved(invitation.listId)
      }
    } catch (err) {
      if (err instanceof ApiError && err.status === 404) {
        // Właściciel cofnął zaproszenie albo usunął listę.
        setError(`Zaproszenie do „${invitation.listTitle}” jest już nieaktualne.`)
        onRemoved(invitation.listId)
      } else {
        setError(accept ? 'Nie udało się przyjąć zaproszenia.' : 'Nie udało się odrzucić zaproszenia.')
      }
    } finally {
      setBusyId(null)
    }
  }

  return (
    <section aria-labelledby="todo-invitations-heading" className="mx-2 mb-3 rounded-lg border border-blue-200 bg-blue-50 p-3">
      <h2 id="todo-invitations-heading" className="text-sm font-semibold text-slate-900">
        Zaproszenia ({invitations.length})
      </h2>
      {error && (
        <p role="alert" className="mt-2 text-sm text-red-800">
          {error}
        </p>
      )}
      <ul className="mt-2 flex flex-col gap-3">
        {invitations.map((invitation) => {
          const busy = busyId === invitation.listId
          return (
            <li key={invitation.listId} className="flex flex-col gap-2">
              <p className="text-sm text-slate-700">
                <span className="font-medium break-words text-slate-900">„{invitation.listTitle}”</span>
                <br />
                <span className="break-all">od {invitation.ownerEmail}</span> · {ACCESS_LABELS[invitation.role]}
              </p>
              <div className="flex gap-2">
                <Button onClick={() => void respond(invitation, true)} disabled={busy} className="min-h-11 flex-1 sm:min-h-8">
                  Przyjmij
                </Button>
                <Button variant="secondary" onClick={() => void respond(invitation, false)} disabled={busy} className="min-h-11 flex-1 sm:min-h-8">
                  Odrzuć
                </Button>
              </div>
            </li>
          )
        })}
      </ul>
    </section>
  )
}
