import { IconButton } from '../../components/ui/IconButton'
import { CloseIcon } from '../../components/ui/icons'

export interface LiveToast {
  id: number
  /** Kto zmienił (adres email). */
  actor: string
  /** Co zrobił, np. „dodaje punkt „Mleko””. */
  text: string
}

interface LiveToastsProps {
  toasts: readonly LiveToast[]
  onDismiss: (id: number) => void
}

/**
 * Powiadomienia o zmianach innych osób (prawy dolny róg, na telefonie – dół ekranu).
 * Region aria-live: czytnik ekranu odczyta nowe powiadomienie, nie przerywając pracy.
 */
export function LiveToasts({ toasts, onDismiss }: LiveToastsProps) {
  return (
    <div
      aria-live="polite"
      aria-label="Zmiany innych osób"
      className="pointer-events-none fixed inset-x-3 bottom-[max(0.75rem,env(safe-area-inset-bottom))] z-40 flex flex-col items-stretch gap-2 sm:inset-x-auto sm:right-4 sm:bottom-4 sm:w-80"
    >
      {toasts.map((toast) => (
        <div
          key={toast.id}
          role="status"
          className="pointer-events-auto flex items-start gap-2 rounded-lg border border-slate-200 bg-white py-2.5 pr-1.5 pl-3.5 text-sm shadow-lg"
        >
          <p className="min-w-0 flex-1 py-0.5 break-words text-slate-700">
            <span className="font-medium text-slate-900">{toast.actor}</span> {toast.text}
          </p>
          <IconButton label="Zamknij powiadomienie" title="Zamknij" onClick={() => onDismiss(toast.id)}>
            <CloseIcon />
          </IconButton>
        </div>
      ))}
    </div>
  )
}
