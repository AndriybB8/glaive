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

/** Uchwyt do przeciągania (sześć kropek). */
export function GripIcon() {
  return (
    <Icon>
      <circle cx="7.5" cy="5" r="0.9" fill="currentColor" />
      <circle cx="12.5" cy="5" r="0.9" fill="currentColor" />
      <circle cx="7.5" cy="10" r="0.9" fill="currentColor" />
      <circle cx="12.5" cy="10" r="0.9" fill="currentColor" />
      <circle cx="7.5" cy="15" r="0.9" fill="currentColor" />
      <circle cx="12.5" cy="15" r="0.9" fill="currentColor" />
    </Icon>
  )
}

export function ChevronUpIcon() {
  return (
    <Icon>
      <path d="m5.5 12.5 4.5-4.5 4.5 4.5" />
    </Icon>
  )
}

export function ChevronDownIcon() {
  return (
    <Icon>
      <path d="m5.5 7.5 4.5 4.5 4.5-4.5" />
    </Icon>
  )
}

/** Lista z punktami do odhaczenia. */
export function ChecklistIcon() {
  return (
    <Icon>
      <path d="m3.5 5.5 1.3 1.3 2.2-2.6" />
      <path d="m3.5 12.5 1.3 1.3 2.2-2.6" />
      <path d="M9.5 6h7M9.5 13h7" />
    </Icon>
  )
}

/** Dwie osoby – lista udostępniona innym. */
export function UsersIcon() {
  return (
    <Icon>
      <circle cx="7.5" cy="7" r="2.5" />
      <path d="M3 16c0-2.5 2-4.5 4.5-4.5S12 13.5 12 16" />
      <path d="M13 4.8a2.5 2.5 0 0 1 0 4.4" />
      <path d="M14.5 11.8c1.5.6 2.5 2.2 2.5 4.2" />
    </Icon>
  )
}

/** Osoba z plusem – udostępnianie (zaproszenie kolejnej osoby). */
export function UserPlusIcon() {
  return (
    <Icon>
      <circle cx="8" cy="7" r="2.5" />
      <path d="M3.5 16c0-2.5 2-4.5 4.5-4.5s4.5 2 4.5 4.5" />
      <path d="M15.5 6v5M13 8.5h5" />
    </Icon>
  )
}

export function DownloadIcon() {
  return (
    <Icon>
      <path d="M10 3.5v9" />
      <path d="M6.5 9 10 12.5 13.5 9" />
      <path d="M4 14.5v2h12v-2" />
    </Icon>
  )
}
