namespace SkcaEnrol.Api.Domain;

public class User : BaseEntity
{
    public string FullName { get; set; } = "";

    // Always stored lower-case so "A@x.com" and "a@x.com" hit the same unique index.
    public string Email { get; set; } = "";

    // BCrypt hash only. The plain password never leaves AuthService.
    public string PasswordHash { get; set; } = "";

    public UserRole Role { get; set; }

    public List<Child> Children { get; set; } = new();
    public List<ChessClass> CoachedClasses { get; set; } = new();
}
