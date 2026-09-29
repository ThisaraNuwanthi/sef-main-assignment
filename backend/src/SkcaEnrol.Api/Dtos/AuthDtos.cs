using System.ComponentModel.DataAnnotations;

namespace SkcaEnrol.Api.Dtos;

// DataAnnotations are checked automatically by [ApiController]; a failure
// returns 400 with a ValidationProblemDetails body before the controller runs.

public class RegisterRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = "";

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = "";

    // At least 8 chars with a letter and a digit: simple but not trivially guessable.
    [Required, StringLength(100, MinimumLength = 8)]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$", ErrorMessage = "Password must contain at least one letter and one number.")]
    public string Password { get; set; } = "";
}

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    public string Password { get; set; } = "";
}

public record UserDto(int Id, string FullName, string Email, string Role);

public record AuthResponse(string Token, DateTime ExpiresAt, UserDto User);
