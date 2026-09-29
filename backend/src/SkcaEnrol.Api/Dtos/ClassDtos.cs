using System.ComponentModel.DataAnnotations;
using SkcaEnrol.Api.Domain;

namespace SkcaEnrol.Api.Dtos;

public record ClassDto(
    int Id,
    string Name,
    ClassLevel Level,
    DayOfWeek DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int Capacity,
    int SeatsTaken,
    int SeatsLeft,
    decimal MonthlyFee,
    int CoachId,
    string CoachName,
    bool IsActive);

/// <summary>Used for both create (POST) and update (PUT).</summary>
public class SaveClassRequest : IValidatableObject
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = "";

    [Required, EnumDataType(typeof(ClassLevel))]
    public ClassLevel? Level { get; set; }

    [Required, EnumDataType(typeof(DayOfWeek))]
    public DayOfWeek? DayOfWeek { get; set; }

    [Required]
    public TimeOnly? StartTime { get; set; }

    [Required]
    public TimeOnly? EndTime { get; set; }

    [Range(1, 100)]
    public int Capacity { get; set; }

    [Range(0, 1_000_000)]
    public decimal MonthlyFee { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Choose a coach.")]
    public int CoachId { get; set; }

    public bool IsActive { get; set; } = true;

    // Cross-field rule that single-property attributes cannot express.
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (StartTime is not null && EndTime is not null && EndTime <= StartTime)
            yield return new ValidationResult("End time must be after start time.", new[] { nameof(EndTime) });
    }
}

/// <summary>Query-string filters for GET /api/classes.</summary>
public class ClassQuery
{
    /// <summary>Matches class name or coach name (case-insensitive).</summary>
    [StringLength(100)]
    public string? Search { get; set; }

    public ClassLevel? Level { get; set; }
    public DayOfWeek? Day { get; set; }

    /// <summary>Admins only: include inactive classes. Others always see active only.</summary>
    public bool IncludeInactive { get; set; }

    /// <summary>name | day | fee | seatsLeft</summary>
    [RegularExpression("^(name|day|fee|seatsLeft)$", ErrorMessage = "sortBy must be name, day, fee or seatsLeft.")]
    public string SortBy { get; set; } = "day";

    public bool Desc { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 50)]
    public int PageSize { get; set; } = 10;
}

public record CoachOptionDto(int Id, string FullName, string Email);

public record RosterStudentDto(int ChildId, string ChildName, int Age, string? LichessUsername, string ParentName, DateTime PlacedAt);

/// <summary>A coach's class with the students currently placed in it.</summary>
public record CoachClassDto(ClassDto Class, List<RosterStudentDto> Students);
