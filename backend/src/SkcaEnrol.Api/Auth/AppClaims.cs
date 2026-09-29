using System.Security.Claims;
using SkcaEnrol.Api.Common;
using SkcaEnrol.Api.Domain;

namespace SkcaEnrol.Api.Auth;

/// <summary>Claim names used in our JWTs (standard short JWT names).</summary>
public static class AppClaims
{
    public const string UserId = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";
}

/// <summary>Role names for [Authorize(Roles = ...)], kept in sync with the UserRole enum.</summary>
public static class Roles
{
    public const string Admin = nameof(UserRole.Admin);
    public const string Coach = nameof(UserRole.Coach);
    public const string Parent = nameof(UserRole.Parent);
}

/// <summary>Reads the logged-in user out of the validated JWT.</summary>
public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(AppClaims.UserId);
        return int.TryParse(raw, out var id) ? id : throw new UnauthorizedException("Missing user id in token.");
    }

    public static UserRole GetRole(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(AppClaims.Role);
        return Enum.TryParse<UserRole>(raw, out var role) ? role : throw new UnauthorizedException("Missing role in token.");
    }
}
