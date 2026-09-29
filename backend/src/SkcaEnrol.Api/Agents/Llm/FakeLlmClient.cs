using System.Collections.Concurrent;
using System.Text.Json;

namespace SkcaEnrol.Api.Agents.Llm;

/// <summary>
/// Offline stand-in for Gemini. It returns fixed-shape JSON built with simple,
/// predictable rules, so tests and demos work with no API key or internet.
/// Tests can force any answer (e.g. an invalid class id) through Overrides.
/// </summary>
public class FakeLlmClient : ILlmClient
{
    /// <summary>Agent name -> function that returns the raw answer. Used by tests.</summary>
    public ConcurrentDictionary<string, Func<LlmRequest, string>> Overrides { get; } = new();

    public Task<string> GenerateJsonAsync(LlmRequest request, CancellationToken ct = default)
    {
        if (Overrides.TryGetValue(request.AgentName, out var forced))
            return Task.FromResult(forced(request));

        var answer = request.AgentName switch
        {
            PlannerAgent.Name => PlannerAnswer(),
            SkillAssessmentAgent.Name => SkillAnswer(request),
            PlacementAgent.Name => PlacementAnswer(request),
            _ => "{}"
        };
        return Task.FromResult(answer);
    }

    private static string PlannerAnswer() => JsonSerializer.Serialize(new
    {
        steps = PlanSteps.All,
        summary = "Assess the child's level, search matching classes, propose one, validate it, then ask an admin."
    });

    private static string SkillAnswer(LlmRequest request)
    {
        // Mirrors what we ask the real model to do: map the best rating to a level.
        using var data = JsonDocument.Parse(AgentPrompt.ExtractData(request.UserContent) ?? "{}");
        var rating = data.RootElement.TryGetProperty("bestRating", out var r) && r.ValueKind == JsonValueKind.Number ? r.GetInt32() : 0;
        var level = LevelRules.FromRating(rating);
        return JsonSerializer.Serialize(new
        {
            level = level.ToString(),
            confidence = "Medium",
            rationale = $"Best Lichess rating {rating} fits the {level} band (offline fake model)."
        });
    }

    private static string PlacementAnswer(LlmRequest request)
    {
        // Picks the first candidate: the search tool already sorts best-fit first.
        using var data = JsonDocument.Parse(AgentPrompt.ExtractData(request.UserContent) ?? "{}");
        var first = data.RootElement.GetProperty("candidates")[0];
        var distance = first.GetProperty("levelDistance").GetInt32();
        return JsonSerializer.Serialize(new
        {
            classId = first.GetProperty("id").GetInt32(),
            reason = "Best match for the assessed level and the preferred day and time (offline fake model).",
            levelToleranceReason = distance == 0 ? null : "No class at the exact level fits the preferred times; this is the closest level."
        });
    }
}
