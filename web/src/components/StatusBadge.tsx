import { humanize } from '../utils/format'

// Colour groups for enrolment and workflow statuses (see .badge-* in index.css).
const tone: Record<string, string> = {
  Approved: 'good',
  AwaitingApproval: 'warn',
  PendingAdminApproval: 'warn',
  RevisionRequested: 'warn',
  Failed: 'bad',
  Rejected: 'bad',
  Cancelled: 'muted',
  Submitted: 'info',
  Queued: 'info',
  AgentProcessing: 'info',
  Running: 'info',
  Succeeded: 'good',
}

export function StatusBadge({ status }: { status: string }) {
  return <span className={`badge badge-${tone[status] ?? 'muted'}`}>{humanize(status)}</span>
}
