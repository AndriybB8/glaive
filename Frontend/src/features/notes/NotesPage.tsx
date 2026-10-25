import { useCallback, useEffect, useRef, useState } from 'react'
import { createNote, deleteNote, getNote, getNotes } from './api'
import { formatUpdatedAt } from './formatDate'
import { NoteEditorPanel } from './NoteEditorPanel'
import type { Note, NoteSummary } from './types'

function sortByUpdated(notes: NoteSummary[]): NoteSummary[] {
  return [...notes].sort((a, b) => b.updatedAt.localeCompare(a.updatedAt))
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

  const loadNotes = useCallback(async () => {
    try {
      setListError(null)
      setNotes(sortByUpdated(await getNotes()))
    } catch {
      setListError('Nie udało się wczytać notatek. Sprawdź, czy backend jest uruchomiony.')
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
    try {
      const note = await createNote()
      latestRequestRef.current = note.id
      setNotes((current) => [{ id: note.id, title: note.title, updatedAt: note.updatedAt }, ...(current ?? [])])
      setSelected(note)
      setEditorKey((key) => key + 1)
    } catch {
      setActionError('Nie udało się utworzyć notatki.')
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
    setNotes((current) =>
      current
        ? sortByUpdated(
            current.map((n) => (n.id === saved.id ? { id: saved.id, title: saved.title, updatedAt: saved.updatedAt } : n)),
          )
        : current,
    )
  }, [])

  const handleReplace = (note: Note) => {
    setSelected(note)
    setEditorKey((key) => key + 1)
    handleSaved(note)
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

        <nav aria-label="Lista notatek" className="min-h-0 flex-1 overflow-y-auto px-2 pb-4">
          {listError ? (
            <div className="px-2 py-4 text-sm text-slate-600">
              <p>{listError}</p>
              <button type="button" onClick={loadNotes} className="mt-2 font-medium text-blue-700 hover:underline">
                Spróbuj ponownie
              </button>
            </div>
          ) : notes === null ? (
            <p className="px-2 py-4 text-sm text-slate-500">Wczytywanie…</p>
          ) : notes.length === 0 ? (
            <p className="px-2 py-4 text-sm text-slate-600">Nie masz jeszcze notatek. Utwórz pierwszą przyciskiem powyżej.</p>
          ) : (
            <ul className="space-y-0.5">
              {notes.map((n) => {
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
          />
        ) : (
          <div className="m-auto max-w-xs px-6 text-center text-sm text-slate-500">
            Wybierz notatkę z listy albo utwórz nową.
          </div>
        )}
      </main>
    </div>
  )
}
