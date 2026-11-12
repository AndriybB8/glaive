import type { SharedAccess } from '../sharing/types'

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
  /**
   * Dostęp bieżącego użytkownika: `owner` – własny, `editor` / `viewer` – wspólny (po przyjęciu zaproszenia).
   * Nazwę, daty i udostępnianie zmienia tylko właściciel; zadania – właściciel i `editor`.
   */
  access: SharedAccess
}

export interface ScheduleDraft {
  name: string
  startDate: string
  endDate: string
}

export type ScheduleStatus = 'ongoing' | 'upcoming' | 'finished'

/** Zaproszenie bieżącego użytkownika do cudzego harmonogramu (GET /api/schedule-invitations). */
export interface ScheduleInvitation {
  scheduleId: string
  scheduleName: string
  ownerEmail: string
  role: 'viewer' | 'editor'
  invitedAt: string
}
