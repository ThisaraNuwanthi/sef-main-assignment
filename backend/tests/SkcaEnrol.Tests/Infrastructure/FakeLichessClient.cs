using SkcaEnrol.Api.Agents;
using SkcaEnrol.Api.Integrations;

namespace SkcaEnrol.Tests.Infrastructure;

/// <summary>
/// Stands in for lichess.org in tests: no network, fully predictable.
/// Tests can make it fail to check the agent's safe fallback.
/// </summary>
public class FakeLichessClient : ILichessClient
{
    /// <summary>Rapid rating returned for any known username.</summary>
    public int Rating { get; set; } = 1100;

    /// <summary>When true, every call throws, like a network failure or a 429.</summary>
    public bool Fail { get; set; }

    public Task<LichessProfile?> GetProfileAsync(string username, CancellationToken ct = default)
    {
        if (Fail) throw new ToolFailedException("Lichess rate limit reached (HTTP 429).");
        if (username == "no_such_user") return Task.FromResult<LichessProfile?>(null);

        var perfs = new Dictionary<string, LichessPerf> { ["rapid"] = new(Rating, 40, false) };
        return Task.FromResult<LichessProfile?>(new LichessProfile(username, false, 40, perfs));
    }
}
