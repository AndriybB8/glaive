import { useId, type TextareaHTMLAttributes } from 'react'

interface TextAreaFieldProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  label: string
  hint?: string
  /** Komunikat błędu pod polem. */
  error?: string
}

/** Pole wielowierszowe z etykietą – wygląda jak TextField. */
export function TextAreaField({ label, hint, error, id, rows = 3, className = '', ...props }: TextAreaFieldProps) {
  const generatedId = useId()
  const textAreaId = id ?? generatedId
  const messageId = `${textAreaId}-message`
  const message = error ?? hint

  return (
    <div className={`flex min-w-0 flex-col gap-1.5 ${className}`}>
      <label htmlFor={textAreaId} className="text-sm font-medium text-slate-700">
        {label}
      </label>
      <textarea
        id={textAreaId}
        rows={rows}
        aria-invalid={error ? true : undefined}
        aria-describedby={message ? messageId : undefined}
        className={`w-full resize-y rounded-md border bg-white px-3 py-2 text-base text-slate-900 placeholder:text-slate-500 focus:outline-2 focus:-outline-offset-1 sm:text-sm ${
          error ? 'border-red-600 focus:outline-red-600' : 'border-slate-300 focus:border-blue-600 focus:outline-blue-600'
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
