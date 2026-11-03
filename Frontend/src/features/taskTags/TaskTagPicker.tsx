import type { Tag } from '../tags/types'
import { TagToggle } from './TagToggle'

interface TaskTagPickerProps {
  /** Wszystkie tagi użytkownika; null = jeszcze się wczytują. */
  tags: readonly Tag[] | null
  selectedIds: ReadonlySet<string>
  onToggle: (tagId: string) => void
  loadError: boolean
  disabled?: boolean
}

/** Wybór tagów zadania: każdy tag to przełącznik ({@link TagToggle}). */
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
          {tags.map((tag) => (
            <TagToggle key={tag.id} tag={tag} pressed={selectedIds.has(tag.id)} onToggle={() => onToggle(tag.id)} disabled={disabled} />
          ))}
        </div>
      )}
    </fieldset>
  )
}
