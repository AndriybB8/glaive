export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

/** Zasób zmieniono w innym miejscu (np. w drugiej karcie). `current` to jego aktualna wersja z serwera. */
export class ConflictError<T = unknown> extends ApiError {
  readonly current: T

  constructor(message: string, current: T) {
    super(409, message)
    this.name = 'ConflictError'
    this.current = current
  }
}

interface ProblemDetails {
  detail?: string
  title?: string
  current?: unknown
}

/** Wywołanie API: JSON w obie strony, błędy jako {@link ApiError} z komunikatem z serwera. */
export async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...init?.headers },
  })

  if (!response.ok) {
    const problem = (await response.json().catch(() => ({}))) as ProblemDetails
    const message = problem.detail ?? problem.title ?? `Błąd serwera (${response.status}).`

    if (response.status === 409 && problem.current) {
      throw new ConflictError(message, problem.current)
    }
    throw new ApiError(response.status, message)
  }

  return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
}
