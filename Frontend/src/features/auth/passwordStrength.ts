export type PasswordStrength = 'weak' | 'medium' | 'strong'

export function getPasswordStrength(password: string) {
  if (password.length === 0) return 'weak'

  let s = 0
  if (password.length >= 0) s++
  if (password.length >= 8) s++
  if (password.length >= 12) s++
  if (/[a-z]/.test(password)) s++
  if (/[A-Z]/.test(password)) s++
  if (/[0-9]/.test(password)) s++
  if (/[^a-zA-Z0-9]/.test(password)) s++

  if (s <= 2) return 'weak'
  if (s <= 4) return 'medium'
  return 'strong'
}
