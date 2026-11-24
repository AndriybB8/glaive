import type { CalendarView } from './types'

/**
 * Daty kalendarza jako tekst "yyyy-MM-dd" w czasie lokalnym przeglądarki – tak samo jak w API.
 * Obliczenia na dniach robimy na lokalnej północy (new Date(y, m, d)), więc zmiana czasu letni/zimowy ich nie psuje.
 */

export function toIsoDate(date: Date): string {
  const y = date.getFullYear()
  const m = String(date.getMonth() + 1).padStart(2, '0')
  const d = String(date.getDate()).padStart(2, '0')
  return `${y}-${m}-${d}`
}

/** "yyyy-MM-dd" → lokalna północ tego dnia. */
export function fromIsoDate(iso: string): Date {
  const [y, m, d] = iso.split('-').map(Number)
  return new Date(y, m - 1, d)
}

export function addDays(iso: string, days: number): string {
  const date = fromIsoDate(iso)
  date.setDate(date.getDate() + days)
  return toIsoDate(date)
}

export function todayIso(): string {
  return toIsoDate(new Date())
}

/** Poniedziałek tygodnia, w którym jest dany dzień. */
export function startOfWeek(iso: string): string {
  const date = fromIsoDate(iso)
  const offset = (date.getDay() + 6) % 7 // pon = 0 … nd = 6
  return addDays(iso, -offset)
}

/** Kolejne dni od `from` do `to` (włącznie). */
export function daysBetween(from: string, to: string): string[] {
  const days: string[] = []
  for (let day = from; day <= to; day = addDays(day, 1)) days.push(day)
  return days
}

/** Widoczne dni widoku. Miesiąc to zawsze 6 pełnych tygodni (42 dni – mieści się w limicie API 62 dni). */
export function visibleRange(view: CalendarView, anchor: string): { from: string; to: string } {
  if (view === 'day') return { from: anchor, to: anchor }
  if (view === 'week') {
    const from = startOfWeek(anchor)
    return { from, to: addDays(from, 6) }
  }
  const first = toIsoDate(new Date(fromIsoDate(anchor).getFullYear(), fromIsoDate(anchor).getMonth(), 1))
  const from = startOfWeek(first)
  return { from, to: addDays(from, 41) }
}

/** Następny/poprzedni okres dla strzałek. */
export function shiftAnchor(view: CalendarView, anchor: string, direction: 1 | -1): string {
  if (view === 'day') return addDays(anchor, direction)
  if (view === 'week') return addDays(anchor, 7 * direction)
  const date = fromIsoDate(anchor)
  return toIsoDate(new Date(date.getFullYear(), date.getMonth() + direction, 1))
}

const monthFormat = new Intl.DateTimeFormat('pl-PL', { month: 'long', year: 'numeric' })
const dayFormat = new Intl.DateTimeFormat('pl-PL', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })
const shortFormat = new Intl.DateTimeFormat('pl-PL', { day: 'numeric', month: 'short' })
const shortYearFormat = new Intl.DateTimeFormat('pl-PL', { day: 'numeric', month: 'short', year: 'numeric' })

/** Tytuł nad kalendarzem: „listopad 2026”, „23–29 lis 2026”, „wtorek, 24 listopada 2026”. */
export function rangeTitle(view: CalendarView, anchor: string): string {
  if (view === 'month') {
    const text = monthFormat.format(fromIsoDate(anchor))
    return text.charAt(0).toUpperCase() + text.slice(1)
  }
  if (view === 'day') {
    const text = dayFormat.format(fromIsoDate(anchor))
    return text.charAt(0).toUpperCase() + text.slice(1)
  }
  const { from, to } = visibleRange('week', anchor)
  const start = fromIsoDate(from)
  const end = fromIsoDate(to)
  if (start.getMonth() === end.getMonth()) return `${start.getDate()}–${shortYearFormat.format(end)}`
  return `${shortFormat.format(start)} – ${shortYearFormat.format(end)}`
}

const timeFormat = new Intl.DateTimeFormat('pl-PL', { hour: '2-digit', minute: '2-digit' })

export function formatTime(isoDateTime: string): string {
  return timeFormat.format(new Date(isoDateTime))
}

/** Godzina w polu formularza (HH:mm) z momentu w UTC – w czasie lokalnym. */
export function toTimeInput(isoDateTime: string): string {
  const date = new Date(isoDateTime)
  return `${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`
}

/** Lokalny dzień i godzina z formularza → moment w UTC (ISO z „Z”), jak oczekuje API. */
export function toUtcIso(date: string, time: string): string {
  const [h, min] = time.split(':').map(Number)
  const local = fromIsoDate(date)
  local.setHours(h, min, 0, 0)
  return local.toISOString()
}

/** Minuty od lokalnej północy danego dnia (może wyjść poza 0–1440, gdy moment jest innego dnia). */
export function minutesFromDayStart(isoDateTime: string, day: string): number {
  return (new Date(isoDateTime).getTime() - fromIsoDate(day).getTime()) / 60000
}
