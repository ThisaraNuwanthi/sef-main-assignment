using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SkcaEnrol.Api.Agents.Llm;

/// <summary>
/// Google Gemini (free tier) via its REST API, in JSON output mode.
/// The API key comes from the GEMINI_API_KEY environment variable.
/// </summary>
public class GeminiLlmClient(HttpClient http, IOptions<LlmOptions> options, IConfiguration config, ILogger<GeminiLlmClient> logger)
    : ILlmClient
{
    public async Task<string> GenerateJsonAsync(LlmRequest request, CancellationToken ct = default)
    {
        var apiKey = config["GEMINI_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new LlmUnavailableException("GEMINI_API_KEY is not configured.");

        var body = new
        {
            systemInstruction = new { parts = new[] { new { text = request.SystemInstruction } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = request.UserContent } } } },
            generationConfig = new
            {
                responseMimeType = "application/json", // JSON mode: the model must answer with JSON
                temperature = 0.2                      // low randomness for repeatable decisions
            }
        };

        // Gemini sometimes answers 503 (overloaded) or 429 (rate limit) for a short while. Instead of failing
        // the step, try the backup model(s) once each; any other error is reported straight away.
        var models = new[] { options.Value.Model }.Concat(options.Value.FallbackModels).Distinct().ToArray();
        for (var i = 0; ; i++)
        {
            try
            {
                return await CallModelAsync(models[i], body, apiKey, request.AgentName, ct);
            }
            catch (GeminiBusyException ex) when (i < models.Length - 1)
            {
                logger.LogWarning("Gemini model {Model} is busy (HTTP {Status}); trying {Next}", models[i], ex.Status, models[i + 1]);
            }
        }
    }

    private async Task<string> CallModelAsync(string model, object body, string apiKey, string agentName, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{model}:generateContent")
        {
            Content = JsonContent.Create(body)
        };
        // Key in a header, not the URL, so it never shows up in request logs.
        message.Headers.Add("x-goog-api-key", apiKey);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(message, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new LlmUnavailableException($"Could not reach Gemini: {ex.Message}");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                logger.LogWarning("Gemini ({Model}) returned {Status} for {Agent}", model, status, agentName);
                if (status is 429 or 500 or 503)
                    throw new GeminiBusyException(status);
                throw new LlmUnavailableException($"Gemini returned HTTP {status}.");
            }

            try
            {
                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                // Shape: { candidates: [ { content: { parts: [ { text: "<json>" } ] } } ] }
                return doc.RootElement.GetProperty("candidates")[0]
                           .GetProperty("content").GetProperty("parts")[0]
                           .GetProperty("text").GetString()
                       ?? throw new LlmUnavailableException("Gemini returned an empty answer.");
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or IndexOutOfRangeException or InvalidOperationException)
            {
                throw new LlmUnavailableException("Gemini returned an unexpected response shape.");
            }
        }
    }
}

/// <summary>Gemini is temporarily overloaded or rate-limited; still a retryable LLM failure.</summary>
public class GeminiBusyException(int status) : LlmUnavailableException($"Gemini returned HTTP {status}.")
{
    public int Status { get; } = status;
}
