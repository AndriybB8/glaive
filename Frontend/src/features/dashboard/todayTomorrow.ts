import { addDays } from '../calendar/dates'
import type { CalendarItem } from '../calendar/types'
import type { Schedule } from '../schedules/types'
import { can } from '../sharing/permissions'
import type { Task, TaskPriority } from '../tasks/types'

/** Zadanie z terminem na dany dzień, z nazwą harmonogramu i informacją, czy wolno je odhaczyć. */
export interface DayTask {
  task: Task
  scheduleName: string | null
  canComplete: boolean
}

export interface DayPlan {
  /** yyyy-MM-dd w czasie lokalnym. */
  date: string
  label: 'Dziś' | 'Jutro'
  /** Wydarzenia z kalendarza (zajęcia, spotkania): całodniowe najpierw, potem od najwcześniejszego. */
  events: CalendarItem[]
  tasks: DayTask[]
}

const priorityRank: Record<TaskPriority, number> = { high: 0, medium: 1, low: 2 }

function localDate(isoDateTime: string): string {
  const d = new Date(isoDateTime)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

/** Czy wydarzenie trwa w danym dniu: całodniowe – dzień w zakresie; w godzinach – przecina ten dzień (także przez północ). */
export function eventOnDay(item: CalendarItem, day: string): boolean {
  if (item.isAllDay) {
    return item.startDate !== null && item.endDate !== null && item.startDate <= day && day <= item.endDate
  }
  if (!item.startsAt || !item.endsAt) return false
  const start = localDate(item.startsAt)
  // Koniec dokładnie o północy nie zahacza o następny dzień.
  const endMoment = new Date(new Date(item.endsAt).getTime() - 1).toISOString()
  const end = localDate(endMoment)
  return start <= day && day <= end
}

function compareEvents(a: CalendarItem, b: CalendarItem): number {
  if (a.isAllDay !== b.isAllDay) return a.isAllDay ? -1 : 1
  return (a.startsAt ?? '').localeCompare(b.startsAt ?? '') || a.title.localeCompare(b.title, 'pl')
}

/**
 * Plan na dziś i jutro z trzech źródeł: wydarzeń z kalendarza (tylko `kind: 'event'` – harmonogramy i zadania z kalendarza
 * pomijamy, zadania bierzemy z /api/tasks/upcoming), nadchodzących zadań i harmonogramów (nazwa, rola).
 */
export function buildDays(today: string, calendarItems: CalendarItem[], upcoming: Task[], schedules: Schedule[]): DayPlan[] {
  const scheduleById = new Map(schedules.map((s) => [s.id, s]))
  const events = calendarItems.filter((item) => item.kind === 'event')

  return [
    { date: today, label: 'Dziś' as const },
    { date: addDays(today, 1), label: 'Jutro' as const },
  ].map(({ date, label }) => ({
    date,
    label,
    events: events.filter((e) => eventOnDay(e, date)).sort(compareEvents),
    tasks: upcoming
      .filter((t) => t.dueDate === date && t.status !== 'done')
      .sort((a, b) => priorityRank[a.priority] - priorityRank[b.priority] || a.title.localeCompare(b.title, 'pl'))
      .map((task) => {
        const schedule = scheduleById.get(task.scheduleId)
        return {
          task,
          scheduleName: schedule?.name ?? null,
          canComplete: schedule ? can(schedule.access, 'editContent') : false,
        }
      }),
  }))
}

const dayFormat = new Intl.DateTimeFormat('pl-PL', { weekday: 'long', day: 'numeric', month: 'long' })

/** „wtorek, 24 listopada”. */
export function formatDay(iso: string): string {
  const [y, m, d] = iso.split('-').map(Number)
  return dayFormat.format(new Date(y, m - 1, d))
}

/** Polska odmiana: 1 zadanie, 2–4 zadania, 5+ zadań (12–14 zadań). */
export function plural(n: number, one: string, few: string, many: string): string {
  if (n === 1) return one
  const lastTwo = n % 100
  const last = n % 10
  return last >= 2 && last <= 4 && !(lastTwo >= 12 && lastTwo <= 14) ? few : many
}
