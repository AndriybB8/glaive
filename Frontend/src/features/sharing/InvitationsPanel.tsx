import { useId, useState } from 'react'
import { Button } from '../../components/ui/Button'
import { ApiError } from '../../lib/http'
import { ACCESS_LABELS } from './sharing'
import type { Invitation } from './types'

interface InvitationsPanelProps {
  invitations: Invitation[]
  accept: (id: string) => Promise<void>
  decline: (id: string) => Promise<void>
  /** Zaproszenie przyjęte – zasób jest już dostępny. */
  onAccepted: (invitation: Invitation) => void
  /** Zaproszenie odrzucone albo już nieaktualne – znika z panelu. */
  onRemoved: (id: string) => void
  className?: string
}

/** Zaproszenia do cudzych zasobów (np. harmonogramów): przyjęcie albo odrzucenie. Nic nie pokazuje, gdy ich nie ma. */
export function InvitationsPanel({ invitations, accept, decline, onAccepted, onRemoved, className = '' }: InvitationsPanelProps) {
  const headingId = useId()
  const [busyId, setBusyId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  if (invitations.length === 0) return null

  const respond = async (invitation: Invitation, accepting: boolean) => {
    setBusyId(invitation.id)
    setError(null)
    try {
      if (accepting) {
        await accept(invitation.id)
        onAccepted(invitation)
      } else {
        await decline(invitation.id)
        onRemoved(invitation.id)
      }
    } catch (err) {
      if (err instanceof ApiError && err.status === 404) {
        // Właściciel cofnął zaproszenie albo usunął zasób.
        setError(`Zaproszenie do „${invitation.title}” jest już nieaktualne.`)
        onRemoved(invitation.id)
      } else {
        setError(accepting ? 'Nie udało się przyjąć zaproszenia.' : 'Nie udało się odrzucić zaproszenia.')
      }
    } finally {
      setBusyId(null)
    }
  }

  return (
    <section aria-labelledby={headingId} className={`rounded-lg border border-blue-200 bg-blue-50 p-3 ${className}`}>
      <h2 id={headingId} className="text-sm font-semibold text-slate-900">
        Zaproszenia ({invitations.length})
      </h2>
      {error && (
        <p role="alert" className="mt-2 text-sm text-red-800">
          {error}
        </p>
      )}
      <ul className="mt-2 flex flex-col gap-3">
        {invitations.map((invitation) => {
          const busy = busyId === invitation.id
          return (
            <li key={invitation.id} className="flex flex-col gap-2">
              <p className="text-sm text-slate-700">
                <span className="font-medium break-words text-slate-900">„{invitation.title}”</span>
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
