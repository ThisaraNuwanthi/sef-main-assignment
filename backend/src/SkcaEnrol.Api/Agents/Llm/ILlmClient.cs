namespace SkcaEnrol.Api.Agents.Llm;

/// <param name="AgentName">Which agent is asking (used for logging and by the fake client).</param>
/// <param name="SystemInstruction">Our fixed instructions for that agent.</param>
/// <param name="UserContent">The task plus the delimited DATA block.</param>
public record LlmRequest(string AgentName, string SystemInstruction, string UserContent);

/// <summary>
/// Agents depend on this interface, never on a vendor SDK. Config picks the
/// implementation: Gemini for real runs, Fake for tests and offline demos.
/// </summary>
public interface ILlmClient
{
    /// <summary>Returns the model's raw answer, which should be a JSON object.</summary>
    Task<string> GenerateJsonAsync(LlmRequest request, CancellationToken ct = default);
}

public class LlmOptions
{
    public const string Section = "Llm";

    /// <summary>"Fake" (default, offline) or "Gemini".</summary>
    public string Provider { get; set; } = "Fake";
    public string Model { get; set; } = "gemini-2.5-flash";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/";
}
