export interface Schedule {
  id: string
  name: string
  /** Dzień w formacie yyyy-MM-dd (bez strefy czasowej) */
  startDate: string
  /** Ostatni dzień harmonogramu (włącznie), yyyy-MM-dd */
  endDate: string
  createdAt: string
  updatedAt: string
  version: number
}

export interface ScheduleDraft {
  name: string
  startDate: string
  endDate: string
}

export type ScheduleStatus = 'ongoing' | 'upcoming' | 'finished'
