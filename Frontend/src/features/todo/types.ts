/** Lista na liście list – z licznikami do paska postępu („3 z 7”). */
export interface TodoListSummary {
  id: string
  title: string
  itemCount: number
  doneCount: number
  createdAt: string
  updatedAt: string
  version: number
}

/** Lista bez liczników – odpowiedź na utworzenie i zmianę nazwy. */
export interface TodoList {
  id: string
  title: string
  createdAt: string
  updatedAt: string
  version: number
}

/** Punkt listy. Kolejność na liście wyznacza `position` (od 0). */
export interface TodoItem {
  id: string
  listId: string
  title: string
  isDone: boolean
  completedAt: string | null
  position: number
  createdAt: string
  updatedAt: string
  version: number
}
