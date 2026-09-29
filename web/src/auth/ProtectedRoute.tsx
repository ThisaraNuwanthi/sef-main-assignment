import { Navigate, Outlet, useLocation } from 'react-router'
import type { Role } from '../api/types'
import { useAuth } from './AuthContext'

/**
 * Wraps routes that need a login. Not logged in -> /login (remembering where you were going).
 * Logged in with the wrong role -> /forbidden.
 * This is only for a good user experience: the API enforces the same rules on every request.
 */
export function ProtectedRoute({ roles }: { roles: Role[] }) {
  const { user } = useAuth()
  const location = useLocation()

  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname }} />
  if (!roles.includes(user.role)) return <Navigate to="/forbidden" replace />
  return <Outlet />
}
