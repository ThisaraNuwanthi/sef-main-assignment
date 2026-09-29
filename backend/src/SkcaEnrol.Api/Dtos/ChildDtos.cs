using System.ComponentModel.DataAnnotations;

namespace SkcaEnrol.Api.Dtos;

public record ChildDto(
    int Id,
    string FullName,
    DateOnly DateOfBirth,
    int Age,
    string? LichessUsername,
    bool HasPhoto);

public class SaveChildRequest : IValidatableObject
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = "";

    [Required]
    public DateOnly? DateOfBirth { get; set; }

    // Lichess usernames: 2-30 letters, digits, "_" or "-".
    [RegularExpression("^[A-Za-z0-9_-]{2,30}$", ErrorMessage = "Not a valid Lichess username.")]
    public string? LichessUsername { get; set; }

    public const int MinAge = 4;
    public const int MaxAge = 18;

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (DateOfBirth is null) yield break;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // The academy teaches children aged 4-18 only.
        if (DateOfBirth > today.AddYears(-MinAge) || DateOfBirth < today.AddYears(-MaxAge - 1))
            yield return new ValidationResult($"Child must be between {MinAge} and {MaxAge} years old.", new[] { nameof(DateOfBirth) });
    }
}
