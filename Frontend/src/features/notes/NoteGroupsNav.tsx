import type { ReactNode } from 'react'
import { IconButton } from '../../components/ui/IconButton'
import { FolderIcon, NotesIcon, PencilIcon, PlusIcon, TrashIcon, TrayIcon } from '../../components/ui/icons'
import type { NoteGroup, NoteGroupFilter, NoteSummary } from './types'

interface NoteGroupsNavProps {
  groups: NoteGroup[] | null
  error: string | null
  notes: NoteSummary[] | null
  filter: NoteGroupFilter
  onFilterChange: (filter: NoteGroupFilter) => void
  onCreate: () => void
  onRename: (group: NoteGroup) => void
  onDelete: (group: NoteGroup) => void
  onRetry: () => void
}

/**
 * Lista grup (folderów) nad listą notatek: „Wszystkie notatki”, „Bez grupy” i grupy użytkownika z licznikami.
 * Wybrana pozycja filtruje listę notatek. Zmiana nazwy i usuwanie są przy zaznaczonej grupie.
 */
export function NoteGroupsNav({ groups, error, notes, filter, onFilterChange, onCreate, onRename, onDelete, onRetry }: NoteGroupsNavProps) {
  const counts = countNotes(notes)

  return (
    <nav aria-label="Grupy notatek" className="max-h-[40vh] shrink-0 overflow-y-auto border-b border-slate-200 px-2 pb-3">
      <div className="flex items-center justify-between gap-2 py-1 pr-1 pl-3">
        <h2 className="text-xs font-semibold tracking-wide text-slate-500 uppercase">Grupy</h2>
        <IconButton label="Nowa grupa" touch onClick={onCreate}>
          <PlusIcon />
        </IconButton>
      </div>

      <ul className="space-y-0.5">
        <FilterRow
          icon={<NotesIcon />}
          label="Wszystkie notatki"
          count={notes?.length}
          active={filter.kind === 'all'}
          onSelect={() => onFilterChange({ kind: 'all' })}
        />
        <FilterRow
          icon={<TrayIcon />}
          label="Bez grupy"
          count={notes ? (counts.get(null) ?? 0) : undefined}
          active={filter.kind === 'ungrouped'}
          onSelect={() => onFilterChange({ kind: 'ungrouped' })}
        />
        {groups?.map((group) => {
          const active = filter.kind === 'group' && filter.groupId === group.id
          return (
            <FilterRow
              key={group.id}
              icon={<FolderIcon />}
              label={group.name}
              count={notes ? (counts.get(group.id) ?? 0) : undefined}
              active={active}
              onSelect={() => onFilterChange({ kind: 'group', groupId: group.id })}
              actions={
                active && (
                  <>
                    <IconButton label={`Zmień nazwę grupy ${group.name}`} title="Zmień nazwę" onClick={() => onRename(group)}>
                      <PencilIcon />
                    </IconButton>
                    <IconButton label={`Usuń grupę ${group.name}`} title="Usuń grupę" tone="danger" onClick={() => onDelete(group)}>
                      <TrashIcon />
                    </IconButton>
                  </>
                )
              }
            />
          )
        })}
      </ul>

      {error ? (
        <div className="px-3 pt-2 text-sm text-slate-600">
          <p>{error}</p>
          <button type="button" onClick={onRetry} className="mt-1 font-medium text-blue-700 hover:underline">
            Spróbuj ponownie
          </button>
        </div>
      ) : groups === null ? (
        <p className="px-3 pt-2 text-sm text-slate-500">Wczytywanie grup…</p>
      ) : (
        groups.length === 0 && (
          <p className="px-3 pt-2 text-sm text-slate-500">Utwórz grupę przyciskiem „+”, żeby porządkować notatki według przedmiotów.</p>
        )
      )}
    </nav>
  )
}

interface FilterRowProps {
  icon: ReactNode
  label: string
  /** Brak = notatki jeszcze się wczytują. */
  count: number | undefined
  active: boolean
  onSelect: () => void
  actions?: ReactNode
}

function FilterRow({ icon, label, count, active, onSelect, actions }: FilterRowProps) {
  return (
    <li className={`flex items-center rounded-md ${active ? 'bg-slate-200/70' : 'hover:bg-slate-100'}`}>
      <button
        type="button"
        onClick={onSelect}
        aria-current={active ? 'true' : undefined}
        className={`flex min-h-11 min-w-0 flex-1 items-center gap-2.5 rounded-md px-3 py-1.5 text-left text-sm focus-visible:outline-2 focus-visible:outline-blue-600 sm:min-h-0 ${
          active ? 'font-medium text-slate-900' : 'text-slate-700'
        }`}
      >
        <span className={`shrink-0 ${active ? 'text-blue-600' : 'text-slate-400'}`}>{icon}</span>
        <span className="min-w-0 flex-1 truncate">{label}</span>
        {count !== undefined && <span className="shrink-0 text-xs text-slate-500 tabular-nums">{count}</span>}
      </button>
      {actions && <div className="flex shrink-0 items-center pr-1">{actions}</div>}
    </li>
  )
}

/** Liczba notatek w każdej grupie; klucz `null` = notatki bez grupy. */
function countNotes(notes: NoteSummary[] | null): Map<string | null, number> {
  const counts = new Map<string | null, number>()
  for (const note of notes ?? []) {
    counts.set(note.groupId, (counts.get(note.groupId) ?? 0) + 1)
  }
  return counts
}
