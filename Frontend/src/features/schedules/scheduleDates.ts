import type { Schedule, ScheduleStatus } from './types'

// Daty harmonogramu to dni kalendarzowe "yyyy-MM-dd". Porównujemy je jako tekst
// (ten format sortuje się poprawnie) i zamieniamy na Date tylko do wyświetlenia.

const rangeFormat = new Intl.DateTimeFormat('pl-PL', { day: 'numeric', month: 'short', year: 'numeric' })
const MS_PER_DAY = 24 * 60 * 60 * 1000

function parts(isoDate: string): [number, number, number] {
  const [year, month, day] = isoDate.split('-').map(Number)
  return [year, month - 1, day]
}

/** Dzisiejszy dzień w strefie czasowej użytkownika, yyyy-MM-dd. */
export function todayIso(now = new Date()): string {
  const month = String(now.getMonth() + 1).padStart(2, '0')
  const day = String(now.getDate()).padStart(2, '0')
  return `${now.getFullYear()}-${month}-${day}`
}

export function getStatus(schedule: Pick<Schedule, 'startDate' | 'endDate'>, today: string): ScheduleStatus {
  if (today < schedule.startDate) return 'upcoming'
  if (today > schedule.endDate) return 'finished'
  return 'ongoing'
}

/** Liczba dni w zakresie, wliczając pierwszy i ostatni. */
export function countDays(startDate: string, endDate: string): number {
  return Math.round((Date.UTC(...parts(endDate)) - Date.UTC(...parts(startDate))) / MS_PER_DAY) + 1
}

export function formatDays(days: number): string {
  return days === 1 ? '1 dzień' : `${days} dni`
}

/** Np. „3 lis 2026”. */
export function formatDate(isoDate: string): string {
  return rangeFormat.format(new Date(...parts(isoDate)))
}

/** Np. „6–12 paź 2026”, „20 paź – 2 lis 2026”, „1 paź 2026 – 1 lut 2027”. */
export function formatDateRange(startDate: string, endDate: string): string {
  return rangeFormat.formatRange(new Date(...parts(startDate)), new Date(...parts(endDate)))
}

const statusOrder: Record<ScheduleStatus, number> = { ongoing: 0, upcoming: 1, finished: 2 }

/** Najpierw trwające, potem nadchodzące (od najbliższego), na końcu zakończone (od najświeższego). */
export function sortSchedules(schedules: Schedule[], today: string): Schedule[] {
  return [...schedules].sort((a, b) => {
    const statusA = getStatus(a, today)
    const statusB = getStatus(b, today)
    if (statusA !== statusB) return statusOrder[statusA] - statusOrder[statusB]
    if (statusA === 'finished') return b.endDate.localeCompare(a.endDate)
    return a.startDate.localeCompare(b.startDate) || a.endDate.localeCompare(b.endDate)
  })
}
