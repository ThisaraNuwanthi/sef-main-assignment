namespace SkcaEnrol.Api.Domain;

/// <summary>One row per status change, so a parent can see the full timeline.</summary>
public class EnrolmentStatusHistory : BaseEntity
{
    public int EnrolmentId { get; set; }
    public Enrolment? Enrolment { get; set; }

    // Null for the very first row (the enrolment did not exist before).
    public EnrolmentStatus? FromStatus { get; set; }
    public EnrolmentStatus ToStatus { get; set; }

    // Null means the system/agent workflow made the change, not a person.
    public int? ChangedByUserId { get; set; }
    public User? ChangedByUser { get; set; }

    public string? Note { get; set; }
    public DateTime ChangedAt { get; set; }
}
