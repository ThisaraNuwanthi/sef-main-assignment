using Microsoft.EntityFrameworkCore;
using SkcaEnrol.Api.Data;
using SkcaEnrol.Api.Domain;
using SkcaEnrol.Api.Services;

namespace SkcaEnrol.Api.Agents.Orchestration;

/// <summary>
/// Background service that runs queued workflows one at a time, outside any HTTP request.
/// Each workflow gets its own DI scope, so it has its own DbContext.
/// </summary>
public class WorkflowWorker(WorkflowQueue queue, IServiceScopeFactory scopes, ILogger<WorkflowWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverAfterRestartAsync(stoppingToken);

        await foreach (var workflowId in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<WorkflowOrchestrator>();
                await orchestrator.RunAsync(workflowId, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One broken workflow must never stop the worker for everyone else.
                logger.LogError(ex, "Workflow {WorkflowId} could not be processed", workflowId);
            }
        }
    }

    /// <summary>
    /// After a crash or redeploy: re-queue workflows that never started, and fail the
    /// ones that were mid-run (we cannot know how far they got), so none hang forever.
    /// </summary>
    private async Task RecoverAfterRestartAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var interrupted = await db.Workflows.Include(w => w.Enrolment)
            .Where(w => w.Status == WorkflowStatus.Running).ToListAsync(ct);
        foreach (var workflow in interrupted)
        {
            workflow.Status = WorkflowStatus.Failed;
            workflow.FailureReason = "Interrupted by a server restart. An admin can retry it.";
            workflow.CompletedAt = DateTime.UtcNow;
            if (workflow.Enrolment!.Status == EnrolmentStatus.AgentProcessing)
                EnrolmentStateMachine.Move(workflow.Enrolment, EnrolmentStatus.Failed, null, workflow.FailureReason);
        }
        await db.SaveChangesAsync(ct);

        var queued = await db.Workflows.Where(w => w.Status == WorkflowStatus.Queued).Select(w => w.Id).ToListAsync(ct);
        foreach (var id in queued) queue.Enqueue(id);

        if (interrupted.Count + queued.Count > 0)
            logger.LogInformation("Recovery: {Failed} interrupted workflows failed, {Queued} re-queued", interrupted.Count, queued.Count);
    }
}
