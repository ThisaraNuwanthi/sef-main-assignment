using SkcaEnrol.Api.Services;

namespace SkcaEnrol.Api.Agents.Tools;

public record FeeToolInput(int ChildId, int ClassId);

/// <summary>
/// Deterministic tool: asks the FeeService (the single fee rule). The LLM never
/// calculates money; it only receives the result.
/// </summary>
public class FeeCalculatorTool(IFeeService fees) : IAgentTool<FeeToolInput, FeeQuote>
{
    public const string ToolName = "FeeCalculator";
    public string Name => ToolName;

    public Task<FeeQuote> RunAsync(FeeToolInput input, CancellationToken ct) =>
        fees.QuoteAsync(input.ChildId, input.ClassId, ct);
}
