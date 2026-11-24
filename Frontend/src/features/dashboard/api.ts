import { request } from '../../lib/http'
import type { Task } from '../tasks/types'

/**
 * Nieukończone zadania z terminem od dziś do dziś + `days` (GET /api/tasks/upcoming, SCRUM-114) – z własnych harmonogramów
 * i udostępnionych, posortowane po terminie, potem po priorytecie. Backend przyjmuje days 1–90 i limit 1–50.
 * „Dziś” liczy w UTC, dlatego widżet prosi o zapas dni i sam wybiera lokalne „dziś” i „jutro”.
 */
export function getUpcomingTasks(days: number, limit: number): Promise<Task[]> {
  const query = new URLSearchParams({ days: String(days), limit: String(limit) })
  return request<Task[]>(`/api/tasks/upcoming?${query.toString()}`)
}
