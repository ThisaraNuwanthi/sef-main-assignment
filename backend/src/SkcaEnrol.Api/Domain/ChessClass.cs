namespace SkcaEnrol.Api.Domain;

public class ChessClass : BaseEntity
{
    public string Name { get; set; } = "";
    public ClassLevel Level { get; set; }

    // One weekly slot per class keeps clash checks simple: same day + overlapping times.
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    public int Capacity { get; set; }
    public decimal MonthlyFee { get; set; }

    public int CoachId { get; set; }
    public User? Coach { get; set; }

    // Inactive classes stay in the database (old enrolments point at them)
    // but are hidden from parents and from the placement search.
    public bool IsActive { get; set; } = true;

    // Enrolments placed in this class. Only the Approved ones take a seat.
    public List<Enrolment> AssignedEnrolments { get; set; } = new();

    /// <summary>True when this class and the other slot share a day and their times overlap.</summary>
    public bool OverlapsWith(DayOfWeek day, TimeOnly start, TimeOnly end) =>
        DayOfWeek == day && StartTime < end && start < EndTime;
}
