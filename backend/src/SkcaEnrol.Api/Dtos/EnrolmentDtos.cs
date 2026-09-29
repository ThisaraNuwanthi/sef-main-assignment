using System.ComponentModel.DataAnnotations;
using SkcaEnrol.Api.Domain;

namespace SkcaEnrol.Api.Dtos;

/// <summary>What the parent can set on a request (shared by create and update).</summary>
public class EnrolmentPreferences : IValidatableObject
{
    /// <summary>Optional: a specific class the parent would like. The agents may suggest another.</summary>
    [Range(1, int.MaxValue)]
    public int? RequestedClassId { get; set; }

    [Required, MinLength(1, ErrorMessage = "Choose at least one preferred day.")]
    public List<DayOfWeek> PreferredDays { get; set; } = new();

    public TimeOnly? PreferredTimeFrom { get; set; }
    public TimeOnly? PreferredTimeTo { get; set; }

    [StringLength(Enrolment.ParentNotesMaxLength)]
    public string? ParentNotes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (PreferredTimeFrom is not null && PreferredTimeTo is not null && PreferredTimeTo <= PreferredTimeFrom)
            yield return new ValidationResult("'To' time must be after 'From' time.", new[] { nameof(PreferredTimeTo) });
        if (PreferredDays.Any(d => !Enum.IsDefined(d)))
            yield return new ValidationResult("Invalid day.", new[] { nameof(PreferredDays) });
    }
}

public class CreateEnrolmentRequest : EnrolmentPreferences
{
    [Range(1, int.MaxValue, ErrorMessage = "Choose a child.")]
    public int ChildId { get; set; }
}

public class UpdateEnrolmentRequest : EnrolmentPreferences;

public class EnrolmentQuery
{
    /// <summary>Matches child name or class name.</summary>
    [StringLength(100)]
    public string? Search { get; set; }

    public EnrolmentStatus? Status { get; set; }

    /// <summary>created | updated | child | status</summary>
    [RegularExpression("^(created|updated|child|status)$", ErrorMessage = "sortBy must be created, updated, child or status.")]
    public string SortBy { get; set; } = "created";

    public bool Desc { get; set; } = true;

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 50)]
    public int PageSize { get; set; } = 10;
}

/// <summary>Returned by POST /api/enrolments: the saved request and the workflow now working on it.</summary>
public record EnrolmentCreatedDto(int EnrolmentId, int WorkflowId, EnrolmentStatus Status);

public record EnrolmentListItemDto(
    int Id, int ChildId, string ChildName, string ParentName, EnrolmentStatus Status,
    string? RequestedClassName, string? AssignedClassName, int? LatestWorkflowId,
    DateTime CreatedAt, DateTime UpdatedAt);

public record ClassSummaryDto(int Id, string Name, ClassLevel Level, DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime, string CoachName);

public record FeeRecordDto(DateOnly Month, decimal Amount, bool SiblingDiscountApplied, FeeStatus Status);

public record EnrolmentDetailDto(
    int Id,
    int ChildId,
    string ChildName,
    string ParentName,
    EnrolmentStatus Status,
    List<DayOfWeek> PreferredDays,
    TimeOnly? PreferredTimeFrom,
    TimeOnly? PreferredTimeTo,
    string? ParentNotes,
    ClassSummaryDto? RequestedClass,
    ClassSummaryDto? AssignedClass,
    List<FeeRecordDto> Fees,
    int? LatestWorkflowId,
    WorkflowStatus? LatestWorkflowStatus,
    string? LatestDecisionNote, // e.g. what the admin wants changed after "request revision"
    bool CanEdit,
    bool CanCancel,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record StatusHistoryDto(EnrolmentStatus? FromStatus, EnrolmentStatus ToStatus, string ChangedBy, string? Note, DateTime ChangedAt);

public class CancelEnrolmentRequest
{
    [StringLength(500)]
    public string? Reason { get; set; }
}
