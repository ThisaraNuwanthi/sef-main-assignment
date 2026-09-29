using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SkcaEnrol.Api.Agents.Tools;
using SkcaEnrol.Api.Data;
using SkcaEnrol.Api.Domain;
using SkcaEnrol.Api.Services;

namespace SkcaEnrol.Api.Agents.Orchestration;

public class AgentOptions
{
    public const string Section = "Agents";

    public int MaxRetries { get; set; } = 2;           // per step, after the first attempt
    public int StepTimeoutSeconds { get; set; } = 45;  // per attempt
    public bool RunInBackground { get; set; } = true;  // tests switch this off and call RunAsync directly
}

/// <summary>
/// Runs one workflow: asks the PlannerAgent for a plan, then walks the plan step
/// by step, delegating each step to the agent that owns it. Every step and tool
/// call is saved with timings. Any unrecoverable problem ends in Status=Failed
/// with a clear reason (safe failure). Success stops at AwaitingApproval:
/// the orchestrator never books a place; only an Admin can (WorkflowService.ApproveAsync).
/// </summary>
public class WorkflowOrchestrator(
    AppDbContext db,
    PlannerAgent planner,
    SkillAssessmentAgent skillAgent,
    PlacementAgent placementAgent,
    ValidationSafetyAgent validationAgent,
    IEnumerable<IAgentTool> tools,
    IOptions<AgentOptions> options,
    ILogger<WorkflowOrchestrator> logger)
{
    private const string OrchestratorName = "Orchestrator";
    private readonly AgentOptions _options = options.Value;

    public async Task RunAsync(int workflowId, CancellationToken ct = default)
    {
        var workflow = await db.Workflows
            .Include(w => w.Enrolment!).ThenInclude(e => e.Child)
            .FirstOrDefaultAsync(w => w.Id == workflowId, ct);

        // Only start work that is still wanted (the parent may have cancelled meanwhile).
        if (workflow is null || workflow.Status != WorkflowStatus.Queued || workflow.Enrolment!.Status != EnrolmentStatus.Submitted)
        {
            logger.LogInformation("Workflow {WorkflowId} skipped: not queued or enrolment not submitted", workflowId);
            return;
        }

        var enrolment = workflow.Enrolment;
        var child = enrolment.Child!;

        workflow.Status = WorkflowStatus.Running;
        workflow.StartedAt = DateTime.UtcNow;
        EnrolmentStateMachine.Move(enrolment, EnrolmentStatus.AgentProcessing, null, $"Agent workflow #{workflow.Id} started.");
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Workflow {WorkflowId} started for enrolment {EnrolmentId}", workflow.Id, enrolment.Id);

        try
        {
            var stepNo = 0;

            // Step 1 is always planning. The rest of the run follows the validated plan.
            var plan = await RunStepAsync(workflow, ++stepNo, "Plan", PlannerAgent.Name, PlannerAgent.AllowedTools,
                new PlannerInput(workflow.Objective), planner.RunAsync, ct);
            workflow.PlanJson = AgentJson.Serialize(plan);

            SkillOutput? skill = null;
            CandidateSearchOutput? candidates = null;
            PlacementOutput? placement = null;
            ValidationOutput? validation = null;

            foreach (var step in plan.Steps)
            {
                switch (step)
                {
                    case PlanSteps.AssessSkill:
                        var age = child.AgeOn(DateOnly.FromDateTime(DateTime.UtcNow));
                        skill = await RunStepAsync(workflow, ++stepNo, step, SkillAssessmentAgent.Name, SkillAssessmentAgent.AllowedTools,
                            new SkillInput(child.FullName, age, child.LichessUsername), skillAgent.RunAsync, ct);
                        break;

                    case PlanSteps.FindCandidateClasses:
                        candidates = await RunStepAsync(workflow, ++stepNo, step, PlacementAgent.Name, PlacementAgent.AllowedTools,
                            new CandidateSearchInput(skill!.Level, enrolment.PreferredDays, enrolment.PreferredTimeFrom, enrolment.PreferredTimeTo),
                            placementAgent.FindCandidatesAsync, ct);
                        if (candidates.Candidates.Count == 0)
                            throw new WorkflowFailedException(
                                $"No active class with free seats matches level {skill.Level} (±1) on the preferred days and times.");
                        break;

                    case PlanSteps.ProposePlacement:
                        placement = await RunStepAsync(workflow, ++stepNo, step, PlacementAgent.Name, PlacementAgent.AllowedTools,
                            new ProposalInput(child.Id, skill!.Level, candidates!.Candidates, enrolment.RequestedClassId, enrolment.ParentNotes),
                            placementAgent.ProposeAsync, ct);
                        break;

                    case PlanSteps.Validate:
                        validation = await RunStepAsync(workflow, ++stepNo, step, ValidationSafetyAgent.Name, ValidationSafetyAgent.AllowedTools,
                            new ValidationInput(enrolment.Id, child.Id, plan, skill!, candidates!, placement!, enrolment.ParentNotes),
                            validationAgent.RunAsync, ct);
                        foreach (var r in validation.Results)
                            workflow.ValidationResults.Add(new WorkflowValidationResult
                            {
                                RuleName = r.RuleName, Passed = r.Passed, Severity = r.Severity, Message = r.Message
                            });
                        if (!validation.AllHardRulesPassed)
                            throw new WorkflowFailedException("Validation failed: " + string.Join(" ", validation.Results
                                .Where(r => !r.Passed && r.Severity == ValidationSeverity.Error).Select(r => r.Message)));
                        break;

                    case PlanSteps.RequestApproval:
                        var proposal = new PlacementProposal(
                            placement!.ClassId, placement.ClassName, skill!.Level, skill.Confidence, skill.Rationale,
                            placement.Reason, placement.LevelToleranceReason, placement.Fee.Amount,
                            placement.Fee.SiblingDiscountApplied, validation!.InjectionSuspected);
                        await RunStepAsync(workflow, ++stepNo, step, OrchestratorName, new HashSet<string>(), proposal,
                            (p, _, _) => Task.FromResult(p), ct);

                        workflow.FinalOutcome = AgentJson.Serialize(proposal);
                        workflow.Status = WorkflowStatus.AwaitingApproval;
                        workflow.CompletedAt = DateTime.UtcNow;
                        var flag = proposal.InjectionSuspected ? " Parent notes were flagged for manual review." : "";
                        EnrolmentStateMachine.Move(enrolment, EnrolmentStatus.PendingAdminApproval, null,
                            $"Proposed class: {proposal.ClassName}. Waiting for admin approval.{flag}");
                        break;
                }
            }

            await db.SaveChangesAsync(ct);
            logger.LogInformation("Workflow {WorkflowId} finished with {Status}", workflow.Id, workflow.Status);
        }
        catch (NonRetryableAgentException ex)
        {
            await FailAsync(workflow, ex.Message, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Unexpected bug: log the details for developers, store a safe message for the admin.
            logger.LogError(ex, "Workflow {WorkflowId} crashed", workflow.Id);
            await FailAsync(workflow, "Unexpected internal error. See server logs.", ct);
        }
    }

    /// <summary>
    /// Runs one plan step with: a step row, a least-privilege tool gateway, a timeout
    /// per attempt, and up to MaxRetries retries for errors that retrying can fix.
    /// </summary>
    private async Task<TOut> RunStepAsync<TIn, TOut>(
        AgentWorkflow workflow, int stepNo, string stepName, string agentName, IReadOnlySet<string> allowedTools,
        TIn input, Func<TIn, ToolGateway, CancellationToken, Task<TOut>> run, CancellationToken ct)
    {
        var step = new AgentStep
        {
            StepNo = stepNo,
            StepName = stepName,
            AgentName = agentName,
            InputJson = AgentJson.Serialize(input),
            StartedAt = DateTime.UtcNow
        };
        workflow.Steps.Add(step);
        await db.SaveChangesAsync(ct); // saved as Running so the review page shows live progress

        var gateway = new ToolGateway(agentName, allowedTools, tools, step);
        var timer = Stopwatch.StartNew();

        for (var attempt = 0; ; attempt++)
        {
            step.RetryCount = attempt;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(_options.StepTimeoutSeconds));

            try
            {
                var output = await run(input, gateway, timeout.Token);
                step.Status = StepStatus.Succeeded;
                step.OutputJson = AgentJson.Serialize(output);
                step.Error = null;
                step.DurationMs = timer.ElapsedMilliseconds;
                await db.SaveChangesAsync(ct);
                return output;
            }
            catch (NonRetryableAgentException ex)
            {
                await MarkStepFailedAsync(step, timer, ex.Message, ct);
                throw;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                var message = ex is OperationCanceledException
                    ? $"Timed out after {_options.StepTimeoutSeconds}s."
                    : ex.Message;
                logger.LogWarning("Step {Step} ({Agent}) attempt {Attempt} failed: {Error}", stepName, agentName, attempt + 1, message);

                if (attempt >= _options.MaxRetries)
                {
                    await MarkStepFailedAsync(step, timer, message, ct);
                    throw new WorkflowFailedException($"{agentName} failed at step '{stepName}' after {attempt + 1} attempts: {message}");
                }
                step.Error = message; // keep the latest error visible while retrying
            }
        }
    }

    private async Task MarkStepFailedAsync(AgentStep step, Stopwatch timer, string error, CancellationToken ct)
    {
        step.Status = StepStatus.Failed;
        step.Error = Truncate(error);
        step.DurationMs = timer.ElapsedMilliseconds;
        await db.SaveChangesAsync(ct);
    }

    private async Task FailAsync(AgentWorkflow workflow, string reason, CancellationToken ct)
    {
        workflow.Status = WorkflowStatus.Failed;
        workflow.FailureReason = Truncate(reason);
        workflow.CompletedAt = DateTime.UtcNow;
        EnrolmentStateMachine.Move(workflow.Enrolment!, EnrolmentStatus.Failed, null, Truncate(reason, 500));
        await db.SaveChangesAsync(ct);
        logger.LogWarning("Workflow {WorkflowId} failed: {Reason}", workflow.Id, reason);
    }

    private static string Truncate(string text, int max = 1000) => text.Length <= max ? text : text[..max];
}
