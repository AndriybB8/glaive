import type { TaskStatus } from '../tasks/types'

export const statusBadges = {
  toDo: { tone: 'neutral', label: 'Do zrobienia' },
  inProgress: { tone: 'blue', label: 'W trakcie' },
  done: { tone: 'muted', label: 'Zrobione' },
} as const satisfies Record<TaskStatus, { tone: 'neutral' | 'blue' | 'muted'; label: string }>
