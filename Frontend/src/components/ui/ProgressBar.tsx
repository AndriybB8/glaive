interface ProgressBarProps {
  value: number
  max: number
  /** Opis dla czytników ekranu, np. „Postęp harmonogramu”. */
  label: string
}

/** Pasek postępu w kolorze akcentu. Przy max = 0 jest pusty. */
export function ProgressBar({ value, max, label }: ProgressBarProps) {
  const percent = max > 0 ? Math.round((value / max) * 100) : 0

  return (
    <div
      role="progressbar"
      aria-label={label}
      aria-valuemin={0}
      aria-valuemax={max}
      aria-valuenow={value}
      aria-valuetext={`${percent}%`}
      className="h-2 overflow-hidden rounded-full bg-slate-100"
    >
      <div className="h-full rounded-full bg-blue-600 transition-[width] duration-300" style={{ width: `${percent}%` }} />
    </div>
  )
}
