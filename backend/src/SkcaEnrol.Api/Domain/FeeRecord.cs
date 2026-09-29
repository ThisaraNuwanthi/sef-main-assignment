namespace SkcaEnrol.Api.Domain;

/// <summary>The fee owed for one enrolment in one month.</summary>
public class FeeRecord : BaseEntity
{
    public int EnrolmentId { get; set; }
    public Enrolment? Enrolment { get; set; }

    // Always the first day of the month (e.g. 2026-10-01) so months compare simply.
    public DateOnly Month { get; set; }

    public decimal Amount { get; set; }
    public bool SiblingDiscountApplied { get; set; }
    public FeeStatus Status { get; set; } = FeeStatus.Due;
}
