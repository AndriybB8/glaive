import { useCallback, useEffect, useRef, useState } from 'react'
import { createNote, deleteNote, getNote, getNotes, moveNoteToGroup } from './api'
import { DeleteNoteGroupDialog } from './DeleteNoteGroupDialog'
import { formatUpdatedAt } from './formatDate'
import { getNoteGroups } from './groupsApi'
import { NoteEditorPanel } from './NoteEditorPanel'
import { NoteGroupFormDialog } from './NoteGroupFormDialog'
import { NoteGroupsNav } from './NoteGroupsNav'
import type { Note, NoteGroup, NoteGroupFilter, NoteSummary } from './types'

const GROUPS_LOAD_ERROR = 'Nie udało się wczytać grup.'

type OpenGroupDialog = { kind: 'create' } | { kind: 'rename'; group: NoteGroup } | { kind: 'delete'; group: NoteGroup } | null

function sortByUpdated(notes: NoteSummary[]): NoteSummary[] {
  return [...notes].sort((a, b) => b.updatedAt.localeCompare(a.updatedAt))
}

/** Grupy w tej samej kolejności co z backendu (alfabetycznie, bez wielkości liter). */
function sortByName(groups: NoteGroup[]): NoteGroup[] {
  return [...groups].sort((a, b) => a.name.localeCompare(b.name, 'pl', { sensitivity: 'base' }))
}

function matchesFilter(note: NoteSummary, filter: NoteGroupFilter): boolean {
  switch (filter.kind) {
    case 'all':
      return true
    case 'ungrouped':
      return note.groupId === null
    case 'group':
      return note.groupId === filter.groupId
  }
}

function emptyListText(filter: NoteGroupFilter): string {
  switch (filter.kind) {
    case 'all':
      return 'Nie masz jeszcze notatek. Utwórz pierwszą przyciskiem powyżej.'
    case 'ungrouped':
      return 'Wszystkie notatki są przypisane do grup.'
    case 'group':
      return 'Ta grupa jest pusta. Utwórz w niej notatkę albo przenieś tu istniejącą (pole „Grupa” w notatce).'
  }
}

export function NotesPage() {
  const [notes, setNotes] = useState<NoteSummary[] | null>(null)
  const [listError, setListError] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [selected, setSelected] = useState<Note | null>(null)
  /** Zmiana klucza wymusza ponowne zamontowanie edytora (np. po wczytaniu wersji z serwera). */
  const [editorKey, setEditorKey] = useState(0)
  const [loadingId, setLoadingId] = useState<string | null>(null)
  const latestRequestRef = useRef<string | null>(null)

  const [groups, setGroups] = useState<NoteGroup[] | null>(null)
  const [groupsError, setGroupsError] = useState<string | null>(null)
  const [filter, setFilter] = useState<NoteGroupFilter>({ kind: 'all' })
  const [groupDialog, setGroupDialog] = useState<OpenGroupDialog>(null)

  const visibleNotes = notes?.filter((n) => matchesFilter(n, filter)) ?? null

  const loadNotes = useCallback(async () => {
    try {
      setListError(null)
      setNotes(sortByUpdated(await getNotes()))
    } catch {
      setListError('Nie udało się wczytać notatek. Sprawdź, czy backend jest uruchomiony.')
    }
  }, [])

  const loadGroups = useCallback(async () => {
    try {
      setGroupsError(null)
      const result = await getNoteGroups()
      setGroups(result)
      // Wybrana grupa mogła zniknąć (usunięta w innej karcie) – wracamy do wszystkich notatek.
      setFilter((current) => (current.kind === 'group' && !result.some((g) => g.id === current.groupId) ? { kind: 'all' } : current))
    } catch {
      setGroupsError(GROUPS_LOAD_ERROR)
    }
  }, [])

  useEffect(() => {
    let ignore = false
    getNotes()
      .then((result) => {
        if (!ignore) setNotes(sortByUpdated(result))
      })
      .catch(() => {
        if (!ignore) setListError('Nie udało się wczytać notatek. Sprawdź, czy backend jest uruchomiony.')
      })
    getNoteGroups()
      .then((result) => {
        if (!ignore) setGroups(result)
      })
      .catch(() => {
        if (!ignore) setGroupsError(GROUPS_LOAD_ERROR)
      })
    return () => {
      ignore = true
    }
  }, [])

  const openNote = async (id: string) => {
    if (id === selected?.id) return
    latestRequestRef.current = id
    setLoadingId(id)
    setActionError(null)
    try {
      const note = await getNote(id)
      if (latestRequestRef.current !== id) return
      setSelected(note)
      setEditorKey((key) => key + 1)
    } catch {
      if (latestRequestRef.current === id) setActionError('Nie udało się otworzyć notatki.')
    } finally {
      if (latestRequestRef.current === id) setLoadingId(null)
    }
  }

  const handleCreate = async () => {
    setActionError(null)
    // Nowa notatka trafia do grupy, którą użytkownik właśnie przegląda.
    const groupId = filter.kind === 'group' ? filter.groupId : null
    try {
      const note = await createNote({}, groupId)
      latestRequestRef.current = note.id
      setNotes((current) => [{ id: note.id, title: note.title, updatedAt: note.updatedAt, groupId: note.groupId }, ...(current ?? [])])
      setSelected(note)
      setEditorKey((key) => key + 1)
    } catch {
      setActionError('Nie udało się utworzyć notatki.')
      // Najczęstszy powód przy wybranej grupie: usunięto ją w innej karcie.
      if (groupId) void loadGroups()
    }
  }

  const handleDelete = async () => {
    if (!selected) return
    const name = selected.title.trim() || 'Bez tytułu'
    if (!window.confirm(`Usunąć notatkę „${name}”? Tej operacji nie można cofnąć.`)) return

    try {
      await deleteNote(selected.id)
      setNotes((current) => current?.filter((n) => n.id !== selected.id) ?? null)
      setSelected(null)
    } catch {
      setActionError('Nie udało się usunąć notatki.')
    }
  }

  const handleSaved = useCallback((saved: Note) => {
    // groupId zostaje z listy: grupę zmienia tylko handleMove, a odpowiedź autozapisu
    // wysłanego tuż przed przeniesieniem mogłaby jeszcze nieść starą grupę.
    setNotes((current) =>
      current
        ? sortByUpdated(current.map((n) => (n.id === saved.id ? { ...n, title: saved.title, updatedAt: saved.updatedAt } : n)))
        : current,
    )
  }, [])

  const handleReplace = (note: Note) => {
    setSelected(note)
    setEditorKey((key) => key + 1)
    setNotes((current) =>
      current
        ? sortByUpdated(current.map((n) => (n.id === note.id ? { id: note.id, title: note.title, updatedAt: note.updatedAt, groupId: note.groupId } : n)))
        : current,
    )
  }

  /** Przeniesienie otwartej notatki. Błąd przekazujemy dalej – pokazuje go pole „Grupa” w edytorze. */
  const handleMove = async (groupId: string | null) => {
    if (!selected) return
    const id = selected.id
    try {
      await moveNoteToGroup(id, groupId)
    } catch (error) {
      // Wybrana grupa mogła zostać usunięta w innej karcie – odświeżamy listę grup.
      void loadGroups()
      throw error
    }
    setSelected((current) => (current?.id === id ? { ...current, groupId } : current))
    setNotes((current) => current?.map((n) => (n.id === id ? { ...n, groupId } : n)) ?? current)
  }

  const handleGroupSaved = (saved: NoteGroup) => {
    const isNew = !groups?.some((g) => g.id === saved.id)
    setGroups((current) => sortByName([...(current ?? []).filter((g) => g.id !== saved.id), saved]))
    // Po utworzeniu od razu pokazujemy nową (pustą) grupę – można w niej dodać notatkę.
    if (isNew) setFilter({ kind: 'group', groupId: saved.id })
    setGroupDialog(null)
  }

  const handleGroupChangedElsewhere = (current: NoteGroup | { deletedId: string }) => {
    if ('deletedId' in current) {
      removeGroupLocally(current.deletedId)
    } else {
      setGroups((list) => (list ? sortByName(list.map((g) => (g.id === current.id ? current : g))) : list))
    }
  }

  const handleGroupDeleted = (id: string) => {
    removeGroupLocally(id)
    setGroupDialog(null)
  }

  /** Grupa zniknęła: jej notatki są teraz „Bez grupy” (tak samo zrobiła baza). */
  const removeGroupLocally = (id: string) => {
    setGroups((current) => current?.filter((g) => g.id !== id) ?? current)
    setNotes((current) => current?.map((n) => (n.groupId === id ? { ...n, groupId: null } : n)) ?? current)
    setSelected((current) => (current?.groupId === id ? { ...current, groupId: null } : current))
    setFilter((current) => (current.kind === 'group' && current.groupId === id ? { kind: 'all' } : current))
  }

  return (
    <div className="flex h-dvh bg-white text-slate-900">
      <aside
        className={`w-full flex-col border-r border-slate-200 bg-slate-50 md:flex md:w-72 md:shrink-0 ${
          selected ? 'hidden' : 'flex'
        }`}
      >
        <div className="flex items-center justify-between gap-2 px-4 pt-5 pb-3">
          <h1 className="text-lg font-semibold">Notatki</h1>
          <button
            type="button"
            onClick={handleCreate}
            className="rounded-md bg-blue-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-blue-700 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600"
          >
            Nowa notatka
          </button>
        </div>

        {actionError && (
          <p role="alert" className="mx-4 mb-2 rounded-md bg-red-50 px-3 py-2 text-sm text-red-800">
            {actionError}
          </p>
        )}

        <NoteGroupsNav
          groups={groups}
          error={groupsError}
          notes={notes}
          filter={filter}
          onFilterChange={setFilter}
          onCreate={() => setGroupDialog({ kind: 'create' })}
          onRename={(group) => setGroupDialog({ kind: 'rename', group })}
          onDelete={(group) => setGroupDialog({ kind: 'delete', group })}
          onRetry={() => void loadGroups()}
        />

        <nav aria-label="Lista notatek" className="min-h-0 flex-1 overflow-y-auto px-2 pt-2 pb-4">
          {listError ? (
            <div className="px-2 py-4 text-sm text-slate-600">
              <p>{listError}</p>
              <button type="button" onClick={loadNotes} className="mt-2 font-medium text-blue-700 hover:underline">
                Spróbuj ponownie
              </button>
            </div>
          ) : visibleNotes === null ? (
            <p className="px-2 py-4 text-sm text-slate-500">Wczytywanie…</p>
          ) : visibleNotes.length === 0 ? (
            <p className="px-2 py-4 text-sm text-slate-600">{emptyListText(filter)}</p>
          ) : (
            <ul className="space-y-0.5">
              {visibleNotes.map((n) => {
                const isActive = n.id === selected?.id
                return (
                  <li key={n.id}>
                    <button
                      type="button"
                      onClick={() => void openNote(n.id)}
                      aria-current={isActive ? 'page' : undefined}
                      className={`flex w-full items-baseline gap-3 rounded-md px-3 py-2 text-left text-sm focus-visible:outline-2 focus-visible:outline-blue-600 ${
                        isActive ? 'bg-white font-medium text-slate-900 shadow-[inset_2px_0_0] shadow-blue-600' : 'text-slate-700 hover:bg-slate-100'
                      }`}
                    >
                      <span className={`min-w-0 flex-1 truncate ${n.title ? '' : 'text-slate-400 italic'}`}>
                        {n.title || 'Bez tytułu'}
                      </span>
                      <span className="shrink-0 text-xs text-slate-400 tabular-nums">
                        {loadingId === n.id ? 'Otwieranie…' : formatUpdatedAt(n.updatedAt)}
                      </span>
                    </button>
                  </li>
                )
              })}
            </ul>
          )}
        </nav>
      </aside>

      <main className={`min-w-0 flex-1 overflow-y-auto ${selected ? 'flex' : 'hidden md:flex'}`}>
        {selected ? (
          <NoteEditorPanel
            key={`${selected.id}-${editorKey}`}
            note={selected}
            onSaved={handleSaved}
            onDelete={handleDelete}
            onReplace={handleReplace}
            onBack={() => setSelected(null)}
            groups={groups}
            onMove={handleMove}
          />
        ) : (
          <div className="m-auto max-w-xs px-6 text-center text-sm text-slate-500">
            Wybierz notatkę z listy albo utwórz nową.
          </div>
        )}
      </main>

      {(groupDialog?.kind === 'create' || groupDialog?.kind === 'rename') && (
        <NoteGroupFormDialog
          group={groupDialog.kind === 'rename' ? groupDialog.group : undefined}
          existingGroups={groups ?? []}
          onClose={() => setGroupDialog(null)}
          onSaved={handleGroupSaved}
          onChangedElsewhere={handleGroupChangedElsewhere}
        />
      )}
      {groupDialog?.kind === 'delete' && (
        <DeleteNoteGroupDialog
          group={groupDialog.group}
          noteCount={notes?.filter((n) => n.groupId === groupDialog.group.id).length ?? 0}
          onClose={() => setGroupDialog(null)}
          onDeleted={handleGroupDeleted}
        />
      )}
    </div>
  )
}
