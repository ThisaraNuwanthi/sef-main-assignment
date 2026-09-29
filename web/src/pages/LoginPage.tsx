import { useState, type FormEvent } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router'
import { homeFor, useAuth } from '../auth/AuthContext'
import { ErrorAlert } from '../components/States'
import { validateLogin, type LoginErrors as FieldErrors } from '../utils/validation'

export function LoginPage() {
  const { user, login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [errors, setErrors] = useState<FieldErrors>({})
  const [submitError, setSubmitError] = useState<unknown>(null)
  const [submitting, setSubmitting] = useState(false)

  // Already logged in: go straight to your home page.
  if (user) return <Navigate to={homeFor(user)} replace />

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault()
    const found = validateLogin(email, password)
    setErrors(found)
    setSubmitError(null)
    if (Object.keys(found).length > 0) return

    setSubmitting(true)
    try {
      const loggedIn = await login(email.trim(), password)
      const from = (location.state as { from?: string } | null)?.from
      navigate(from ?? homeFor(loggedIn), { replace: true })
    } catch (error) {
      setSubmitError(error)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="login-page">
      <form className="card login-card" onSubmit={handleSubmit} noValidate aria-labelledby="login-title">
        <h1 id="login-title">♞ SKCA Enrol</h1>
        <p className="muted">Staff sign in (admins and coaches)</p>

        {submitError !== null && <ErrorAlert error={submitError} title="Sign in failed" />}

        <label htmlFor="email">Email</label>
        <input
          id="email"
          type="email"
          autoComplete="username"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          aria-invalid={!!errors.email}
          aria-describedby={errors.email ? 'email-error' : undefined}
        />
        {errors.email && <p id="email-error" className="field-error">{errors.email}</p>}

        <label htmlFor="password">Password</label>
        <input
          id="password"
          type="password"
          autoComplete="current-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          aria-invalid={!!errors.password}
          aria-describedby={errors.password ? 'password-error' : undefined}
        />
        {errors.password && <p id="password-error" className="field-error">{errors.password}</p>}

        <button type="submit" className="btn-primary" disabled={submitting}>
          {submitting ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </div>
  )
}
