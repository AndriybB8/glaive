import { useCallback, useEffect, useRef, useState } from 'react'
import { isNoteConflict, updateNote, updateNoteOnExit } from './api'
import type { Note, NoteDraft } from './types'

export type SaveStatus = 'saved' | 'pending' | 'saving' | 'error' | 'conflict'

/** Ile czekać po ostatniej zmianie, zanim wyślemy zapis. */
const AUTOSAVE_DELAY_MS = 1000
/** Po nieudanym zapisie (np. brak sieci) ponawiamy po tym czasie. */
const RETRY_DELAY_MS = 5000

/**
 * Autozapis jednej notatki.
 * - zapis wysyłany jest {@link AUTOSAVE_DELAY_MS} po ostatniej zmianie (debounce),
 * - naraz leci tylko jedno żądanie; zmiany wpisane w trakcie zapisu idą w kolejnym,
 * - przy konflikcie wersji (409) autozapis się wstrzymuje, a użytkownik decyduje, co dalej,
 * - przy zamknięciu karty lub przełączeniu notatki niezapisane zmiany są wysyłane od razu.
 */
export function useNoteAutosave(note: Note, onSaved: (note: Note) => void) {
  const noteId = note.id
  const [status, setStatus] = useState<SaveStatus>('saved')
  const [lastSavedAt, setLastSavedAt] = useState(note.updatedAt)
  const [conflict, setConflict] = useState<Note | null>(null)

  const draftRef = useRef<NoteDraft>({ title: note.title, content: note.content })
  const versionRef = useRef(note.version)
  const dirtyRef = useRef(false)
  const savingRef = useRef(false)
  const blockedRef = useRef(false)
  const timerRef = useRef<number | undefined>(undefined)
  const onSavedRef = useRef(onSaved)
  const saveRef = useRef<() => Promise<void>>(() => Promise.resolve())

  useEffect(() => {
    onSavedRef.current = onSaved
  }, [onSaved])

  const schedule = useCallback((delay: number) => {
    window.clearTimeout(timerRef.current)
    timerRef.current = window.setTimeout(() => void saveRef.current(), delay)
  }, [])

  const save = useCallback(async () => {
    window.clearTimeout(timerRef.current)
    if (!dirtyRef.current || savingRef.current || blockedRef.current) return

    savingRef.current = true
    dirtyRef.current = false
    setStatus('saving')

    try {
      const saved = await updateNote(noteId, draftRef.current, versionRef.current)
      versionRef.current = saved.version
      setLastSavedAt(saved.updatedAt)
      onSavedRef.current(saved)

      if (dirtyRef.current) {
        setStatus('pending')
        schedule(AUTOSAVE_DELAY_MS)
      } else {
        setStatus('saved')
      }
    } catch (error) {
      dirtyRef.current = true
      if (isNoteConflict(error)) {
        blockedRef.current = true
        setConflict(error.current)
        setStatus('conflict')
      } else {
        setStatus('error')
        schedule(RETRY_DELAY_MS)
      }
    } finally {
      savingRef.current = false
    }
  }, [noteId, schedule])

  useEffect(() => {
    saveRef.current = save
  }, [save])

  /** Wywoływane przy każdej zmianie tytułu lub treści. */
  const markChanged = useCallback(
    (change: Partial<NoteDraft>) => {
      draftRef.current = { ...draftRef.current, ...change }
      dirtyRef.current = true
      if (blockedRef.current) return

      setStatus('pending')
      schedule(AUTOSAVE_DELAY_MS)
    },
    [schedule],
  )

  /** Natychmiastowy zapis, np. po Ctrl+S. */
  const saveNow = useCallback(() => {
    void save()
  }, [save])

  /** Rozwiązanie konfliktu: nadpisz wersję z serwera tym, co jest w edytorze. */
  const keepMine = useCallback(() => {
    if (conflict) versionRef.current = conflict.version
    setConflict(null)
    blockedRef.current = false
    dirtyRef.current = true
    void save()
  }, [conflict, save])

  useEffect(() => {
    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      if (blockedRef.current) {
        event.preventDefault()
        return
      }
      if (dirtyRef.current) {
        updateNoteOnExit(noteId, draftRef.current, versionRef.current)
        event.preventDefault()
      } else if (savingRef.current) {
        event.preventDefault()
      }
    }

    window.addEventListener('beforeunload', handleBeforeUnload)
    return () => {
      window.removeEventListener('beforeunload', handleBeforeUnload)
      window.clearTimeout(timerRef.current)
      // Przełączenie na inną notatkę: dosyłamy to, czego autozapis nie zdążył wysłać.
      if (dirtyRef.current && !blockedRef.current) {
        updateNoteOnExit(noteId, draftRef.current, versionRef.current)
      }
    }
  }, [noteId])

  return { status, lastSavedAt, conflict, markChanged, saveNow, keepMine }
}
