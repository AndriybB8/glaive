import { DEFAULT_TAG_COLOR, readableTextColor, sameColor, TAG_COLOR_PRESETS } from './tagColors'

interface TagColorPickerProps {
  value: string
  onChange: (color: string) => void
}

const swatchBase =
  'flex size-9 items-center justify-center rounded-full ring-offset-2 transition-shadow sm:size-8 peer-focus-visible:ring-2 peer-focus-visible:ring-blue-600'

function CheckMark({ color }: { color: string }) {
  return (
    <svg viewBox="0 0 16 16" className="size-4" fill="none" stroke={color} strokeWidth={2.2} strokeLinecap="round" strokeLinejoin="round" aria-hidden>
      <path d="m3.5 8.5 3 3 6-7" />
    </svg>
  )
}

/** Wybór koloru: gotowa paleta jako grupa przycisków radio plus dowolny kolor z systemowego próbnika. */
export function TagColorPicker({ value, onChange }: TagColorPickerProps) {
  const isPreset = TAG_COLOR_PRESETS.some((preset) => sameColor(preset.value, value))

  return (
    <fieldset className="flex min-w-0 flex-col gap-1.5">
      <legend className="mb-1.5 text-sm font-medium text-slate-700">Kolor</legend>
      <div className="flex flex-wrap gap-2">
        {TAG_COLOR_PRESETS.map((preset) => {
          const checked = sameColor(preset.value, value)
          return (
            <label key={preset.value} title={preset.label} className="cursor-pointer">
              <input
                type="radio"
                name="tag-color"
                value={preset.value}
                checked={checked}
                onChange={() => onChange(preset.value)}
                aria-label={preset.label}
                className="peer sr-only"
              />
              <span
                className={`${swatchBase} ${checked ? 'ring-2 ring-slate-900' : 'hover:ring-2 hover:ring-slate-300'}`}
                style={{ backgroundColor: preset.value }}
              >
                {checked && <CheckMark color={readableTextColor(preset.value)} />}
              </span>
            </label>
          )
        })}

        {/* Własny kolor: przezroczysty input type="color" leży na kółku, więc kliknięcie otwiera systemowy próbnik. */}
        <label title="Własny kolor" className="relative cursor-pointer">
          <input
            type="color"
            value={isPreset ? DEFAULT_TAG_COLOR.toLowerCase() : value.toLowerCase()}
            onChange={(event) => onChange(event.target.value.toUpperCase())}
            aria-label="Własny kolor"
            className="peer absolute inset-0 size-full cursor-pointer opacity-0"
          />
          <span
            className={`${swatchBase} ${isPreset ? 'hover:ring-2 hover:ring-slate-300' : 'ring-2 ring-slate-900'}`}
            style={{
              background: isPreset
                ? 'conic-gradient(#dc2626, #ca8a04, #16a34a, #0284c7, #7c3aed, #db2777, #dc2626)'
                : value,
            }}
          >
            {!isPreset && <CheckMark color={readableTextColor(value)} />}
          </span>
        </label>
      </div>
    </fieldset>
  )
}
