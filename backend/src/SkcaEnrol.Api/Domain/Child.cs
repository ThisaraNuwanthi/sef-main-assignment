namespace SkcaEnrol.Api.Domain;

public class Child : BaseEntity
{
    public int ParentId { get; set; }
    public User? Parent { get; set; }

    public string FullName { get; set; } = "";
    public DateOnly DateOfBirth { get; set; }

    // Optional. Used by the skill assessment agent to read public ratings.
    public string? LichessUsername { get; set; }

    // Relative path inside the uploads folder, never a full disk path.
    public string? PhotoPath { get; set; }

    public List<Enrolment> Enrolments { get; set; } = new();

    /// <summary>Age in whole years on the given day.</summary>
    public int AgeOn(DateOnly day)
    {
        var age = day.Year - DateOfBirth.Year;
        if (DateOfBirth.AddYears(age) > day) age--; // birthday not reached yet this year
        return age;
    }
}
