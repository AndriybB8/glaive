import type { Tag } from '../tags/types'
import { readableTextColor } from '../tags/tagColors'

interface TaskTagPickerProps {
  /** Wszystkie tagi użytkownika; null = jeszcze się wczytują. */
  tags: readonly Tag[] | null
  selectedIds: ReadonlySet<string>
  onToggle: (tagId: string) => void
  loadError: boolean
  disabled?: boolean
}

/**
 * Wybór tagów zadania: każdy tag to przełącznik. Wybrany ma tło w swoim kolorze,
 * niewybrany – kropkę w kolorze i obrys, więc stan widać nie tylko po kolorze, ale też po znaczku ✓.
 */
export function TaskTagPicker({ tags, selectedIds, onToggle, loadError, disabled = false }: TaskTagPickerProps) {
  return (
    <fieldset className="flex min-w-0 flex-col gap-1.5">
      <legend className="mb-1.5 text-sm font-medium text-slate-700">Tagi</legend>

      {loadError ? (
        <p className="text-sm text-red-700">Nie udało się wczytać tagów. Zadanie zapiszesz bez zmian w tagach.</p>
      ) : tags === null ? (
        <p role="status" className="text-sm text-slate-500">
          Wczytywanie…
        </p>
      ) : tags.length === 0 ? (
        <p className="text-sm text-slate-500">
          Nie masz jeszcze tagów. Utworzysz je w zakładce{' '}
          <a href="#tagi" className="font-medium text-blue-700 hover:underline">
            Tagi
          </a>
          .
        </p>
      ) : (
        <div className="flex flex-wrap gap-1.5">
          {tags.map((tag) => {
            const selected = selectedIds.has(tag.id)
            return (
              <button
                key={tag.id}
                type="button"
                aria-pressed={selected}
                disabled={disabled}
                onClick={() => onToggle(tag.id)}
                className={`inline-flex min-h-9 max-w-full items-center gap-1.5 rounded-full border px-3 text-sm font-medium transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600 disabled:opacity-60 sm:min-h-7 sm:px-2.5 sm:text-xs ${
                  selected ? 'border-transparent' : 'border-slate-300 bg-white text-slate-700 hover:bg-slate-50'
                }`}
                style={selected ? { backgroundColor: tag.color, color: readableTextColor(tag.color) } : undefined}
              >
                {selected ? (
                  <svg viewBox="0 0 16 16" className="size-3.5 shrink-0" fill="none" stroke="currentColor" strokeWidth={2.2} strokeLinecap="round" strokeLinejoin="round" aria-hidden>
                    <path d="m3.5 8.5 3 3 6-7" />
                  </svg>
                ) : (
                  <span aria-hidden className="size-2.5 shrink-0 rounded-full" style={{ backgroundColor: tag.color }} />
                )}
                <span className="truncate">{tag.name}</span>
              </button>
            )
          })}
        </div>
      )}
    </fieldset>
  )
}
