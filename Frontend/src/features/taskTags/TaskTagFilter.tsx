import { useId } from 'react'
import { Button } from '../../components/ui/Button'
import type { Tag } from '../tags/types'
import { TagToggle } from './TagToggle'

export type TagFilterStatus = 'idle' | 'loading' | 'error'

interface TaskTagFilterProps {
  tags: readonly Tag[]
  selectedIds: readonly string[]
  onToggle: (tagId: string) => void
  onClear: () => void
  status: TagFilterStatus
  onRetry: () => void
  /** Ile zadań pasuje do filtru; null = filtr nieaktywny albo wynik jeszcze nie przyszedł. */
  matchCount: number | null
  totalCount: number
}

/**
 * Filtr listy zadań po tagach. Backend zwraca zadania, które mają wszystkie wybrane tagi,
 * dlatego przy kilku tagach mówimy to wprost.
 */
export function TaskTagFilter({ tags, selectedIds, onToggle, onClear, status, onRetry, matchCount, totalCount }: TaskTagFilterProps) {
  const headingId = useId()
  const active = selectedIds.length > 0

  let summary: string | null = null
  if (active && status === 'loading') summary = 'Filtrowanie…'
  else if (active && matchCount !== null) {
    summary = `Pasujące zadania: ${matchCount} z ${totalCount}`
    if (selectedIds.length > 1) summary += ' · tylko zadania ze wszystkimi wybranymi tagami'
  }

  return (
    <section aria-labelledby={headingId} className="mt-5 flex flex-col gap-2">
      <div className="flex min-h-8 flex-wrap items-center justify-between gap-x-3 gap-y-1">
        <h2 id={headingId} className="text-sm font-medium text-slate-700">
          Filtruj po tagach
        </h2>
        {active && (
          <Button variant="ghost" onClick={onClear} className="-mr-2">
            Wyczyść filtr
          </Button>
        )}
      </div>

      <div role="group" aria-label="Tagi filtru" className="flex flex-wrap gap-1.5">
        {tags.map((tag) => (
          <TagToggle key={tag.id} tag={tag} pressed={selectedIds.includes(tag.id)} onToggle={() => onToggle(tag.id)} />
        ))}
      </div>

      {status === 'error' && active ? (
        <p role="alert" className="text-sm text-red-700">
          Nie udało się przefiltrować zadań.{' '}
          <button type="button" onClick={onRetry} className="font-medium text-blue-700 hover:underline">
            Spróbuj ponownie
          </button>
        </p>
      ) : (
        summary && (
          <p role="status" className="text-sm text-slate-500 tabular-nums">
            {summary}
          </p>
        )
      )}
    </section>
  )
}
