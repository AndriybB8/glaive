import { useEffect, useId, useRef, type SubmitEvent, type ReactNode } from 'react'
import { IconButton } from './IconButton'
import { CloseIcon } from './icons'

interface DialogProps {
  title: string
  description?: string
  onClose: () => void
  /** Gdy podane, treść i stopka są formularzem (Enter zatwierdza). */
  onSubmit?: (event: SubmitEvent<HTMLFormElement>) => void
  /** Przyciski w stopce: najpierw „Anuluj”, potem główna akcja. */
  footer: ReactNode
  /** Okno potwierdzenia (alertdialog): bez krzyżyka, opis jako konsekwencja akcji. */
  confirm?: boolean
  /** W trakcie zapisu Esc i krzyżyk nie zamykają okna. */
  busy?: boolean
  children?: ReactNode
}

/**
 * Okno modalne na natywnym <dialog>. Jest otwarte, dopóki jest zamontowane – rodzic renderuje je warunkowo.
 * Po otwarciu fokus trafia na element z atrybutem `data-autofocus`, po zamknięciu wraca tam, gdzie był.
 */
export function Dialog({ title, description, onClose, onSubmit, footer, confirm = false, busy = false, children }: DialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const titleId = useId()
  const descriptionId = useId()

  useEffect(() => {
    const dialog = dialogRef.current
    if (!dialog) return
    const previous = document.activeElement instanceof HTMLElement ? document.activeElement : null

    dialog.showModal()
    dialog.querySelector<HTMLElement>('[data-autofocus]')?.focus()

    return () => {
      dialog.close()
      previous?.focus()
    }
  }, [])

  const content = (
    <>
      <div className="flex items-center justify-between gap-3">
        <h2 id={titleId} className="text-lg font-semibold text-slate-900">
          {title}
        </h2>
        {!confirm && (
          <IconButton label="Zamknij" title="Zamknij (Esc)" touch onClick={onClose} disabled={busy} className="-mr-2">
            <CloseIcon />
          </IconButton>
        )}
      </div>
      {description && (
        <p id={descriptionId} className="-mt-2 text-sm text-slate-600">
          {description}
        </p>
      )}
      {children}
      <div className="mt-2 grid grid-cols-2 gap-2 sm:flex sm:justify-end">{footer}</div>
    </>
  )

  return (
    <dialog
      ref={dialogRef}
      role={confirm ? 'alertdialog' : undefined}
      aria-labelledby={titleId}
      aria-describedby={description ? descriptionId : undefined}
      onCancel={(event) => {
        event.preventDefault()
        if (!busy) onClose()
      }}
      className="m-auto w-[calc(100%-2rem)] max-w-md rounded-xl bg-white p-5 text-slate-900 shadow-xl backdrop:bg-slate-900/40 sm:p-6"
    >
      {onSubmit ? (
        <form noValidate onSubmit={onSubmit} className="flex flex-col gap-4">
          {content}
        </form>
      ) : (
        <div className="flex flex-col gap-4">{content}</div>
      )}
    </dialog>
  )
}
