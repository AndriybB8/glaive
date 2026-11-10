const STORAGE_KEY = 'SP.token'

export function getToken() : string | null {
  return localStorage.getItem(STORAGE_KEY)
}

export function setToken(token: string) : void {
  localStorage.setItem(STORAGE_KEY, token)
}

export function clearToken() : void {
  localStorage.removeItem(STORAGE_KEY)
}

/** Zalogowany użytkownik odczytany z tokenu (JWT: `sub` i `email`). */
export interface TokenUser {
  id: string
  email: string
}

/**
 * Dane zalogowanego użytkownika z tokenu – bez zapytania do serwera. Tylko do wyświetlania i wskazania „siebie”
 * (np. opuszczenie udostępnionej listy); uprawnienia i tak sprawdza backend. Null, gdy brak tokenu albo jest nieczytelny.
 */
export function getCurrentUser(): TokenUser | null {
  const token = getToken()
  const payload = token?.split('.')[1]
  if (!payload) return null

  try {
    // Base64url → base64, potem bajty → UTF-8 (adres może mieć polskie znaki).
    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(payload.length / 4) * 4, '=')
    const bytes = Uint8Array.from(atob(base64), (char) => char.charCodeAt(0))
    const claims = JSON.parse(new TextDecoder().decode(bytes)) as Record<string, unknown>
    return typeof claims.sub === 'string' && typeof claims.email === 'string' ? { id: claims.sub, email: claims.email } : null
  } catch {
    return null
  }
}
