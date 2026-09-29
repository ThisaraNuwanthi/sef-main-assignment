using SkcaEnrol.Api.Integrations;

namespace SkcaEnrol.Api.Agents.Tools;

public record LichessToolInput(string Username);

/// <param name="BestRating">Highest established rating across rapid, classical and blitz (null if none).</param>
public record LichessToolOutput(bool Found, string Username, bool Closed, int TotalGames, int? BestRating, string? RatingType);

/// <summary>Read-only tool: public Lichess profile, reduced to what the skill agent needs.</summary>
public class LichessProfileTool(ILichessClient lichess) : IAgentTool<LichessToolInput, LichessToolOutput>
{
    public const string ToolName = "LichessProfile";
    public string Name => ToolName;

    // Slower time controls first: they reflect real playing strength better than blitz.
    private static readonly string[] RatingTypes = { "rapid", "classical", "blitz" };
    private const int MinGamesForRating = 5; // fewer games = the rating is mostly Lichess's 1500 starting guess

    public async Task<LichessToolOutput> RunAsync(LichessToolInput input, CancellationToken ct)
    {
        var profile = await lichess.GetProfileAsync(input.Username, ct);
        if (profile is null)
            return new LichessToolOutput(false, input.Username, false, 0, null, null);

        var best = RatingTypes
            .Where(type => profile.Perfs.TryGetValue(type, out var p) && p.Games >= MinGamesForRating && !p.Provisional)
            .Select(type => (Type: type, profile.Perfs[type].Rating))
            .OrderByDescending(x => x.Rating)
            .FirstOrDefault();

        return new LichessToolOutput(true, profile.Username, profile.Closed, profile.TotalGames,
            best.Type is null ? null : best.Rating, best.Type);
    }
}
