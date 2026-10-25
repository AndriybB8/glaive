const timeFormat = new Intl.DateTimeFormat('pl-PL', { hour: '2-digit', minute: '2-digit' })
const dateFormat = new Intl.DateTimeFormat('pl-PL', { day: 'numeric', month: 'short' })
const dateWithYearFormat = new Intl.DateTimeFormat('pl-PL', { day: 'numeric', month: 'short', year: 'numeric' })

/** Godzina dla dzisiejszych dat, w przeciwnym razie dzień i miesiąc (z rokiem, jeśli inny niż bieżący). */
export function formatUpdatedAt(iso: string): string {
  const date = new Date(iso)
  const now = new Date()

  if (date.toDateString() === now.toDateString()) return timeFormat.format(date)
  if (date.getFullYear() === now.getFullYear()) return dateFormat.format(date)
  return dateWithYearFormat.format(date)
}
