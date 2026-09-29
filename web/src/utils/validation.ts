// Client-side form checks for instant feedback. The API validates everything again
// on the server (DataAnnotations), so these are a convenience, not a security measure.

export interface LoginErrors {
  email?: string
  password?: string
}

export function validateLogin(email: string, password: string): LoginErrors {
  const errors: LoginErrors = {}
  if (!email.trim()) errors.email = 'Email is required.'
  else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) errors.email = 'Enter a valid email address.'
  if (!password) errors.password = 'Password is required.'
  return errors
}

/** Class form values are strings (what inputs give us); they become numbers on submit. */
export interface ClassFormValues {
  name: string
  level: string
  dayOfWeek: string
  startTime: string
  endTime: string
  capacity: string
  monthlyFee: string
  coachId: string
  isActive: boolean
}

export type ClassFormErrors = Partial<Record<keyof ClassFormValues, string>>

/** Same rules as SaveClassRequest on the server. */
export function validateClass(v: ClassFormValues): ClassFormErrors {
  const e: ClassFormErrors = {}
  if (v.name.trim().length < 2) e.name = 'Name must be at least 2 characters.'
  if (!v.startTime) e.startTime = 'Start time is required.'
  if (!v.endTime) e.endTime = 'End time is required.'
  else if (v.startTime && v.endTime <= v.startTime) e.endTime = 'End time must be after start time.'
  const capacity = Number(v.capacity)
  if (!Number.isInteger(capacity) || capacity < 1 || capacity > 100) e.capacity = 'Capacity must be a whole number from 1 to 100.'
  const fee = Number(v.monthlyFee)
  if (v.monthlyFee === '' || Number.isNaN(fee) || fee < 0) e.monthlyFee = 'Fee must be 0 or more.'
  if (!v.coachId) e.coachId = 'Choose a coach.'
  return e
}
