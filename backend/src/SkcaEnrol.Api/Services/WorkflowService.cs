using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SkcaEnrol.Api.Agents;
using SkcaEnrol.Api.Agents.Orchestration;
using SkcaEnrol.Api.Common;
using SkcaEnrol.Api.Data;
using SkcaEnrol.Api.Domain;
using SkcaEnrol.Api.Dtos;

namespace SkcaEnrol.Api.Services;

public interface IWorkflowService
{
    Task<WorkflowDto> GetAsync(int id, CancellationToken ct = default);
    Task<List<WorkflowListItemDto>> ListForEnrolmentAsync(int enrolmentId, CancellationToken ct = default);
    Task<WorkflowDto> ApproveAsync(int id, int adminId, string? note, CancellationToken ct = default);
    Task<WorkflowDto> RejectAsync(int id, int adminId, string? note, CancellationToken ct = default);
    Task<WorkflowDto> RequestRevisionAsync(int id, int adminId, string? note, CancellationToken ct = default);
    Task<RetryResultDto> RetryAsync(int id, int adminId, CancellationToken ct = default);
}

public class WorkflowService(AppDbContext db, IFeeService fees, WorkflowQueue queue, ILogger<WorkflowService> logger) : IWorkflowService
{
    public async Task<WorkflowDto> GetAsync(int id, CancellationToken ct = default)
    {
        var w = await db.Workflows.AsNoTracking()
            .Include(x => x.Enrolment!).ThenInclude(e => e.Child!).ThenInclude(c => c.Parent)
            .Include(x => x.Steps).ThenInclude(s => s.ToolCalls)
            .Include(x => x.ValidationResults)
            .Include(x => x.ApprovalDecisions).ThenInclude(d => d.AdminUser)
            .AsSplitQuery() // several collections: split into a few simple queries instead of one huge join
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException($"Workflow {id} was not found.");

        long? total = w.StartedAt is not null && w.CompletedAt is not null
            ? (long)(w.CompletedAt.Value - w.StartedAt.Value).TotalMilliseconds
            : null;

        return new WorkflowDto(
            w.Id, w.EnrolmentId, w.Enrolment!.Status, w.Enrolment.Child!.FullName, w.Enrolment.Child.Parent!.FullName,
            w.Enrolment.ParentNotes, w.Objective, w.Status, Json(w.PlanJson), Json(w.FinalOutcome), w.FailureReason,
            w.CreatedAt, w.StartedAt, w.CompletedAt, total,
            w.Steps.OrderBy(s => s.StepNo).Select(s => new AgentStepDto(
                s.Id, s.StepNo, s.StepName, s.AgentName, s.Status, Json(s.InputJson), Json(s.OutputJson),
                s.DurationMs, s.Error, s.RetryCount, s.StartedAt,
                s.ToolCalls.OrderBy(t => t.Id).Select(t => new ToolCallDto(
                    t.Id, t.ToolName, Json(t.InputJson), Json(t.OutputJson), t.Success, t.DurationMs, t.Error, t.CreatedAt)).ToList()
            )).ToList(),
            w.ValidationResults.OrderBy(v => v.Id).Select(v => new ValidationResultDto(v.RuleName, v.Passed, v.Severity, v.Message)).ToList(),
            w.ApprovalDecisions.OrderBy(d => d.DecidedAt).Select(d => new ApprovalDecisionDto(d.Decision, d.AdminUser!.FullName, d.Note, d.DecidedAt)).ToList());
    }

    public Task<List<WorkflowListItemDto>> ListForEnrolmentAsync(int enrolmentId, CancellationToken ct = default) =>
        db.Workflows.AsNoTracking()
            .Where(w => w.EnrolmentId == enrolmentId)
            .OrderByDescending(w => w.Id)
            .Select(w => new WorkflowListItemDto(w.Id, w.EnrolmentId, w.Enrolment!.Child!.FullName, w.Status, w.CreatedAt, w.CompletedAt))
            .ToListAsync(ct);

    /// <summary>
    /// The high-impact action, done in ONE database transaction:
    /// lock the class row, re-check capacity/clash/active, set the class, create the fee,
    /// write history + the approval decision, then commit. Any failure rolls everything back.
    /// </summary>
    public async Task<WorkflowDto> ApproveAsync(int id, int adminId, string? note, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var workflow = await LoadForDecisionAsync(id, ct);
        var proposal = AgentJson.Deserialize<PlacementProposal>(workflow.FinalOutcome)
                       ?? throw new ConflictException("This workflow has no proposal to approve.");
        var enrolment = workflow.Enrolment!;

        // SELECT ... FOR UPDATE: other approvals for the same class wait here until we commit,
        // so two admins can never both take the last seat.
        var klass = await db.Classes
            .FromSql($"""SELECT * FROM "Classes" WHERE "Id" = {proposal.ClassId} FOR UPDATE""")
            .SingleOrDefaultAsync(ct)
            ?? throw new ConflictException("The proposed class no longer exists.");

        if (!klass.IsActive)
            throw new ConflictException($"'{klass.Name}' is no longer active.");

        // Re-check capacity now: seats may have been taken since the agents looked.
        var seatsTaken = await db.Enrolments.CountAsync(e => e.AssignedClassId == klass.Id && e.Status == EnrolmentStatus.Approved, ct);
        if (seatsTaken >= klass.Capacity)
            throw new ConflictException($"'{klass.Name}' is full ({seatsTaken}/{klass.Capacity}). Request a revision or reject.");

        var clash = await db.Enrolments
            .Where(e => e.ChildId == enrolment.ChildId && e.Status == EnrolmentStatus.Approved && e.Id != enrolment.Id)
            .Select(e => e.AssignedClass!)
            .Where(c => c.DayOfWeek == klass.DayOfWeek && c.StartTime < klass.EndTime && klass.StartTime < c.EndTime)
            .AnyAsync(ct);
        if (clash)
            throw new ConflictException("The child already has a class at an overlapping time.");

        // Price again with the single fee rule (a sibling may have been approved meanwhile).
        var fee = await fees.QuoteAsync(enrolment.ChildId, klass.Id, ct);

        enrolment.AssignedClassId = klass.Id;
        EnrolmentStateMachine.Move(enrolment, EnrolmentStatus.Approved, adminId,
            string.IsNullOrWhiteSpace(note) ? $"Approved: placed in {klass.Name}." : note.Trim());

        var now = DateTime.UtcNow;
        db.FeeRecords.Add(new FeeRecord
        {
            EnrolmentId = enrolment.Id,
            Month = new DateOnly(now.Year, now.Month, 1),
            Amount = fee.Amount,
            SiblingDiscountApplied = fee.SiblingDiscountApplied
        });

        workflow.Status = WorkflowStatus.Approved;
        workflow.ApprovalDecisions.Add(new ApprovalDecision
        {
            AdminUserId = adminId, Decision = ApprovalDecisionType.Approved, Note = note?.Trim(), DecidedAt = now
        });

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation("Workflow {WorkflowId} approved by admin {AdminId}: class {ClassId}, fee {Fee}", id, adminId, klass.Id, fee.Amount);

        return await GetAsync(id, ct);
    }

    public Task<WorkflowDto> RejectAsync(int id, int adminId, string? note, CancellationToken ct = default) =>
        DecideAsync(id, adminId, note, ApprovalDecisionType.Rejected, EnrolmentStatus.Rejected, WorkflowStatus.Rejected, ct);

    public Task<WorkflowDto> RequestRevisionAsync(int id, int adminId, string? note, CancellationToken ct = default) =>
        DecideAsync(id, adminId, note, ApprovalDecisionType.RevisionRequested, EnrolmentStatus.RevisionRequested, WorkflowStatus.RevisionRequested, ct);

    /// <summary>Re-runs a failed request as a NEW workflow; the failed one is kept for the audit trail.</summary>
    public async Task<RetryResultDto> RetryAsync(int id, int adminId, CancellationToken ct = default)
    {
        var old = await db.Workflows.Include(w => w.Enrolment).FirstOrDefaultAsync(w => w.Id == id, ct)
                  ?? throw new NotFoundException($"Workflow {id} was not found.");
        if (old.Status != WorkflowStatus.Failed || old.Enrolment!.Status != EnrolmentStatus.Failed)
            throw new ConflictException("Only failed workflows can be retried.");

        var enrolment = old.Enrolment;
        EnrolmentStateMachine.Move(enrolment, EnrolmentStatus.Submitted, adminId, $"Admin retried after failed workflow #{old.Id}.");
        var fresh = new AgentWorkflow { EnrolmentId = enrolment.Id, Objective = old.Objective };
        db.Workflows.Add(fresh);
        await db.SaveChangesAsync(ct);

        queue.Enqueue(fresh.Id);
        return new RetryResultDto(fresh.Id, enrolment.Status);
    }

    // ---------- helpers ----------

    private async Task<WorkflowDto> DecideAsync(int id, int adminId, string? note, ApprovalDecisionType decision,
        EnrolmentStatus enrolmentStatus, WorkflowStatus workflowStatus, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(note))
            throw new BadRequestException("Please add a note so the parent knows why.");

        var workflow = await LoadForDecisionAsync(id, ct);
        EnrolmentStateMachine.Move(workflow.Enrolment!, enrolmentStatus, adminId, note.Trim());
        workflow.Status = workflowStatus;
        workflow.ApprovalDecisions.Add(new ApprovalDecision
        {
            AdminUserId = adminId, Decision = decision, Note = note.Trim(), DecidedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>A decision is only possible while the proposal is waiting for one.</summary>
    private async Task<AgentWorkflow> LoadForDecisionAsync(int id, CancellationToken ct)
    {
        var workflow = await db.Workflows.Include(w => w.Enrolment).FirstOrDefaultAsync(w => w.Id == id, ct)
                       ?? throw new NotFoundException($"Workflow {id} was not found.");
        if (workflow.Status != WorkflowStatus.AwaitingApproval || workflow.Enrolment!.Status != EnrolmentStatus.PendingAdminApproval)
            throw new ConflictException($"This workflow is {workflow.Status}; only proposals awaiting approval can be decided.");
        return workflow;
    }

    private static JsonElement? Json(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone(); // Clone so the element outlives the disposed document
    }
}
