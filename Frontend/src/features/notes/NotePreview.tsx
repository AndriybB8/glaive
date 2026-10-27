import { EditorContent, useEditor } from '@tiptap/react'
import { createNoteExtensions } from './noteExtensions'
import './note-editor.css'

/**
 * Treść notatki tylko do odczytu, sformatowana jak w edytorze.
 * Używa TipTap zamiast dangerouslySetInnerHTML: edytor przepuszcza tylko znane elementy, więc HTML z bazy
 * nie może wstrzyknąć skryptu.
 */
export function NotePreview({ content }: { content: string }) {
  const editor = useEditor({
    extensions: createNoteExtensions(),
    content,
    editable: false,
    editorProps: { attributes: { class: 'note-content note-preview' } },
  })

  if (!content) {
    return <p className="text-sm text-slate-500 italic">Ta notatka jest pusta.</p>
  }

  return <EditorContent editor={editor} />
}
