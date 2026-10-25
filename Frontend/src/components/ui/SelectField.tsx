import { useId, type SelectHTMLAttributes } from 'react'

interface SelectOption {
  value: string
  label: string
}

interface SelectFieldProps extends SelectHTMLAttributes<HTMLSelectElement> {
  label: string
  options: readonly SelectOption[]
}

/** Lista wyboru z etykietą – wygląda jak TextField. */
export function SelectField({ label, options, id, className = '', ...props }: SelectFieldProps) {
  const generatedId = useId()
  const selectId = id ?? generatedId

  return (
    <div className={`flex min-w-0 flex-col gap-1.5 ${className}`}>
      <label htmlFor={selectId} className="text-sm font-medium text-slate-700">
        {label}
      </label>
      <select
        id={selectId}
        className="h-11 w-full rounded-md border border-slate-300 bg-white px-3 text-base text-slate-900 focus:border-blue-600 focus:outline-2 focus:-outline-offset-1 focus:outline-blue-600 sm:h-10 sm:text-sm"
        {...props}
      >
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </div>
  )
}
