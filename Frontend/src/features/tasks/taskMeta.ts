import type { Task, TaskPriority, TaskStatus } from './types'

export const priorityOptions: readonly { value: TaskPriority; label: string }[] = [
  { value: 'high', label: 'Wysoki' },
  { value: 'medium', label: 'Średni' },
  { value: 'low', label: 'Niski' },
]

export const statusOptions: readonly { value: TaskStatus; label: string }[] = [
  { value: 'toDo', label: 'Do zrobienia' },
  { value: 'inProgress', label: 'W trakcie' },
  { value: 'done', label: 'Zrobione' },
]

export const priorityBadges = {
  high: { tone: 'blue', label: 'Wysoki priorytet' },
  medium: { tone: 'neutral', label: 'Średni priorytet' },
  low: { tone: 'muted', label: 'Niski priorytet' },
} as const

const priorityRank: Record<TaskPriority, number> = { high: 0, medium: 1, low: 2 }

/** Termin minął, a zadanie nie jest zrobione. Daty yyyy-MM-dd porównujemy jako tekst. */
export function isOverdue(task: Task, today: string): boolean {
  return task.status !== 'done' && task.dueDate !== null && task.dueDate < today
}

/** Wyższy priorytet wyżej; przy równym – najbliższy termin (bez terminu na końcu), potem kolejność dodania. */
function compareTasks(a: Task, b: Task): number {
  const priorityDiff = priorityRank[a.priority] - priorityRank[b.priority]
  if (priorityDiff !== 0) return priorityDiff
  if (a.dueDate !== b.dueDate) {
    if (a.dueDate === null) return 1
    if (b.dueDate === null) return -1
    return a.dueDate.localeCompare(b.dueDate)
  }
  return a.createdAt.localeCompare(b.createdAt)
}

export interface TaskGroup {
  status: TaskStatus
  label: string
  tasks: Task[]
}

/** Grupy w kolejności pracy: do zrobienia → w trakcie → zrobione. Puste grupy pomijamy. */
export function groupTasks(tasks: Task[]): TaskGroup[] {
  return statusOptions
    .map(({ value, label }) => ({
      status: value,
      label,
      tasks: tasks.filter((task) => task.status === value).sort(compareTasks),
    }))
    .filter((group) => group.tasks.length > 0)
}
