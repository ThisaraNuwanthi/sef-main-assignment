import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api, UNAUTHORIZED_EVENT } from '../api/client'
import type { AuthResponse, User } from '../api/types'
import { clearSession, loadSession, saveSession } from './authStorage'

interface AuthState {
  user: User | null
  /** Logs in and returns the user, or throws ApiError. Parents are refused (they use the mobile app). */
  login: (email: string, password: string) => Promise<User>
  logout: () => void
}

const AuthContext = createContext<AuthState | null>(null)

/**
 * Auth is small, app-wide client state (who is logged in), so plain React Context is enough.
 * Server data (classes, enrolments...) lives in TanStack Query instead. See ADR 0001.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(() => loadSession()?.user ?? null)

  const logout = useCallback(() => {
    clearSession()
    setUser(null)
  }, [])

  const login = useCallback(async (email: string, password: string) => {
    const session = await api<AuthResponse>('/api/auth/login', { method: 'POST', body: { email, password } })
    if (session.user.role === 'Parent') {
      throw new Error('Parents use the SKCA Enrol mobile app. This website is for staff.')
    }
    saveSession(session)
    setUser(session.user)
    return session.user
  }, [])

  // If any API call gets a 401 (expired token), drop the user so routes redirect to login.
  useEffect(() => {
    const onUnauthorized = () => setUser(null)
    window.addEventListener(UNAUTHORIZED_EVENT, onUnauthorized)
    return () => window.removeEventListener(UNAUTHORIZED_EVENT, onUnauthorized)
  }, [])

  const value = useMemo(() => ({ user, login, logout }), [user, login, logout])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth(): AuthState {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used inside <AuthProvider>')
  return context
}

/** Where each role lands after logging in. */
// eslint-disable-next-line react-refresh/only-export-components
export function homeFor(user: User): string {
  return user.role === 'Coach' ? '/coach/classes' : '/admin'
}
