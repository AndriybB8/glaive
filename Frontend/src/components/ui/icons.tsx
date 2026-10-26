import type { ReactNode } from 'react'

/** Ikony rysowane na siatce 20×20, sam obrys w kolorze tekstu (jak w EditorToolbar). */
function Icon({ children }: { children: ReactNode }) {
  return (
    <svg viewBox="0 0 20 20" className="h-[18px] w-[18px]" fill="none" stroke="currentColor" strokeWidth={1.6} strokeLinecap="round" strokeLinejoin="round" aria-hidden>
      {children}
    </svg>
  )
}

export function PencilIcon() {
  return (
    <Icon>
      <path d="M13.5 3.5l3 3L7 16H4v-3z" />
      <path d="M11.5 5.5l3 3" />
    </Icon>
  )
}

export function TrashIcon() {
  return (
    <Icon>
      <path d="M3.5 5.5h13" />
      <path d="M8 5.5V4a1 1 0 0 1 1-1h2a1 1 0 0 1 1 1v1.5" />
      <path d="M5 5.5l.8 10.1a1.5 1.5 0 0 0 1.5 1.4h5.4a1.5 1.5 0 0 0 1.5-1.4L15 5.5" />
    </Icon>
  )
}

export function CloseIcon() {
  return (
    <Icon>
      <path d="m5 5 10 10M15 5 5 15" />
    </Icon>
  )
}

export function PlusIcon() {
  return (
    <Icon>
      <path d="M10 4.5v11M4.5 10h11" />
    </Icon>
  )
}

/** Grupa notatek (folder). */
export function FolderIcon() {
  return (
    <Icon>
      <path d="M3 6a1.5 1.5 0 0 1 1.5-1.5h3.2l1.6 1.8h6.2A1.5 1.5 0 0 1 17 7.8v6.7a1.5 1.5 0 0 1-1.5 1.5h-11A1.5 1.5 0 0 1 3 14.5z" />
    </Icon>
  )
}

/** Wszystkie notatki. */
export function NotesIcon() {
  return (
    <Icon>
      <rect x="4.5" y="3" width="11" height="14" rx="1.5" />
      <path d="M7.5 7h5M7.5 10h5M7.5 13h3" />
    </Icon>
  )
}

/** Notatki bez grupy. */
export function TrayIcon() {
  return (
    <Icon>
      <path d="M3 11.5l2-6.5a1.5 1.5 0 0 1 1.4-1h7.2a1.5 1.5 0 0 1 1.4 1l2 6.5" />
      <path d="M3 11.5V15a1.5 1.5 0 0 0 1.5 1.5h11A1.5 1.5 0 0 0 17 15v-3.5h-4l-1 1.5H8l-1-1.5z" />
    </Icon>
  )
}
