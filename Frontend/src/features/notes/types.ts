export interface Note {
  id: string
  title: string
  /** HTML z edytora TipTap */
  content: string
  /** Grupa (folder) notatki; null = „Bez grupy”. */
  groupId: string | null
  createdAt: string
  updatedAt: string
  version: number
}

export interface NoteSummary {
  id: string
  title: string
  updatedAt: string
  groupId: string | null
}

export interface NoteDraft {
  title: string
  content: string
}

/** Grupa (folder) notatek. Notatka należy do jednej grupy albo do żadnej. */
export interface NoteGroup {
  id: string
  name: string
  createdAt: string
  updatedAt: string
  version: number
}

/** Co pokazuje lista notatek: wszystkie, tylko te bez grupy albo jedną grupę. */
export type NoteGroupFilter = { kind: 'all' } | { kind: 'ungrouped' } | { kind: 'group'; groupId: string }
