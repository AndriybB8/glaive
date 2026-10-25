export interface Note {
  id: string
  title: string
  /** HTML z edytora TipTap */
  content: string
  createdAt: string
  updatedAt: string
  version: number
}

export interface NoteSummary {
  id: string
  title: string
  updatedAt: string
}

export interface NoteDraft {
  title: string
  content: string
}
