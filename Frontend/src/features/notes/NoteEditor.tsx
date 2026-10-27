import { EditorContent, useEditor, type Editor } from '@tiptap/react'
import { useEffect, useRef } from 'react'
import { EditorToolbar } from './EditorToolbar'
import { createNoteExtensions } from './noteExtensions'
import './note-editor.css'

interface NoteEditorProps {
  initialContent: string
  onChange: (html: string) => void
  onSaveShortcut: () => void
  onReady?: (editor: Editor) => void
}

/**
 * Edytor treści notatki (TipTap). Komponent niekontrolowany: treść podajemy raz,
 * a zmiany wychodzą przez onChange jako HTML.
 * Przy zmianie notatki komponent jest montowany od nowa (key w rodzicu).
 */
export function NoteEditor({ initialContent, onChange, onSaveShortcut, onReady }: NoteEditorProps) {
  const onChangeRef = useRef(onChange)
  const onSaveShortcutRef = useRef(onSaveShortcut)

  useEffect(() => {
    onChangeRef.current = onChange
    onSaveShortcutRef.current = onSaveShortcut
  }, [onChange, onSaveShortcut])

  const editor = useEditor({
    extensions: createNoteExtensions('Zacznij pisać notatkę…'),
    content: initialContent,
    editorProps: {
      attributes: {
        class: 'note-content',
        'aria-label': 'Treść notatki',
      },
      handleKeyDown: (_view, event) => {
        if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's') {
          event.preventDefault()
          onSaveShortcutRef.current()
          return true
        }
        return false
      },
    },
    onUpdate: ({ editor: e }) => {
      onChangeRef.current(e.isEmpty ? '' : e.getHTML())
    },
  })

  useEffect(() => {
    if (editor) onReady?.(editor)
  }, [editor, onReady])

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <div className="sticky top-0 z-10 -mx-2 border-b border-slate-200 bg-white/95 px-2 py-1.5 backdrop-blur">
        <EditorToolbar editor={editor} />
      </div>
      <EditorContent editor={editor} className="flex-1 py-5" />
    </div>
  )
}
