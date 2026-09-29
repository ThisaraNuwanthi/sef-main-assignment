import { ApiError } from '../api/client'

// The four UI states every data screen needs: loading, error, empty, success.

export function Loading({ label = 'Loading…' }: { label?: string }) {
  return (
    <p className="state state-loading" role="status" aria-live="polite">
      <span className="spinner" aria-hidden="true" /> {label}
    </p>
  )
}

export function ErrorAlert({ error, title = 'Something went wrong' }: { error: unknown; title?: string }) {
  const message = error instanceof Error ? error.message : 'Unexpected error.'
  const details = error instanceof ApiError ? error.fieldErrors : []
  return (
    <div className="alert alert-error" role="alert">
      <strong>{title}.</strong> {message}
      {details.length > 0 && (
        <ul>
          {details.map((d) => (
            <li key={d}>{d}</li>
          ))}
        </ul>
      )}
    </div>
  )
}

export function SuccessAlert({ message }: { message: string }) {
  return (
    <div className="alert alert-success" role="status" aria-live="polite">
      {message}
    </div>
  )
}

export function Empty({ message }: { message: string }) {
  return <p className="state state-empty">{message}</p>
}
