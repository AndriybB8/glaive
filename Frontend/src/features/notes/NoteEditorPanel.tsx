import type { Editor } from '@tiptap/react'
import { useCallback, useId, useRef, useState } from 'react'
import { ApiError } from './api'
import { NoteEditor } from './NoteEditor'
import { formatUpdatedAt } from './formatDate'
import { useNoteAutosave, type SaveStatus } from './useNoteAutosave'
import type { Note, NoteGroup } from './types'

interface NoteEditorPanelProps {
  note: Note
  onSaved: (note: Note) => void
  onDelete: () => void
  /** Odrzuca lokalne zmiany i pokazuje podaną wersję notatki. */
  onReplace: (note: Note) => void
  onBack: () => void
  /** Grupy do wyboru; null = jeszcze się wczytują (albo nie udało się ich pobrać). */
  groups: NoteGroup[] | null
  /** Przenosi notatkę do grupy (null = „Bez grupy”). Odrzuca obietnicę, gdy się nie uda. */
  onMove: (groupId: string | null) => Promise<void>
}

function statusText(status: SaveStatus, lastSavedAt: string): string {
  switch (status) {
    case 'pending':
      return 'Niezapisane zmiany'
    case 'saving':
      return 'Zapisywanie…'
    case 'error':
      return 'Nie udało się zapisać. Ponawiam za chwilę.'
    case 'conflict':
      return 'Autozapis wstrzymany'
    default:
      return `Zapisano ${formatUpdatedAt(lastSavedAt)}`
  }
}

export function NoteEditorPanel({ note, onSaved, onDelete, onReplace, onBack, groups, onMove }: NoteEditorPanelProps) {
  const [title, setTitle] = useState(note.title)
  const editorRef = useRef<Editor | null>(null)
  const { status, lastSavedAt, conflict, markChanged, saveNow, keepMine } = useNoteAutosave(note, onSaved)

  const handleContentChange = useCallback((content: string) => markChanged({ content }), [markChanged])
  const handleEditorReady = useCallback((editor: Editor) => {
    editorRef.current = editor
  }, [])

  return (
    <article className="mx-auto flex w-full max-w-3xl flex-1 flex-col px-5 pb-16 sm:px-8">
      <header className="flex items-center gap-3 py-4 text-sm">
        <button
          type="button"
          onClick={onBack}
          className="rounded-md px-2 py-1 text-slate-600 hover:bg-slate-100 focus-visible:outline-2 focus-visible:outline-blue-600 md:hidden"
        >
          Wszystkie notatki
        </button>
        <p
          role="status"
          className={`ml-auto ${status === 'error' ? 'text-red-700' : 'text-slate-500'}`}
        >
          {statusText(status, lastSavedAt)}
        </p>
        <button
          type="button"
          onClick={onDelete}
          className="rounded-md px-2 py-1 text-slate-500 hover:bg-red-50 hover:text-red-700 focus-visible:outline-2 focus-visible:outline-blue-600"
        >
          Usuń
        </button>
      </header>

      {conflict && (
        <div role="alert" className="mb-4 rounded-lg border border-amber-300 bg-amber-50 p-4 text-sm text-amber-900">
          <p className="font-medium">Ta notatka została zmieniona w innym miejscu.</p>
          <p className="mt-1">
            Wersja na serwerze jest z {formatUpdatedAt(conflict.updatedAt)}. Wybierz, którą wersję zachować.
          </p>
          <div className="mt-3 flex flex-wrap gap-2">
            <button
              type="button"
              onClick={keepMine}
              className="rounded-md bg-amber-900 px-3 py-1.5 font-medium text-white hover:bg-amber-800"
            >
              Zachowaj moją wersję
            </button>
            <button
              type="button"
              onClick={() => onReplace(conflict)}
              className="rounded-md border border-amber-400 px-3 py-1.5 font-medium hover:bg-amber-100"
            >
              Wczytaj wersję z serwera
            </button>
          </div>
        </div>
      )}

      <input
        value={title}
        onChange={(event) => {
          setTitle(event.target.value)
          markChanged({ title: event.target.value })
        }}
        onKeyDown={(event) => {
          if (event.key === 'Enter') {
            event.preventDefault()
            editorRef.current?.commands.focus('start')
          } else if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's') {
            event.preventDefault()
            saveNow()
          }
        }}
        maxLength={200}
        placeholder="Bez tytułu"
        aria-label="Tytuł notatki"
        className="w-full bg-transparent pb-3 text-3xl font-bold tracking-tight text-slate-900 outline-none placeholder:text-slate-300"
      />

      <NoteGroupPicker groupId={note.groupId} groups={groups} onMove={onMove} />

      <NoteEditor
        initialContent={note.content}
        onChange={handleContentChange}
        onSaveShortcut={saveNow}
        onReady={handleEditorReady}
      />
    </article>
  )
}

interface NoteGroupPickerProps {
  groupId: string | null
  groups: NoteGroup[] | null
  onMove: (groupId: string | null) => Promise<void>
}

/** Wybór grupy notatki. Zmiana od razu przenosi notatkę – nie czeka na autozapis i go nie przerywa. */
function NoteGroupPicker({ groupId, groups, onMove }: NoteGroupPickerProps) {
  const selectId = useId()
  const [moving, setMoving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  // Grupa mogła zostać usunięta w innej karcie – wtedy pokazujemy „Bez grupy”.
  const value = groupId && groups?.some((g) => g.id === groupId) ? groupId : ''

  const handleChange = async (newValue: string) => {
    setMoving(true)
    setError(null)
    try {
      await onMove(newValue || null)
    } catch (err) {
      if (err instanceof ApiError && err.status === 400) {
        setError(err.message)
      } else if (err instanceof ApiError && err.status === 404) {
        setError('Ta notatka została w międzyczasie usunięta.')
      } else {
        setError('Nie udało się przenieść notatki.')
      }
    } finally {
      setMoving(false)
    }
  }

  return (
    <div className="mb-4 flex flex-wrap items-center gap-x-2 gap-y-1 text-sm">
      <label htmlFor={selectId} className="text-slate-500">
        Grupa
      </label>
      <select
        id={selectId}
        value={value}
        onChange={(event) => void handleChange(event.target.value)}
        disabled={moving || groups === null}
        className="h-11 max-w-full min-w-0 truncate rounded-md border border-slate-200 bg-white px-2 text-slate-700 hover:border-slate-300 focus:border-blue-600 focus:outline-2 focus:-outline-offset-1 focus:outline-blue-600 disabled:opacity-60 sm:h-8 sm:max-w-64"
      >
        <option value="">Bez grupy</option>
        {groups?.map((group) => (
          <option key={group.id} value={group.id}>
            {group.name}
          </option>
        ))}
      </select>
      {moving && <span className="text-slate-500">Przenoszenie…</span>}
      {error && (
        <span role="alert" className="text-red-700">
          {error}
        </span>
      )}
    </div>
  )
}
