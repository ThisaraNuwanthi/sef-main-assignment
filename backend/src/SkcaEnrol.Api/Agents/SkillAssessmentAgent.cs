using SkcaEnrol.Api.Agents.Llm;
using SkcaEnrol.Api.Agents.Tools;
using SkcaEnrol.Api.Domain;

namespace SkcaEnrol.Api.Agents;

/// <summary>
/// Agent 2 (LLM + LichessProfile tool): estimates the child's level.
/// Without a Lichess account, or if Lichess fails, it falls back to an age-based
/// default marked Low confidence, so the workflow can still continue safely.
/// </summary>
public class SkillAssessmentAgent(ILlmClient llm)
{
    public const string Name = "SkillAssessmentAgent";
    public static readonly IReadOnlySet<string> AllowedTools = new HashSet<string> { LichessProfileTool.ToolName };

    private const string SystemInstruction = """
        You are the skill assessment agent of a chess academy.
        From the child's public Lichess data, choose a class level:
        Beginner (rating below 1300), Intermediate (1300-1799) or Advanced (1800+).
        Consider the number of games: few games means lower confidence.
        Answer with JSON only, in exactly this shape:
        {"level": "Beginner|Intermediate|Advanced", "confidence": "Low|Medium|High", "rationale": "max 2 sentences"}
        """;

    private static readonly string[] Confidences = { "Low", "Medium", "High" };

    public async Task<SkillOutput> RunAsync(SkillInput input, ToolGateway tools, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.LichessUsername))
            return AgeDefault(input, "No Lichess username was given.");

        LichessToolOutput profile;
        try
        {
            profile = await tools.CallAsync<LichessToolInput, LichessToolOutput>(
                LichessProfileTool.ToolName, new LichessToolInput(input.LichessUsername), ct);
        }
        catch (ToolFailedException ex)
        {
            // Safe fallback: the failed tool call is already recorded by the gateway.
            return AgeDefault(input, $"Lichess could not be read ({ex.Message}).");
        }

        if (!profile.Found) return AgeDefault(input, $"Lichess user '{input.LichessUsername}' was not found.");
        if (profile.Closed) return AgeDefault(input, "The Lichess account is closed.");
        if (profile.BestRating is null) return AgeDefault(input, "The Lichess account has too few rated games.");

        var prompt = AgentPrompt.WithData("Assess this child's chess level.", new
        {
            age = input.Age,
            lichessUsername = profile.Username,
            bestRating = profile.BestRating,
            ratingType = profile.RatingType,
            totalGames = profile.TotalGames
        });
        var raw = await llm.GenerateJsonAsync(new LlmRequest(Name, SystemInstruction, prompt), ct);
        var answer = AgentJson.ParseLlmOutput<SkillLlmAnswer>(raw);

        // Contract checks on the model's answer before anything uses it.
        if (answer.Level is null || !Enum.IsDefined(answer.Level.Value))
            throw new AgentOutputException("The model did not return a valid level.");
        if (!Confidences.Contains(answer.Confidence))
            throw new AgentOutputException("The model did not return a valid confidence.");
        if (string.IsNullOrWhiteSpace(answer.Rationale) || answer.Rationale.Length > 400)
            throw new AgentOutputException("The model's rationale is missing or too long.");

        // Guardrail: the model may nudge the level, but not jump two levels away from what the rating says.
        var ratingLevel = LevelRules.FromRating(profile.BestRating.Value);
        if (LevelRules.Distance(answer.Level.Value, ratingLevel) > 1)
            throw new AgentOutputException($"Level {answer.Level} is too far from rating {profile.BestRating} ({ratingLevel}).");

        return new SkillOutput(answer.Level.Value, answer.Confidence!, answer.Rationale, "Lichess", profile.BestRating);
    }

    private static SkillOutput AgeDefault(SkillInput input, string why)
    {
        var level = LevelRules.FromAge(input.Age);
        return new SkillOutput(level, "Low", $"{why} Using the age-based default for age {input.Age}.", "AgeDefault", null);
    }

    /// <summary>The raw shape we ask the model for (nullable, because models can omit fields).</summary>
    private record SkillLlmAnswer(ClassLevel? Level, string? Confidence, string? Rationale);
}

/// <summary>Deterministic level rules shared by the agents and the fake LLM.</summary>
public static class LevelRules
{
    public static ClassLevel FromRating(int rating) =>
        rating < 1300 ? ClassLevel.Beginner : rating < 1800 ? ClassLevel.Intermediate : ClassLevel.Advanced;

    /// <summary>No evidence of skill: young children start as Beginners, teens one level up. Always Low confidence.</summary>
    public static ClassLevel FromAge(int age) => age >= 12 ? ClassLevel.Intermediate : ClassLevel.Beginner;

    public static int Distance(ClassLevel a, ClassLevel b) => Math.Abs((int)a - (int)b);
}
