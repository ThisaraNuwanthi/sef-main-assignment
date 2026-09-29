import { clearSession, loadSession } from '../auth/authStorage'

// The only place the app talks to the ASP.NET Core API.
export const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5056').replace(/\/$/, '')

/** Fired when the API says our token is no longer valid; AuthContext listens and logs out. */
export const UNAUTHORIZED_EVENT = 'skca:unauthorized'

/** An error the UI can show: HTTP status + the API's ProblemDetails message. */
export class ApiError extends Error {
  status: number
  fieldErrors: string[]

  constructor(status: number, message: string, fieldErrors: string[] = []) {
    super(message)
    this.status = status
    this.fieldErrors = fieldErrors
  }
}

interface ProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]> // validation errors from [ApiController]
}

type Method = 'GET' | 'POST' | 'PUT' | 'DELETE'

export async function api<T>(path: string, options: { method?: Method; body?: unknown } = {}): Promise<T> {
  const session = loadSession()
  const headers: Record<string, string> = { Accept: 'application/json' }
  if (options.body !== undefined) headers['Content-Type'] = 'application/json'
  if (session) headers.Authorization = `Bearer ${session.token}`

  let response: Response
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      method: options.method ?? 'GET',
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
    })
  } catch {
    throw new ApiError(0, 'Cannot reach the server. Check your connection or that the API is running.')
  }

  if (response.status === 401 && session) {
    clearSession()
    window.dispatchEvent(new Event(UNAUTHORIZED_EVENT))
  }

  if (!response.ok) {
    let problem: ProblemDetails = {}
    try {
      problem = (await response.json()) as ProblemDetails
    } catch {
      // Not JSON (e.g. a proxy error page): fall back to a generic message below.
    }
    const fieldErrors = problem.errors ? Object.values(problem.errors).flat() : []
    const message = problem.detail ?? (fieldErrors.length ? 'Please fix the highlighted problems.' : problem.title) ?? `Request failed (${response.status}).`
    throw new ApiError(response.status, message, fieldErrors)
  }

  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

/** Builds "?a=1&b=x" from an object, skipping empty values. */
export function toQuery(params: Record<string, string | number | boolean | undefined | null>): string {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== '') search.set(key, String(value))
  }
  const text = search.toString()
  return text ? `?${text}` : ''
}
