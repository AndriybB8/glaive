import type {PasswordStrength} from "./passwordStrength.ts";

const STRENGTH_META: Record<PasswordStrength, { label: string; percent: number; barClass: string; textClass: string }> = {
  weak: { label: 'Słabe', percent: 33, barClass: 'bg-red-600', textClass: 'text-red-700' },
  medium: { label: 'Średnie', percent: 66, barClass: 'bg-amber-500', textClass: 'text-amber-700' },
  strong: { label: 'Silne', percent: 100, barClass: 'bg-green-600', textClass: 'text-green-700' },
}

interface PasswordStrengthMeterProps {
  strength: PasswordStrength;
}

export function PasswordStrengthMeter({strength}: PasswordStrengthMeterProps) {
  const meta = STRENGTH_META[strength];

  return  (
    <div className="flex flex-col gap-1">
      <div
        role="progressbar"
        aria-label="Siła hasła"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={meta.percent}
        aria-valuetext={meta.label}
        className="h-1.5 overflow-hidden rounded-full bg-slate-100"
      >
        <div className={`h-full rounded-full transition-[width] duration-300 ${meta.barClass}`} style={{ width: `${meta.percent}%` }} />
      </div>
      <span className={`text-xs ${meta.textClass}`}>{meta.label}</span>
    </div>
  )
}
