import { ConflictError, request } from '../../lib/http'
import type { TodoItem, TodoList, TodoListSummary } from './types'

const LISTS_URL = '/api/todo-lists'
const ITEMS_URL = '/api/todo-items'

/** Limity jak w backendzie (TodoList.TitleMaxLength, TodoItem.TitleMaxLength). */
export const TODO_LIST_TITLE_MAX_LENGTH = 100
export const TODO_ITEM_TITLE_MAX_LENGTH = 200

/** Listę zmieniono w innym miejscu (np. w drugiej karcie). */
export function isTodoListConflict(error: unknown): error is ConflictError<TodoList> {
  return error instanceof ConflictError
}

/** Punkt zmieniono w innym miejscu (np. w drugiej karcie). */
export function isTodoItemConflict(error: unknown): error is ConflictError<TodoItem> {
  return error instanceof ConflictError
}

/** Listy użytkownika z licznikami, alfabetycznie (sortuje backend). */
export function getTodoLists(): Promise<TodoListSummary[]> {
  return request(LISTS_URL)
}

export function createTodoList(title: string): Promise<TodoList> {
  return request(LISTS_URL, { method: 'POST', body: JSON.stringify({ title }) })
}

export function renameTodoList(id: string, title: string, version: number): Promise<TodoList> {
  return request(`${LISTS_URL}/${id}`, { method: 'PUT', body: JSON.stringify({ title, version }) })
}

/** Usuwa listę razem z jej punktami. */
export function deleteTodoList(id: string): Promise<void> {
  return request(`${LISTS_URL}/${id}`, { method: 'DELETE' })
}

/** Punkty listy w kolejności na liście (sortuje backend). */
export function getTodoItems(listId: string): Promise<TodoItem[]> {
  return request(`${LISTS_URL}/${listId}/items`)
}

/** Nowy punkt – niezrobiony, na końcu listy. */
export function createTodoItem(listId: string, title: string): Promise<TodoItem> {
  return request(`${LISTS_URL}/${listId}/items`, { method: 'POST', body: JSON.stringify({ title }) })
}

/** Edycja treści punktu (z kontrolą wersji). */
export function updateTodoItem(item: TodoItem, title: string): Promise<TodoItem> {
  return request(`${ITEMS_URL}/${item.id}`, {
    method: 'PUT',
    body: JSON.stringify({ title, isDone: item.isDone, version: item.version }),
  })
}

/** Odhaczenie / odznaczenie punktu – bez kontroli wersji (wygrywa ostatnia zmiana). */
export function setTodoItemDone(id: string, isDone: boolean): Promise<TodoItem> {
  return request(`${ITEMS_URL}/${id}/done`, { method: 'PATCH', body: JSON.stringify({ isDone }) })
}

export function deleteTodoItem(id: string): Promise<void> {
  return request(`${ITEMS_URL}/${id}`, { method: 'DELETE' })
}

/** Nowa kolejność: wszystkie punkty listy, od góry. 409, gdy lista zmieniła się w międzyczasie. */
export function reorderTodoItems(listId: string, itemIds: string[]): Promise<TodoItem[]> {
  return request(`${LISTS_URL}/${listId}/items/order`, { method: 'PUT', body: JSON.stringify({ itemIds }) })
}
