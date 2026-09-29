// Small display helpers used across pages.

/** "14:00:00" -> "14:00" */
export const hhmm = (time: string | null | undefined) => (time ? time.slice(0, 5) : '')

/** "14:00" (from <input type="time">) -> "14:00:00" (what the API expects) */
export const toApiTime = (time: string) => (time.length === 5 ? `${time}:00` : time)

const money = new Intl.NumberFormat('en-LK', { style: 'currency', currency: 'LKR', maximumFractionDigits: 2 })
export const lkr = (amount: number) => money.format(amount)

export const dateTime = (iso: string | null | undefined) =>
  iso ? new Date(iso).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' }) : '—'

/** "PendingAdminApproval" -> "Pending admin approval" */
export const humanize = (value: string) =>
  value.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/^./, (c) => c.toUpperCase()).replace(/ ([A-Z])/g, (m) => m.toLowerCase())

export const ms = (value: number | null | undefined) =>
  value === null || value === undefined ? '—' : value < 1000 ? `${value} ms` : `${(value / 1000).toFixed(1)} s`
