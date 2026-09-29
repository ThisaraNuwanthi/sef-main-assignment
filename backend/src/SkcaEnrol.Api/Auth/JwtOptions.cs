namespace SkcaEnrol.Api.Auth;

/// <summary>
/// Bound from the "Jwt" config section. The signing Key is a secret: it comes
/// from user-secrets locally and the Jwt__Key environment variable in production.
/// </summary>
public class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "skca-enrol-api";
    public string Audience { get; set; } = "skca-enrol-clients";
    public string Key { get; set; } = "";
    public int ExpiryMinutes { get; set; } = 480; // one working day
}
