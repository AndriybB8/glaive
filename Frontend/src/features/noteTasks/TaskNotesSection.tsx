import { useEffect, useId, useState } from 'react'
import { getNote } from '../notes/api'
import { formatUpdatedAt } from '../notes/formatDate'
import { NotePreview } from '../notes/NotePreview'
import type { Note, NoteSummary } from '../notes/types'
import { getTaskNotes } from './api'

type PreviewState = Note | 'loading' | 'error'

/** Notatki przypisane do zadania – w oknie szczegółów zadania. Kliknięcie rozwija podgląd treści. */
export function TaskNotesSection({ taskId }: { taskId: string }) {
  const headingId = useId()
  const [notes, setNotes] = useState<NoteSummary[] | null>(null)
  const [loadError, setLoadError] = useState(false)
  const [expandedId, setExpandedId] = useState<string | null>(null)
  const [previews, setPreviews] = useState<Record<string, PreviewState>>({})

  useEffect(() => {
    let ignore = false
    getTaskNotes(taskId)
      .then((result) => {
        if (!ignore) setNotes(result)
      })
      .catch(() => {
        if (!ignore) setLoadError(true)
      })
    return () => {
      ignore = true
    }
  }, [taskId])

  const toggle = async (noteId: string) => {
    if (expandedId === noteId) {
      setExpandedId(null)
      return
    }
    setExpandedId(noteId)
    // Treść pobieramy raz; ponownie tylko po błędzie.
    const preview = previews[noteId]
    if (preview && preview !== 'error') return

    setPreviews((current) => ({ ...current, [noteId]: 'loading' }))
    try {
      const note = await getNote(noteId)
      setPreviews((current) => ({ ...current, [noteId]: note }))
    } catch {
      setPreviews((current) => ({ ...current, [noteId]: 'error' }))
    }
  }

  return (
    <section aria-labelledby={headingId} className="flex flex-col gap-1.5">
      <h3 id={headingId} className="text-sm font-medium text-slate-700">
        Notatki
      </h3>

      {loadError ? (
        <p className="text-sm text-red-700">Nie udało się wczytać notatek tego zadania.</p>
      ) : notes === null ? (
        <p role="status" className="text-sm text-slate-500">
          Wczytywanie…
        </p>
      ) : notes.length === 0 ? (
        <p className="text-sm text-slate-500">Brak przypisanych notatek. Przypiszesz je w zakładce Notatki, przy wybranej notatce.</p>
      ) : (
        <ul className="divide-y divide-slate-200 rounded-lg border border-slate-200">
          {notes.map((note) => {
            const expanded = expandedId === note.id
            const preview = previews[note.id]
            const panelId = `${headingId}-${note.id}`
            return (
              <li key={note.id}>
                <button
                  type="button"
                  onClick={() => void toggle(note.id)}
                  aria-expanded={expanded}
                  aria-controls={panelId}
                  className="flex w-full items-baseline gap-3 px-3 py-2.5 text-left text-sm hover:bg-slate-50 focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-blue-600"
                >
                  <span aria-hidden className={`inline-block shrink-0 text-slate-400 transition-transform ${expanded ? 'rotate-90' : ''}`}>
                    ›
                  </span>
                  <span className={`min-w-0 flex-1 truncate ${note.title ? 'text-slate-900' : 'text-slate-500 italic'}`}>
                    {note.title || 'Bez tytułu'}
                  </span>
                  <span className="shrink-0 text-xs text-slate-500 tabular-nums">{formatUpdatedAt(note.updatedAt)}</span>
                </button>
                {expanded && (
                  <div id={panelId} className="max-h-64 overflow-y-auto border-t border-slate-100 bg-slate-50/60 px-4 py-3">
                    {preview === 'loading' || preview === undefined ? (
                      <p role="status" className="text-sm text-slate-500">
                        Wczytywanie treści…
                      </p>
                    ) : preview === 'error' ? (
                      <p className="text-sm text-red-700">Nie udało się wczytać notatki. Mogła zostać usunięta.</p>
                    ) : (
                      <NotePreview content={preview.content} />
                    )}
                  </div>
                )}
              </li>
            )
          })}
        </ul>
      )}
    </section>
  )
}
