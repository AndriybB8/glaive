import type { EditScope } from './types'

const OPTIONS: { value: EditScope; label: string }[] = [
  { value: 'single', label: 'To wydarzenie' },
  { value: 'following', label: 'To i następne wydarzenia' },
  { value: 'all', label: 'Wszystkie wydarzenia' },
]

interface ScopePickerProps {
  legend: string
  value: EditScope
  onChange: (scope: EditScope) => void
  disabled?: boolean
}

/** Które wystąpienia serii zmienić albo usunąć – jak w Google Calendar. */
export function ScopePicker({ legend, value, onChange, disabled = false }: ScopePickerProps) {
  return (
    <fieldset className="flex min-w-0 flex-col gap-1.5 rounded-lg bg-slate-50 px-3 py-2.5">
      <legend className="float-left mb-1 text-sm font-medium text-slate-700">{legend}</legend>
      {OPTIONS.map((option) => (
        <label key={option.value} className="clear-left flex cursor-pointer items-center gap-2 text-sm text-slate-800">
          <input
            type="radio"
            name="calendar-scope"
            value={option.value}
            checked={value === option.value}
            onChange={() => onChange(option.value)}
            disabled={disabled}
            className="size-4 accent-blue-600"
          />
          {option.label}
        </label>
      ))}
    </fieldset>
  )
}
