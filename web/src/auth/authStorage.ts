import type { AuthResponse } from '../api/types'

// The logged-in session lives in localStorage so a page refresh keeps you logged in.
// Trade-off (see ADR 0001): any script on the page could read it, so we never allow
// untrusted HTML to be rendered (React escapes text by default).
const KEY = 'skca.auth'

export function loadSession(): AuthResponse | null {
  try {
    const raw = localStorage.getItem(KEY)
    if (!raw) return null
    const session = JSON.parse(raw) as AuthResponse
    // Treat an expired token as logged out instead of waiting for a 401.
    if (new Date(session.expiresAt).getTime() <= Date.now()) {
      localStorage.removeItem(KEY)
      return null
    }
    return session
  } catch {
    return null
  }
}

export function saveSession(session: AuthResponse) {
  localStorage.setItem(KEY, JSON.stringify(session))
}

export function clearSession() {
  localStorage.removeItem(KEY)
}
