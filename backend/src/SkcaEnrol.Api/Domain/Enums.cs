namespace SkcaEnrol.Api.Domain;

// All enums are stored as text in PostgreSQL (see AppDbContext) so the
// database stays readable and re-ordering members can never corrupt data.

public enum UserRole
{
    Admin,
    Coach,
    Parent
}

public enum ClassLevel
{
    Beginner,
    Intermediate,
    Advanced
}

public enum EnrolmentStatus
{
    Submitted,
    AgentProcessing,
    PendingAdminApproval,
    Approved,
    Rejected,
    RevisionRequested,
    Failed,
    Cancelled
}

public enum FeeStatus
{
    Due,
    Paid
}
