export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

type ProblemDetails = {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

/** Turns the API's problem details into one sentence a user can act on. */
function describe(problem: ProblemDetails, fallback: string): string {
  if (problem.detail) return problem.detail

  if (problem.errors) {
    const messages = Object.values(problem.errors).flat()
    if (messages.length > 0) return messages.join(' ')
  }

  return problem.title ?? fallback
}

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const response = await fetch(path, {
    ...options,
    // The session is a cookie, so every call must carry it.
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', ...options.headers },
  })

  if (!response.ok) {
    let message = response.statusText
    try {
      message = describe((await response.json()) as ProblemDetails, message)
    } catch {
      // A response without a JSON body keeps the status text.
    }
    throw new ApiError(response.status, message)
  }

  if (response.status === 204) return undefined as T

  return (await response.json()) as T
}

export const api = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'POST', body: body === undefined ? undefined : JSON.stringify(body) }),
  put: <T>(path: string, body: unknown) =>
    request<T>(path, { method: 'PUT', body: JSON.stringify(body) }),
}
