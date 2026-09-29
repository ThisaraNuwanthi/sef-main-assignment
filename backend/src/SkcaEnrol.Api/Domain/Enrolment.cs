namespace SkcaEnrol.Api.Domain;

/// <summary>
/// A parent's request to place one child in a class. It moves through
/// EnrolmentStatus; every move is written to EnrolmentStatusHistory.
/// </summary>
public class Enrolment : BaseEntity
{
    public int ChildId { get; set; }
    public Child? Child { get; set; }

    // The class the parent asked for, if any. The agents may suggest another.
    public int? RequestedClassId { get; set; }
    public ChessClass? RequestedClass { get; set; }

    // Only set when an Admin approves. Until then nothing is really booked.
    public int? AssignedClassId { get; set; }
    public ChessClass? AssignedClass { get; set; }

    public List<DayOfWeek> PreferredDays { get; set; } = new();
    public TimeOnly? PreferredTimeFrom { get; set; }
    public TimeOnly? PreferredTimeTo { get; set; }

    // Free text from the parent. Treated as untrusted data everywhere.
    public string? ParentNotes { get; set; }

    public EnrolmentStatus Status { get; set; } = EnrolmentStatus.Submitted;

    public List<EnrolmentStatusHistory> History { get; set; } = new();
    public List<FeeRecord> FeeRecords { get; set; } = new();

    public const int ParentNotesMaxLength = 500;
}
