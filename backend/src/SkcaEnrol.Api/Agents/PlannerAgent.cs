using SkcaEnrol.Api.Agents.Llm;
using SkcaEnrol.Api.Agents.Tools;

namespace SkcaEnrol.Api.Agents;

/// <summary>
/// Agent 1 (LLM, no tools): turns the objective into a structured plan.
/// The model proposes the plan; PlanValidator decides whether it is acceptable.
/// </summary>
public class PlannerAgent(ILlmClient llm)
{
    public const string Name = "PlannerAgent";
    public static readonly IReadOnlySet<string> AllowedTools = new HashSet<string>(); // least privilege: none

    private const string SystemInstruction = """
        You are the planning agent of a chess academy's enrolment system.
        Produce a plan to place one child in a suitable weekly chess class.
        Use ONLY these step names, each at most once, in a sensible order:
        AssessSkill, FindCandidateClasses, ProposePlacement, Validate, RequestApproval.
        Answer with JSON only, in exactly this shape:
        {"steps": ["StepName", ...], "summary": "one short sentence"}
        """;

    public async Task<PlannerOutput> RunAsync(PlannerInput input, ToolGateway tools, CancellationToken ct)
    {
        var prompt = AgentPrompt.WithData("Create the plan for this objective.",
            new { objective = input.Objective, allowedSteps = PlanSteps.All });

        var raw = await llm.GenerateJsonAsync(new LlmRequest(Name, SystemInstruction, prompt), ct);
        var plan = AgentJson.ParseLlmOutput<PlannerOutput>(raw);

        PlanValidator.EnsureValid(plan);
        return plan;
    }
}

/// <summary>
/// Deterministic checks on the model's plan. The LLM may word the plan, but it can
/// never add unknown steps, skip a step, or skip validation/human approval.
/// </summary>
public static class PlanValidator
{
    public static void EnsureValid(PlannerOutput? plan)
    {
        if (plan?.Steps is null || plan.Steps.Count == 0)
            throw new AgentOutputException("The plan has no steps.");

        var unknown = plan.Steps.Where(s => !PlanSteps.All.Contains(s)).ToList();
        if (unknown.Count > 0)
            throw new AgentOutputException($"The plan contains unknown steps: {string.Join(", ", unknown)}.");

        if (plan.Steps.Distinct().Count() != plan.Steps.Count)
            throw new AgentOutputException("The plan repeats a step.");

        // Every step is needed (each one feeds the next), and they must run in dependency order.
        if (!plan.Steps.SequenceEqual(PlanSteps.All))
            throw new AgentOutputException(
                $"The plan must be exactly: {string.Join(" -> ", PlanSteps.All)}. Got: {string.Join(" -> ", plan.Steps)}.");
    }
}
