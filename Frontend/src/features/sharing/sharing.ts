import { request } from '../../lib/http'
import type { MemberRole, SharedAccess, UserLookup } from './types'

export const MEMBER_ROLE_OPTIONS: readonly { value: MemberRole; label: string }[] = [
  { value: 'viewer', label: 'Podgląd' },
  { value: 'editor', label: 'Edycja' },
]

export const ACCESS_LABELS: Record<SharedAccess, string> = {
  owner: 'Właściciel',
  editor: 'Edycja',
  viewer: 'Podgląd',
}

export function isMemberRole(value: string): value is MemberRole {
  return MEMBER_ROLE_OPTIONS.some((option) => option.value === value)
}

/** Ta sama prosta reguła co w rejestracji – resztę sprawdza serwer. */
export function looksLikeEmail(value: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)
}

/**
 * Wyszukanie aktywnego konta po dokładnym adresie email. 404, gdy nie ma takiego konta;
 * 429 po zbyt wielu próbach (limit na serwerze, bo odpowiedź zdradza, czy adres ma konto).
 */
export function lookupUser(email: string): Promise<UserLookup> {
  return request('/api/users/lookup', { method: 'POST', body: JSON.stringify({ email }) })
}
