namespace SkcaEnrol.Api.Domain;

public enum WorkflowStatus
{
    Queued,            // saved, waiting for the background worker
    Running,           // agents are working on it
    AwaitingApproval,  // proposal ready; nothing is booked until an Admin decides
    Approved,
    Rejected,
    RevisionRequested,
    Failed,            // stopped safely; FailureReason says why
    Cancelled
}

public enum StepStatus
{
    Running,
    Succeeded,
    Failed
}

public enum ValidationSeverity
{
    Error,   // blocks the proposal
    Warning  // shown to the Admin, does not block (e.g. suspicious parent notes)
}

public enum ApprovalDecisionType
{
    Approved,
    Rejected,
    RevisionRequested
}

/// <summary>
/// One run of the agent pipeline for one enrolment. A retry or a resubmission
/// creates a new run, so failed runs stay visible as an audit trail.
/// </summary>
public class AgentWorkflow : BaseEntity
{
    public int EnrolmentId { get; set; }
    public Enrolment? Enrolment { get; set; }

    // The goal in plain words, built from the parent's request (without their free-text notes).
    public string Objective { get; set; } = "";

    // jsonb: the plan the PlannerAgent produced (after validation).
    public string? PlanJson { get; set; }

    public WorkflowStatus Status { get; set; } = WorkflowStatus.Queued;

    // jsonb: the placement proposal the Admin approves or rejects.
    public string? FinalOutcome { get; set; }

    public string? FailureReason { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public List<AgentStep> Steps { get; set; } = new();
    public List<WorkflowValidationResult> ValidationResults { get; set; } = new();
    public List<ApprovalDecision> ApprovalDecisions { get; set; } = new();
}

/// <summary>One plan step executed by one agent. Inputs/outputs are stored, never hidden reasoning.</summary>
public class AgentStep : BaseEntity
{
    public int WorkflowId { get; set; }
    public AgentWorkflow? Workflow { get; set; }

    public int StepNo { get; set; }
    public string StepName { get; set; } = "";
    public string AgentName { get; set; } = "";

    public string InputJson { get; set; } = "{}";   // jsonb
    public string? OutputJson { get; set; }         // jsonb

    public StepStatus Status { get; set; } = StepStatus.Running;
    public long DurationMs { get; set; }
    public string? Error { get; set; }
    public int RetryCount { get; set; }
    public DateTime StartedAt { get; set; }

    public List<ToolCall> ToolCalls { get; set; } = new();
}

/// <summary>Every tool an agent used (or tried to use), with timing and result.</summary>
public class ToolCall : BaseEntity
{
    public int StepId { get; set; }
    public AgentStep? Step { get; set; }

    public string ToolName { get; set; } = "";
    public string InputJson { get; set; } = "{}";   // jsonb
    public string? OutputJson { get; set; }         // jsonb
    public bool Success { get; set; }
    public long DurationMs { get; set; }
    public string? Error { get; set; }
}

/// <summary>One deterministic rule checked by the ValidationSafetyAgent.</summary>
public class WorkflowValidationResult : BaseEntity
{
    public int WorkflowId { get; set; }
    public AgentWorkflow? Workflow { get; set; }

    public string RuleName { get; set; } = "";
    public bool Passed { get; set; }
    public ValidationSeverity Severity { get; set; }
    public string Message { get; set; } = "";
}

/// <summary>The human decision on a proposal (the audit entry for the high-impact action).</summary>
public class ApprovalDecision : BaseEntity
{
    public int WorkflowId { get; set; }
    public AgentWorkflow? Workflow { get; set; }

    public int AdminUserId { get; set; }
    public User? AdminUser { get; set; }

    public ApprovalDecisionType Decision { get; set; }
    public string? Note { get; set; }
    public DateTime DecidedAt { get; set; }
}
