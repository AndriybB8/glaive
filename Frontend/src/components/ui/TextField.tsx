import { useId, type InputHTMLAttributes } from 'react'

interface TextFieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string
  hint?: string
  /** Komunikat błędu pod polem. */
  error?: string
  /** Czerwona ramka bez własnego komunikatu – gdy błąd dotyczy kilku pól i jest pokazany niżej. */
  invalid?: boolean
}

/** Pole formularza z etykietą nad nim. Działa też z `type="date"` (natywny wybór daty). */
export function TextField({ label, hint, error, invalid = false, id, className = '', 'aria-describedby': describedBy, ...props }: TextFieldProps) {
  const generatedId = useId()
  const inputId = id ?? generatedId
  const messageId = `${inputId}-message`
  const message = error ?? hint
  const isInvalid = Boolean(error) || invalid

  return (
    <div className={`flex min-w-0 flex-col gap-1.5 ${className}`}>
      <label htmlFor={inputId} className="text-sm font-medium text-slate-700">
        {label}
      </label>
      <input
        id={inputId}
        aria-invalid={isInvalid || undefined}
        aria-describedby={[describedBy, message ? messageId : undefined].filter(Boolean).join(' ') || undefined}
        className={`h-11 w-full rounded-md border bg-white px-3 text-base text-slate-900 placeholder:text-slate-500 focus:outline-2 focus:-outline-offset-1 sm:h-10 sm:text-sm ${
          isInvalid ? 'border-red-600 focus:outline-red-600' : 'border-slate-300 focus:border-blue-600 focus:outline-blue-600'
        }`}
        {...props}
      />
      {message && (
        <p id={messageId} className={`text-sm ${error ? 'text-red-700' : 'text-slate-500'}`}>
          {message}
        </p>
      )}
    </div>
  )
}
