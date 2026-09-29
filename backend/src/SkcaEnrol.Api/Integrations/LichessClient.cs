using System.Net;
using System.Text.Json;
using SkcaEnrol.Api.Agents;

namespace SkcaEnrol.Api.Integrations;

public record LichessPerf(int Rating, int Games, bool Provisional);

/// <param name="Perfs">Rating per game type, e.g. "rapid", "blitz", "classical".</param>
public record LichessProfile(string Username, bool Closed, int TotalGames, Dictionary<string, LichessPerf> Perfs);

public interface ILichessClient
{
    /// <summary>Returns the public profile, or null if the user does not exist (404).</summary>
    Task<LichessProfile?> GetProfileAsync(string username, CancellationToken ct = default);
}

/// <summary>
/// Typed HttpClient for the public Lichess API (GET /api/user/{username}, no auth needed).
/// Configured in Program.cs with a 10 second timeout.
/// </summary>
public class LichessClient(HttpClient http, ILogger<LichessClient> logger) : ILichessClient
{
    private const int MaxAttempts = 3; // 1 try + 2 retries, for temporary failures only

    public async Task<LichessProfile?> GetProfileAsync(string username, CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var response = await http.GetAsync($"api/user/{Uri.EscapeDataString(username)}", ct);

                if (response.StatusCode == HttpStatusCode.NotFound)
                    return null;

                // Lichess asks clients to wait a full minute after a 429, so retrying now would be rude and useless.
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    throw new ToolFailedException("Lichess rate limit reached (HTTP 429).");

                // 5xx = Lichess-side problem, which is often temporary: retry.
                if ((int)response.StatusCode >= 500 && attempt < MaxAttempts)
                {
                    logger.LogWarning("Lichess returned {Status}, retrying (attempt {Attempt})", (int)response.StatusCode, attempt);
                    await Task.Delay(TimeSpan.FromMilliseconds(400 * attempt), ct);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                    throw new ToolFailedException($"Lichess returned HTTP {(int)response.StatusCode}.");

                var json = await response.Content.ReadAsStringAsync(ct);
                return Parse(json);
            }
            catch (HttpRequestException) when (attempt < MaxAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(400 * attempt), ct);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested && attempt < MaxAttempts)
            {
                // HttpClient.Timeout (10s) fired, not our caller cancelling: retry.
                logger.LogWarning("Lichess timed out, retrying (attempt {Attempt})", attempt);
            }
            catch (HttpRequestException ex)
            {
                throw new ToolFailedException($"Could not reach Lichess: {ex.Message}");
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new ToolFailedException("Lichess did not answer within 10 seconds.");
            }
        }
    }

    /// <summary>Reads only the fields we need; anything malformed becomes a clear tool failure.</summary>
    public static LichessProfile Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var perfs = new Dictionary<string, LichessPerf>();
            if (root.TryGetProperty("perfs", out var perfsElement) && perfsElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var perf in perfsElement.EnumerateObject())
                {
                    if (perf.Value.ValueKind != JsonValueKind.Object || !perf.Value.TryGetProperty("rating", out var rating)) continue;
                    var games = perf.Value.TryGetProperty("games", out var g) ? g.GetInt32() : 0;
                    var prov = perf.Value.TryGetProperty("prov", out var p) && p.GetBoolean();
                    perfs[perf.Name] = new LichessPerf(rating.GetInt32(), games, prov);
                }
            }

            var total = root.TryGetProperty("count", out var count) && count.TryGetProperty("all", out var all) ? all.GetInt32() : 0;
            var closed = (root.TryGetProperty("disabled", out var d) && d.GetBoolean())
                         || (root.TryGetProperty("tosViolation", out var t) && t.GetBoolean());

            return new LichessProfile(root.GetProperty("username").GetString() ?? "", closed, total, perfs);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new ToolFailedException("Lichess returned data in an unexpected format.");
        }
    }
}
