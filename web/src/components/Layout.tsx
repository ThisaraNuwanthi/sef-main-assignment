import { NavLink, Outlet, useNavigate } from 'react-router'
import { useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/AuthContext'

/** Header + role-based navigation around every logged-in page. */
export function Layout() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const handleLogout = () => {
    logout()
    queryClient.clear() // never show one user's cached data to the next
    navigate('/login')
  }

  return (
    <div className="app">
      <a className="skip-link" href="#main">Skip to content</a>
      <header className="topbar">
        <span className="brand">♞ SKCA Enrol</span>
        <nav aria-label="Main">
          {user?.role === 'Admin' && (
            <>
              <NavLink to="/admin" end>Dashboard</NavLink>
              <NavLink to="/admin/enrolments">Enrolments</NavLink>
              <NavLink to="/admin/classes">Classes</NavLink>
            </>
          )}
          {user?.role === 'Coach' && <NavLink to="/coach/classes">My classes</NavLink>}
        </nav>
        <div className="user">
          <span>
            {user?.fullName} <small>({user?.role})</small>
          </span>
          <button type="button" className="btn-secondary" onClick={handleLogout}>
            Log out
          </button>
        </div>
      </header>
      <main id="main" className="content">
        <Outlet />
      </main>
    </div>
  )
}
