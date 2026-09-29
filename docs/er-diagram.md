# Database design (PostgreSQL via EF Core)

Every table also has `Id` (PK), `CreatedAt` and `UpdatedAt` (set automatically in `AppDbContext.SaveChangesAsync`).
Enums are stored as text, except `DayOfWeek` (integer, so it sorts in week order).

```mermaid
erDiagram
    Users ||--o{ Children : "parent of"
    Users ||--o{ Classes : "coaches"
    Children ||--o{ Enrolments : "requests"
    Classes ||--o{ Enrolments : "assigned to (AssignedClassId)"
    Classes ||--o{ Enrolments : "requested (RequestedClassId)"
    Enrolments ||--o{ FeeRecords : "billed as"
    Enrolments ||--o{ EnrolmentStatusHistory : "timeline"
    Users |o--o{ EnrolmentStatusHistory : "changed by"
    Enrolments ||--o{ AgentWorkflows : "processed by"
    AgentWorkflows ||--o{ AgentSteps : "has"
    AgentSteps ||--o{ ToolCalls : "made"
    AgentWorkflows ||--o{ ValidationResults : "checked by"
    AgentWorkflows ||--o{ ApprovalDecisions : "decided by"
    Users ||--o{ ApprovalDecisions : "admin"

    Users {
        int Id PK
        string FullName
        string Email UK "lower-case, unique"
        string PasswordHash "BCrypt"
        string Role "Admin | Coach | Parent"
    }
    Children {
        int Id PK
        int ParentId FK
        string FullName
        date DateOfBirth
        string LichessUsername "nullable"
        string PhotoPath "nullable, relative path"
    }
    Classes {
        int Id PK
        string Name UK
        string Level "Beginner | Intermediate | Advanced"
        int DayOfWeek "0 = Sunday"
        time StartTime
        time EndTime "CHECK EndTime > StartTime"
        int Capacity "CHECK > 0"
        numeric MonthlyFee "CHECK >= 0"
        int CoachId FK
        bool IsActive
    }
    Enrolments {
        int Id PK
        int ChildId FK
        int RequestedClassId FK "nullable"
        int AssignedClassId FK "nullable, set only on approval"
        int_array PreferredDays
        time PreferredTimeFrom "nullable"
        time PreferredTimeTo "nullable"
        string ParentNotes "max 500, untrusted"
        string Status "8 states"
        xid xmin "optimistic concurrency"
    }
    FeeRecords {
        int Id PK
        int EnrolmentId FK
        date Month "first day of month; UNIQUE with EnrolmentId"
        numeric Amount "CHECK >= 0"
        bool SiblingDiscountApplied
        string Status "Due | Paid"
    }
    EnrolmentStatusHistory {
        int Id PK
        int EnrolmentId FK
        string FromStatus "nullable"
        string ToStatus
        int ChangedByUserId FK "null = agent workflow"
        string Note
        timestamptz ChangedAt
    }
    AgentWorkflows {
        int Id PK
        int EnrolmentId FK
        string Objective
        jsonb PlanJson
        string Status "Queued ... AwaitingApproval ... Failed"
        jsonb FinalOutcome "placement proposal"
        string FailureReason
        timestamptz StartedAt
        timestamptz CompletedAt
    }
    AgentSteps {
        int Id PK
        int WorkflowId FK
        int StepNo "UNIQUE with WorkflowId"
        string StepName
        string AgentName
        jsonb InputJson
        jsonb OutputJson
        string Status
        bigint DurationMs
        string Error
        int RetryCount
    }
    ToolCalls {
        int Id PK
        int StepId FK
        string ToolName
        jsonb InputJson
        jsonb OutputJson
        bool Success
        bigint DurationMs
        string Error
    }
    ValidationResults {
        int Id PK
        int WorkflowId FK
        string RuleName
        bool Passed
        string Severity "Error | Warning"
        string Message
    }
    ApprovalDecisions {
        int Id PK
        int WorkflowId FK
        int AdminUserId FK
        string Decision "Approved | Rejected | RevisionRequested"
        string Note
        timestamptz DecidedAt
    }
```

## Constraints and indexes

| Kind | Where | Why |
|---|---|---|
| Unique | `Users.Email`, `Classes.Name`, `(FeeRecords.EnrolmentId, Month)`, `(AgentSteps.WorkflowId, StepNo)` | no duplicate accounts/classes; approving twice cannot double-bill |
| Check | `Capacity > 0`, `MonthlyFee >= 0`, `EndTime > StartTime`, `FeeRecords.Amount >= 0` | the database rejects impossible data even if the API has a bug |
| Index | `Enrolments.Status`, `Enrolments.ChildId`, `(Enrolments.AssignedClassId, Status)` | status filters, "open request" check, seat counting |
| Index | `(Classes.Level, DayOfWeek)`, `Classes.CoachId` | ClassSearch tool, coach roster |
| Index | FKs on `Children`, `AgentWorkflows`, `AgentSteps`, `ToolCalls`, `ValidationResults`, `ApprovalDecisions`, `EnrolmentStatusHistory` | loading a workflow / history quickly |
| Concurrency | `Enrolments.xmin` | two admins cannot both change the same enrolment |
| Delete rules | Restrict on Class → Coach and Enrolment → Child/Class; Cascade on workflow children | history is never silently lost |

Migrations: `backend/src/SkcaEnrol.Api/Data/Migrations` (`InitialCreate`, `AgentWorkflowState`), applied automatically on startup.
