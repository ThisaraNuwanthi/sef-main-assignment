// TypeScript shapes of the API's JSON (mirrors the C# DTOs in backend/src/SkcaEnrol.Api/Dtos).

export type Role = 'Admin' | 'Coach' | 'Parent'
export type ClassLevel = 'Beginner' | 'Intermediate' | 'Advanced'
export type Day = 'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday'

export const LEVELS: ClassLevel[] = ['Beginner', 'Intermediate', 'Advanced']
export const DAYS: Day[] = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday']

export type EnrolmentStatus =
  | 'Submitted'
  | 'AgentProcessing'
  | 'PendingAdminApproval'
  | 'Approved'
  | 'Rejected'
  | 'RevisionRequested'
  | 'Failed'
  | 'Cancelled'

export const ENROLMENT_STATUSES: EnrolmentStatus[] = [
  'Submitted', 'AgentProcessing', 'PendingAdminApproval', 'Approved',
  'Rejected', 'RevisionRequested', 'Failed', 'Cancelled',
]

export type WorkflowStatus =
  | 'Queued' | 'Running' | 'AwaitingApproval' | 'Approved'
  | 'Rejected' | 'RevisionRequested' | 'Failed' | 'Cancelled'

export interface User {
  id: number
  fullName: string
  email: string
  role: Role
}

export interface AuthResponse {
  token: string
  expiresAt: string
  user: User
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface ChessClass {
  id: number
  name: string
  level: ClassLevel
  dayOfWeek: Day
  startTime: string // "14:00:00"
  endTime: string
  capacity: number
  seatsTaken: number
  seatsLeft: number
  monthlyFee: number
  coachId: number
  coachName: string
  isActive: boolean
}

export interface SaveClassRequest {
  name: string
  level: ClassLevel
  dayOfWeek: Day
  startTime: string
  endTime: string
  capacity: number
  monthlyFee: number
  coachId: number
  isActive: boolean
}

export interface CoachOption {
  id: number
  fullName: string
  email: string
}

export interface RosterStudent {
  childId: number
  childName: string
  age: number
  lichessUsername: string | null
  parentName: string
  placedAt: string
}

export interface CoachClass {
  class: ChessClass
  students: RosterStudent[]
}

export interface EnrolmentListItem {
  id: number
  childId: number
  childName: string
  parentName: string
  status: EnrolmentStatus
  requestedClassName: string | null
  assignedClassName: string | null
  latestWorkflowId: number | null
  createdAt: string
  updatedAt: string
}

export interface ToolCall {
  id: number
  toolName: string
  input: unknown
  output: unknown
  success: boolean
  durationMs: number
  error: string | null
  calledAt: string
}

export interface AgentStep {
  id: number
  stepNo: number
  stepName: string
  agentName: string
  status: 'Running' | 'Succeeded' | 'Failed'
  input: unknown
  output: unknown
  durationMs: number
  error: string | null
  retryCount: number
  startedAt: string
  toolCalls: ToolCall[]
}

export interface ValidationResult {
  ruleName: string
  passed: boolean
  severity: 'Error' | 'Warning'
  message: string
}

export interface ApprovalDecision {
  decision: 'Approved' | 'Rejected' | 'RevisionRequested'
  adminName: string
  note: string | null
  decidedAt: string
}

export interface PlacementProposal {
  classId: number
  className: string
  assessedLevel: ClassLevel
  skillConfidence: string
  skillRationale: string
  placementReason: string
  levelToleranceReason: string | null
  feeAmount: number
  siblingDiscountApplied: boolean
  injectionSuspected: boolean
}

export interface Workflow {
  id: number
  enrolmentId: number
  enrolmentStatus: EnrolmentStatus
  childName: string
  parentName: string
  parentNotes: string | null
  objective: string
  status: WorkflowStatus
  plan: { steps: string[]; summary?: string | null } | null
  proposal: PlacementProposal | null
  failureReason: string | null
  createdAt: string
  startedAt: string | null
  completedAt: string | null
  totalDurationMs: number | null
  steps: AgentStep[]
  validationResults: ValidationResult[]
  decisions: ApprovalDecision[]
}

export interface EnrolmentSummary {
  totalEnrolments: number
  byStatus: { status: EnrolmentStatus; count: number }[]
  classFill: { classId: number; className: string; level: ClassLevel; capacity: number; seatsTaken: number; fillRatePercent: number }[]
  monthlyFees: { month: string; total: number; records: number }[]
  agents: {
    totalWorkflows: number
    awaitingApproval: number
    failed: number
    averageDurationMs: number | null
    successRatePercent: number | null
  }
}
