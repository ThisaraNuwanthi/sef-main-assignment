using System.Diagnostics;
using SkcaEnrol.Api.Domain;

namespace SkcaEnrol.Api.Agents.Tools;

/// <summary>Marker for every tool, so DI can hand the gateway the full list.</summary>
public interface IAgentTool
{
    string Name { get; }
}

/// <summary>A tool with a typed input and output (its contract).</summary>
public interface IAgentTool<TIn, TOut> : IAgentTool
{
    Task<TOut> RunAsync(TIn input, CancellationToken ct);
}

/// <summary>
/// The ONLY way an agent can use a tool. Agents never get tool objects directly.
/// For every call the gateway:
///  1. enforces the agent's allow-list (least privilege),
///  2. times the call,
///  3. records a ToolCall row (input, output, success, error) on the current step.
/// </summary>
public class ToolGateway(string agentName, IReadOnlySet<string> allowedTools, IEnumerable<IAgentTool> tools, AgentStep step)
{
    public async Task<TOut> CallAsync<TIn, TOut>(string toolName, TIn input, CancellationToken ct)
    {
        var record = new ToolCall { ToolName = toolName, InputJson = AgentJson.Serialize(input) };
        step.ToolCalls.Add(record);

        if (!allowedTools.Contains(toolName))
        {
            record.Success = false;
            record.Error = "Denied: tool is not on this agent's allow-list.";
            throw new AgentToolNotAllowedException(agentName, toolName);
        }

        var tool = tools.OfType<IAgentTool<TIn, TOut>>().FirstOrDefault(t => t.Name == toolName)
                   ?? throw new InvalidOperationException($"Tool '{toolName}' is not registered.");

        var timer = Stopwatch.StartNew();
        try
        {
            var output = await tool.RunAsync(input, ct);
            record.Success = true;
            record.OutputJson = AgentJson.Serialize(output);
            return output;
        }
        catch (Exception ex)
        {
            record.Success = false;
            record.Error = ex.Message;
            throw;
        }
        finally
        {
            record.DurationMs = timer.ElapsedMilliseconds;
        }
    }
}
