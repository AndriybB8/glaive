export type TaskPriority = 'low' | 'medium' | 'high'

export type TaskStatus = 'toDo' | 'inProgress' | 'done'

export interface Task {
  id: string
  scheduleId: string
  title: string
  /** Opcjonalny opis (zwykły tekst) */
  description: string | null
  /** Dzień w formacie yyyy-MM-dd albo brak terminu */
  dueDate: string | null
  priority: TaskPriority
  status: TaskStatus
  createdAt: string
  updatedAt: string
  version: number
}

export interface TaskDraft {
  title: string
  /** W formularzu pusty tekst = brak opisu (serwer zapisze null). */
  description: string
  dueDate: string | null
  priority: TaskPriority
  status: TaskStatus
}
