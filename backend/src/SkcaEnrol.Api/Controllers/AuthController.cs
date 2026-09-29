using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkcaEnrol.Api.Dtos;
using SkcaEnrol.Api.Services;

namespace SkcaEnrol.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController(IAuthService auth) : ControllerBase
{
    /// <summary>Creates a Parent account and returns a JWT so the app can log straight in.</summary>
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await auth.RegisterParentAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Returns a JWT for any role (Admin, Coach or Parent).</summary>
    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(await auth.LoginAsync(request, ct));
}
