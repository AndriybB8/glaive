/**
 * Wspólne typy udostępniania – harmonogramy i listy ToDo (backend: AccessRole).
 * `owner` – wszystko, `editor` – zmiany w zawartości, `viewer` – tylko podgląd. Co dokładnie wolno, ustala `can()` w permissions.ts.
 */
export type SharedAccess = 'owner' | 'editor' | 'viewer'

/** Uprawnienia nadawane przy zapraszaniu. Właściciela nie da się nadać. */
export type MemberRole = 'viewer' | 'editor'

/** Osoba z dostępem albo z zaproszeniem, na które jeszcze nie odpowiedziała (`acceptedAt` = null). */
export interface Member {
  userId: string
  email: string
  role: MemberRole
  createdAt: string
  acceptedAt: string | null
}

/** Zaproszenie bieżącego użytkownika – w kształcie wspólnym dla panelu zaproszeń. */
export interface Invitation {
  id: string
  title: string
  ownerEmail: string
  role: MemberRole
}

/** Wynik wyszukania konta po adresie email (POST /api/users/lookup). */
export interface UserLookup {
  id: string
  email: string
}

/** Wywołania API dla jednej udostępnianej rzeczy (np. harmonogramu) – okno udostępniania nie zna adresów. */
export interface MembersApi {
  getMembers: () => Promise<Member[]>
  invite: (email: string, role: MemberRole) => Promise<Member>
  changeRole: (userId: string, role: MemberRole) => Promise<Member>
  remove: (userId: string) => Promise<void>
}
