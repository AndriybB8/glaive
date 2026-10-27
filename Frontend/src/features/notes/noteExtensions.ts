import StarterKit from '@tiptap/starter-kit'
import { Placeholder } from '@tiptap/extensions'
import { TaskItem, TaskList } from '@tiptap/extension-list'

/**
 * Rozszerzenia TipTap wspólne dla edytora i podglądu notatki – oba muszą rozumieć ten sam HTML.
 * Placeholder tylko w edytorze (podgląd jest do odczytu).
 */
export function createNoteExtensions(placeholder?: string) {
  return [
    StarterKit.configure({
      heading: { levels: [2, 3] },
      link: { openOnClick: false, autolink: true },
    }),
    TaskList,
    TaskItem.configure({ nested: true }),
    ...(placeholder ? [Placeholder.configure({ placeholder })] : []),
  ]
}
