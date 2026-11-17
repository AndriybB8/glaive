import type { SharedAccess } from './types'

/**
 * Co można zrobić z udostępnianym zasobem (harmonogramem, listą). Odpowiednik `Permission` z backendu
 * (StudyPlanner.Application/Common/AccessRole.cs) – te same nazwy i znaczenie.
 * - `view` – odczyt zasobu i jego zawartości,
 * - `editContent` – zmiany zawartości: zadania albo punkty listy, ich status, kolejność i tagi,
 * - `manage` – zmiana nazwy (i dat), usunięcie oraz zarządzanie osobami z dostępem.
 */
export type Permission = 'view' | 'editContent' | 'manage'

/**
 * Jedyne miejsce na froncie, które przypisuje uprawnienia do ról – ta sama macierz co `AccessRoleExtensions.Can`.
 * Widok pokazuje tylko akcje, na które rola pozwala; serwer i tak sprawdza je ponownie (403 = rola zmieniła się w międzyczasie).
 * Wyjątek poza macierzą (jak na backendzie): każda osoba z dostępem, która nie jest właścicielem, może sama zrezygnować.
 */
export function can(access: SharedAccess, permission: Permission): boolean {
  switch (permission) {
    case 'view':
      return true
    case 'editContent':
      return access === 'editor' || access === 'owner'
    case 'manage':
      return access === 'owner'
  }
}

/** Czy bieżący użytkownik może opuścić zasób – tylko cudzy (właściciel zasób usuwa, a nie opuszcza). */
export function canLeave(access: SharedAccess): boolean {
  return access !== 'owner'
}
