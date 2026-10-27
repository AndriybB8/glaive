import type { TaskStatus } from '../tasks/types'

/** Zadanie przypisane do notatki, z nazwą harmonogramu (tytuły zadań w różnych harmonogramach mogą się powtarzać). */
export interface LinkedTask {
  id: string
  scheduleId: string
  scheduleName: string
  title: string
  status: TaskStatus
  /** Dzień w formacie yyyy-MM-dd albo brak terminu */
  dueDate: string | null
}
