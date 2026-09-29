using Microsoft.EntityFrameworkCore;
using SkcaEnrol.Api.Auth;
using SkcaEnrol.Api.Common;
using SkcaEnrol.Api.Data;
using SkcaEnrol.Api.Domain;
using SkcaEnrol.Api.Dtos;

namespace SkcaEnrol.Api.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterParentAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
}

public class AuthService(AppDbContext db, ITokenService tokens) : IAuthService
{
    /// <summary>
    /// Public self-registration only ever creates Parents. Admin and Coach
    /// accounts are created by seeding/admins, so nobody can sign up as Admin.
    /// </summary>
    public async Task<AuthResponse> RegisterParentAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = NormaliseEmail(request.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("An account with this email already exists.");

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.Parent
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return BuildResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = NormaliseEmail(request.Email);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);

        // Same message for "no such user" and "wrong password" so attackers
        // cannot use login to discover which emails are registered.
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException("Invalid email or password.");

        return BuildResponse(user);
    }

    private AuthResponse BuildResponse(User user)
    {
        var (token, expiresAt) = tokens.CreateToken(user);
        return new AuthResponse(token, expiresAt, new UserDto(user.Id, user.FullName, user.Email, user.Role.ToString()));
    }

    private static string NormaliseEmail(string email) => email.Trim().ToLowerInvariant();
}
