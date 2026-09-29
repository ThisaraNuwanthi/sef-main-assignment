using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using SkcaEnrol.Api.Domain;

namespace SkcaEnrol.Api.Dtos;

// JSON columns are returned as JsonElement so the client receives real JSON objects, not strings.

public record ToolCallDto(int Id, string ToolName, JsonElement? Input, JsonElement? Output, bool Success, long DurationMs, string? Error, DateTime CalledAt);

public record AgentStepDto(
    int Id, int StepNo, string StepName, string AgentName, StepStatus Status,
    JsonElement? Input, JsonElement? Output, long DurationMs, string? Error, int RetryCount,
    DateTime StartedAt, List<ToolCallDto> ToolCalls);

public record ValidationResultDto(string RuleName, bool Passed, ValidationSeverity Severity, string Message);

public record ApprovalDecisionDto(ApprovalDecisionType Decision, string AdminName, string? Note, DateTime DecidedAt);

public record WorkflowDto(
    int Id,
    int EnrolmentId,
    EnrolmentStatus EnrolmentStatus,
    string ChildName,
    string ParentName,
    string? ParentNotes,
    string Objective,
    WorkflowStatus Status,
    JsonElement? Plan,
    JsonElement? Proposal,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    long? TotalDurationMs,
    List<AgentStepDto> Steps,
    List<ValidationResultDto> ValidationResults,
    List<ApprovalDecisionDto> Decisions);

public record WorkflowListItemDto(int Id, int EnrolmentId, string ChildName, WorkflowStatus Status, DateTime CreatedAt, DateTime? CompletedAt);

/// <summary>Body for approve / reject / revise.</summary>
public class DecisionRequest
{
    /// <summary>Optional for approve; required for reject and revise (the parent needs to know why).</summary>
    [StringLength(500)]
    public string? Note { get; set; }
}

public record RetryResultDto(int NewWorkflowId, EnrolmentStatus EnrolmentStatus);
