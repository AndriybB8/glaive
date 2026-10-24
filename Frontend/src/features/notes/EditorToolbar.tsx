import { useEditorState, type Editor } from '@tiptap/react'
import type { ReactNode } from 'react'

interface ToolbarButtonProps {
  label: string
  shortcut?: string
  active?: boolean
  disabled?: boolean
  onClick: () => void
  children: ReactNode
}

function ToolbarButton({ label, shortcut, active = false, disabled = false, onClick, children }: ToolbarButtonProps) {
  return (
    <button
      type="button"
      // onMouseDown zamiast onClick nie zabiera fokusu z edytora
      onMouseDown={(event) => event.preventDefault()}
      onClick={onClick}
      disabled={disabled}
      aria-label={label}
      aria-pressed={active}
      title={shortcut ? `${label} (${shortcut})` : label}
      className={`flex h-8 min-w-8 items-center justify-center rounded-md px-1.5 text-sm transition-colors focus-visible:outline-2 focus-visible:outline-blue-600 disabled:cursor-not-allowed disabled:opacity-35 ${
        active ? 'bg-blue-100 text-blue-800' : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900'
      }`}
    >
      {children}
    </button>
  )
}

function Divider() {
  return <span aria-hidden className="mx-1 h-5 w-px bg-slate-200" />
}

function Icon({ children }: { children: ReactNode }) {
  return (
    <svg viewBox="0 0 20 20" className="h-[18px] w-[18px]" fill="none" stroke="currentColor" strokeWidth={1.6} strokeLinecap="round" strokeLinejoin="round" aria-hidden>
      {children}
    </svg>
  )
}

export function EditorToolbar({ editor }: { editor: Editor }) {
  const state = useEditorState({
    editor,
    selector: ({ editor: e }) => ({
      bold: e.isActive('bold'),
      italic: e.isActive('italic'),
      underline: e.isActive('underline'),
      strike: e.isActive('strike'),
      heading2: e.isActive('heading', { level: 2 }),
      heading3: e.isActive('heading', { level: 3 }),
      bulletList: e.isActive('bulletList'),
      orderedList: e.isActive('orderedList'),
      taskList: e.isActive('taskList'),
      blockquote: e.isActive('blockquote'),
      canUndo: e.can().undo(),
      canRedo: e.can().redo(),
    }),
  })

  const chain = () => editor.chain().focus()

  return (
    <div role="toolbar" aria-label="Formatowanie tekstu" className="flex flex-wrap items-center gap-0.5">
      <ToolbarButton label="Pogrubienie" shortcut="Ctrl+B" active={state.bold} onClick={() => chain().toggleBold().run()}>
        <span className="font-bold">B</span>
      </ToolbarButton>
      <ToolbarButton label="Kursywa" shortcut="Ctrl+I" active={state.italic} onClick={() => chain().toggleItalic().run()}>
        <span className="font-serif italic">I</span>
      </ToolbarButton>
      <ToolbarButton label="Podkreślenie" shortcut="Ctrl+U" active={state.underline} onClick={() => chain().toggleUnderline().run()}>
        <span className="underline underline-offset-2">U</span>
      </ToolbarButton>
      <ToolbarButton label="Przekreślenie" shortcut="Ctrl+Shift+S" active={state.strike} onClick={() => chain().toggleStrike().run()}>
        <span className="line-through">S</span>
      </ToolbarButton>

      <Divider />

      <ToolbarButton label="Nagłówek" shortcut="Ctrl+Alt+2" active={state.heading2} onClick={() => chain().toggleHeading({ level: 2 }).run()}>
        <span className="font-semibold">H1</span>
      </ToolbarButton>
      <ToolbarButton label="Śródtytuł" shortcut="Ctrl+Alt+3" active={state.heading3} onClick={() => chain().toggleHeading({ level: 3 }).run()}>
        <span className="font-semibold">H2</span>
      </ToolbarButton>

      <Divider />

      <ToolbarButton label="Lista punktowana" shortcut="Ctrl+Shift+8" active={state.bulletList} onClick={() => chain().toggleBulletList().run()}>
        <Icon>
          <circle cx="4" cy="5.5" r="0.9" fill="currentColor" stroke="none" />
          <circle cx="4" cy="10" r="0.9" fill="currentColor" stroke="none" />
          <circle cx="4" cy="14.5" r="0.9" fill="currentColor" stroke="none" />
          <path d="M8 5.5h9M8 10h9M8 14.5h9" />
        </Icon>
      </ToolbarButton>
      <ToolbarButton label="Lista numerowana" shortcut="Ctrl+Shift+7" active={state.orderedList} onClick={() => chain().toggleOrderedList().run()}>
        <Icon>
          <path d="M3 4.5h1.2v3M3 7.5h2.2M3 12.2c.3-.6 1.9-.7 1.9.3 0 .8-1.9 1.4-1.9 2.4h2" />
          <path d="M8 5.5h9M8 10h9M8 14.5h9" />
        </Icon>
      </ToolbarButton>
      <ToolbarButton label="Lista zadań" shortcut="Ctrl+Shift+9" active={state.taskList} onClick={() => chain().toggleTaskList().run()}>
        <Icon>
          <rect x="2.5" y="3.5" width="4" height="4" rx="1" />
          <path d="m3.4 13.4 1 1 1.8-2" />
          <rect x="2.5" y="11.5" width="4" height="4" rx="1" />
          <path d="M9.5 5.5h7.5M9.5 13.5h7.5" />
        </Icon>
      </ToolbarButton>
      <ToolbarButton label="Cytat" shortcut="Ctrl+Shift+B" active={state.blockquote} onClick={() => chain().toggleBlockquote().run()}>
        <Icon>
          <path d="M4 4.5v11M8 6h9M8 10h9M8 14h6" />
        </Icon>
      </ToolbarButton>

      <Divider />

      <ToolbarButton label="Cofnij" shortcut="Ctrl+Z" disabled={!state.canUndo} onClick={() => chain().undo().run()}>
        <Icon>
          <path d="M7 5 3.5 8.5 7 12" />
          <path d="M3.5 8.5H12a4.5 4.5 0 0 1 0 9h-2" />
        </Icon>
      </ToolbarButton>
      <ToolbarButton label="Ponów" shortcut="Ctrl+Shift+Z" disabled={!state.canRedo} onClick={() => chain().redo().run()}>
        <Icon>
          <path d="m13 5 3.5 3.5L13 12" />
          <path d="M16.5 8.5H8a4.5 4.5 0 0 0 0 9h2" />
        </Icon>
      </ToolbarButton>
    </div>
  )
}
