export interface Tag {
  id: string
  name: string
  /** Kolor w formacie #RRGGBB */
  color: string
  createdAt: string
  updatedAt: string
  version: number
}

export interface TagDraft {
  name: string
  color: string
}
