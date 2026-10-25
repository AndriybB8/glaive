import type { ReactNode } from 'react'

const toneClasses = {
  error: 'rounded-md bg-red-50 px-3 py-2 text-red-800',
  warning: 'rounded-lg border border-amber-300 bg-amber-50 p-4 text-amber-900',
}

interface AlertProps {
  /** `error` – nieudana akcja, `warning` – sytuacja, którą użytkownik musi rozstrzygnąć (np. konflikt wersji). */
  tone: keyof typeof toneClasses
  title?: string
  actions?: ReactNode
  children: ReactNode
}

export function Alert({ tone, title, actions, children }: AlertProps) {
  return (
    <div role="alert" className={`text-sm ${toneClasses[tone]}`}>
      {title && <p className="font-medium">{title}</p>}
      <div className={title ? 'mt-1' : undefined}>{children}</div>
      {actions && <div className="mt-3 flex flex-wrap gap-2">{actions}</div>}
    </div>
  )
}
