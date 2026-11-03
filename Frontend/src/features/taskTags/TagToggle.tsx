import { readableTextColor } from '../tags/tagColors'
import type { Tag } from '../tags/types'

interface TagToggleProps {
  tag: Tag
  pressed: boolean
  onToggle: () => void
  disabled?: boolean
}

/**
 * Tag jako przełącznik (aria-pressed). Włączony ma tło w swoim kolorze i znaczek ✓,
 * wyłączony – kropkę w kolorze i obrys, więc stan widać nie tylko po kolorze.
 * Używany przy wyborze tagów zadania i w filtrze listy zadań.
 */
export function TagToggle({ tag, pressed, onToggle, disabled = false }: TagToggleProps) {
  return (
    <button
      type="button"
      aria-pressed={pressed}
      disabled={disabled}
      onClick={onToggle}
      className={`inline-flex min-h-9 max-w-full items-center gap-1.5 rounded-full border px-3 text-sm font-medium transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600 disabled:opacity-60 sm:min-h-7 sm:px-2.5 sm:text-xs ${
        pressed ? 'border-transparent' : 'border-slate-300 bg-white text-slate-700 hover:bg-slate-50'
      }`}
      style={pressed ? { backgroundColor: tag.color, color: readableTextColor(tag.color) } : undefined}
    >
      {pressed ? (
        <svg viewBox="0 0 16 16" className="size-3.5 shrink-0" fill="none" stroke="currentColor" strokeWidth={2.2} strokeLinecap="round" strokeLinejoin="round" aria-hidden>
          <path d="m3.5 8.5 3 3 6-7" />
        </svg>
      ) : (
        <span aria-hidden className="size-2.5 shrink-0 rounded-full" style={{ backgroundColor: tag.color }} />
      )}
      <span className="truncate">{tag.name}</span>
    </button>
  )
}
