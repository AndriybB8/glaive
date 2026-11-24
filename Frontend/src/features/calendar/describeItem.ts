import { formatTime } from './dates'
import type { CalendarItem } from './types'

const statusLabels = { toDo: 'do zrobienia', inProgress: 'w trakcie', done: 'zrobione' } as const

/** Pełny opis elementu dla czytnika ekranu i podpowiedzi po najechaniu. */
export function describeItem(item: CalendarItem): string {
  if (item.kind === 'schedule') return `Harmonogram: ${item.title}`
  if (item.kind === 'task') {
    const status = item.taskStatus ? `, ${statusLabels[item.taskStatus]}` : ''
    return `Zadanie: ${item.title}${item.scheduleName ? ` (${item.scheduleName})` : ''}${status}`
  }
  const time = item.isAllDay ? 'cały dzień' : `${formatTime(item.startsAt!)}–${formatTime(item.endsAt!)}`
  return `${item.title}, ${time}${item.isRecurring ? ', powtarzane' : ''}`
}
