using Microsoft.EntityFrameworkCore;
using SkcaEnrol.Api.Agents.Orchestration;
using SkcaEnrol.Api.Common;
using SkcaEnrol.Api.Data;
using SkcaEnrol.Api.Domain;
using SkcaEnrol.Api.Dtos;

namespace SkcaEnrol.Api.Services;

public interface IEnrolmentService
{
    Task<EnrolmentCreatedDto> CreateAsync(int parentId, CreateEnrolmentRequest request, CancellationToken ct = default);
    Task<PagedResult<EnrolmentListItemDto>> ListAsync(EnrolmentQuery query, int userId, UserRole role, CancellationToken ct = default);
    Task<EnrolmentDetailDto> GetAsync(int id, int userId, UserRole role, CancellationToken ct = default);
    Task<EnrolmentDetailDto> UpdateAsync(int id, int parentId, UpdateEnrolmentRequest request, CancellationToken ct = default);
    Task<EnrolmentDetailDto> CancelAsync(int id, int userId, UserRole role, string? reason, CancellationToken ct = default);
    Task<List<StatusHistoryDto>> HistoryAsync(int id, int userId, UserRole role, CancellationToken ct = default);
}

public class EnrolmentService(AppDbContext db, WorkflowQueue queue) : IEnrolmentService
{
    // Statuses in which a request is still "open"; a child may have only one open request at a time.
    private static readonly EnrolmentStatus[] OpenStatuses =
    {
        EnrolmentStatus.Submitted, EnrolmentStatus.AgentProcessing,
        EnrolmentStatus.PendingAdminApproval, EnrolmentStatus.RevisionRequested
    };

    private static readonly EnrolmentStatus[] EditableStatuses = { EnrolmentStatus.Submitted, EnrolmentStatus.RevisionRequested };

    // Everything except mid-processing and final states can be cancelled.
    private static readonly EnrolmentStatus[] CancellableStatuses =
    {
        EnrolmentStatus.Submitted, EnrolmentStatus.PendingAdminApproval, EnrolmentStatus.RevisionRequested,
        EnrolmentStatus.Failed, EnrolmentStatus.Approved
    };

    /// <summary>Saves the request, then hands it to the agent workflow (runs in the background).</summary>
    public async Task<EnrolmentCreatedDto> CreateAsync(int parentId, CreateEnrolmentRequest request, CancellationToken ct = default)
    {
        var child = await db.Children.FirstOrDefaultAsync(c => c.Id == request.ChildId, ct)
                    ?? throw new NotFoundException($"Child {request.ChildId} was not found.");
        if (child.ParentId != parentId)
            throw new ForbiddenException("You can only enrol your own children.");

        if (await db.Enrolments.AnyAsync(e => e.ChildId == child.Id && OpenStatuses.Contains(e.Status), ct))
            throw new ConflictException($"{child.FullName} already has an open enrolment request.");

        var requested = await LoadRequestedClassAsync(request.RequestedClassId, ct);

        var enrolment = new Enrolment { ChildId = child.Id, Child = child, Status = EnrolmentStatus.Submitted };
        Apply(enrolment, request);
        enrolment.History.Add(new EnrolmentStatusHistory
        {
            ToStatus = EnrolmentStatus.Submitted, ChangedByUserId = parentId, ChangedAt = DateTime.UtcNow, Note = "Request submitted by parent."
        });

        var workflow = NewWorkflow(enrolment, child, requested);
        db.Enrolments.Add(enrolment);
        await db.SaveChangesAsync(ct);

        queue.Enqueue(workflow.Id); // after the save, so the worker can find the rows
        return new EnrolmentCreatedDto(enrolment.Id, workflow.Id, enrolment.Status);
    }

    public async Task<PagedResult<EnrolmentListItemDto>> ListAsync(EnrolmentQuery q, int userId, UserRole role, CancellationToken ct = default)
    {
        var query = db.Enrolments.AsNoTracking();

        // Ownership: parents only ever see their own children's requests.
        if (role == UserRole.Parent) query = query.Where(e => e.Child!.ParentId == userId);
        else if (role != UserRole.Admin) throw new ForbiddenException("Only admins and parents can list enrolments.");

        if (q.Status is not null) query = query.Where(e => e.Status == q.Status);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var pattern = $"%{q.Search.Trim()}%";
            query = query.Where(e => EF.Functions.ILike(e.Child!.FullName, pattern)
                                     || (e.AssignedClass != null && EF.Functions.ILike(e.AssignedClass.Name, pattern))
                                     || (e.RequestedClass != null && EF.Functions.ILike(e.RequestedClass.Name, pattern)));
        }

        IOrderedQueryable<Enrolment> sorted = q.SortBy switch
        {
            "child" => q.Desc ? query.OrderByDescending(e => e.Child!.FullName) : query.OrderBy(e => e.Child!.FullName),
            "status" => q.Desc ? query.OrderByDescending(e => e.Status) : query.OrderBy(e => e.Status),
            "updated" => q.Desc ? query.OrderByDescending(e => e.UpdatedAt) : query.OrderBy(e => e.UpdatedAt),
            _ => q.Desc ? query.OrderByDescending(e => e.CreatedAt) : query.OrderBy(e => e.CreatedAt)
        };

        var projected = sorted.ThenBy(e => e.Id).Select(e => new EnrolmentListItemDto(
            e.Id, e.ChildId, e.Child!.FullName, e.Child.Parent!.FullName, e.Status,
            e.RequestedClass != null ? e.RequestedClass.Name : null,
            e.AssignedClass != null ? e.AssignedClass.Name : null,
            e.Workflows.OrderByDescending(w => w.Id).Select(w => (int?)w.Id).FirstOrDefault(),
            e.CreatedAt, e.UpdatedAt));

        return await PagedResult<EnrolmentListItemDto>.CreateAsync(projected, q.Page, q.PageSize, ct);
    }

    public async Task<EnrolmentDetailDto> GetAsync(int id, int userId, UserRole role, CancellationToken ct = default)
    {
        await LoadAuthorisedAsync(id, userId, role, ct);
        return await BuildDetailAsync(id, ct);
    }

    /// <summary>
    /// Parent edits a request. Allowed only before processing (Submitted) or after the
    /// admin asked for changes (RevisionRequested); the latter resubmits it to the agents.
    /// </summary>
    public async Task<EnrolmentDetailDto> UpdateAsync(int id, int parentId, UpdateEnrolmentRequest request, CancellationToken ct = default)
    {
        var enrolment = await LoadAuthorisedAsync(id, parentId, UserRole.Parent, ct);
        if (!EditableStatuses.Contains(enrolment.Status))
            throw new ConflictException($"A {enrolment.Status} request can no longer be edited.");

        var requested = await LoadRequestedClassAsync(request.RequestedClassId, ct);
        Apply(enrolment, request);

        AgentWorkflow? workflow = null;
        if (enrolment.Status == EnrolmentStatus.RevisionRequested)
        {
            EnrolmentStateMachine.Move(enrolment, EnrolmentStatus.Submitted, parentId, "Parent updated the request and resubmitted it.");
            workflow = NewWorkflow(enrolment, enrolment.Child!, requested);
        }

        await db.SaveChangesAsync(ct); // the xmin concurrency token stops a clash with the agent worker
        if (workflow is not null) queue.Enqueue(workflow.Id);

        return await BuildDetailAsync(id, ct);
    }

    public async Task<EnrolmentDetailDto> CancelAsync(int id, int userId, UserRole role, string? reason, CancellationToken ct = default)
    {
        var enrolment = await LoadAuthorisedAsync(id, userId, role, ct);
        if (!CancellableStatuses.Contains(enrolment.Status))
            throw new ConflictException(enrolment.Status == EnrolmentStatus.AgentProcessing
                ? "The agents are working on this request right now. Try again in a moment."
                : $"A {enrolment.Status} request cannot be cancelled.");

        EnrolmentStateMachine.Move(enrolment, EnrolmentStatus.Cancelled, userId, string.IsNullOrWhiteSpace(reason) ? "Cancelled." : reason.Trim());

        // A pending proposal is no longer needed.
        var openWorkflows = await db.Workflows
            .Where(w => w.EnrolmentId == id && (w.Status == WorkflowStatus.Queued || w.Status == WorkflowStatus.AwaitingApproval))
            .ToListAsync(ct);
        foreach (var w in openWorkflows) w.Status = WorkflowStatus.Cancelled;

        await db.SaveChangesAsync(ct);
        return await BuildDetailAsync(id, ct);
    }

    public async Task<List<StatusHistoryDto>> HistoryAsync(int id, int userId, UserRole role, CancellationToken ct = default)
    {
        await LoadAuthorisedAsync(id, userId, role, ct);
        return await db.EnrolmentStatusHistory.AsNoTracking()
            .Where(h => h.EnrolmentId == id)
            .OrderBy(h => h.ChangedAt).ThenBy(h => h.Id)
            .Select(h => new StatusHistoryDto(h.FromStatus, h.ToStatus,
                h.ChangedByUser != null ? h.ChangedByUser.FullName : "System (agent workflow)", h.Note, h.ChangedAt))
            .ToListAsync(ct);
    }

    // ---------- helpers ----------

    /// <summary>Ownership check: admins see all; a parent only their own child's requests; coaches none.</summary>
    private async Task<Enrolment> LoadAuthorisedAsync(int id, int userId, UserRole role, CancellationToken ct)
    {
        var enrolment = await db.Enrolments.Include(e => e.Child).FirstOrDefaultAsync(e => e.Id == id, ct)
                        ?? throw new NotFoundException($"Enrolment {id} was not found.");

        var allowed = role == UserRole.Admin || (role == UserRole.Parent && enrolment.Child!.ParentId == userId);
        if (!allowed) throw new ForbiddenException("You can only access your own children's enrolments.");
        return enrolment;
    }

    private async Task<ChessClass?> LoadRequestedClassAsync(int? classId, CancellationToken ct)
    {
        if (classId is null) return null;
        return await db.Classes.FirstOrDefaultAsync(c => c.Id == classId && c.IsActive, ct)
               ?? throw new BadRequestException("The requested class does not exist or is not active.");
    }

    private AgentWorkflow NewWorkflow(Enrolment enrolment, Child child, ChessClass? requested)
    {
        var workflow = new AgentWorkflow { Enrolment = enrolment, Objective = ObjectiveBuilder.Build(child, enrolment, requested) };
        db.Workflows.Add(workflow);
        return workflow;
    }

    private static void Apply(Enrolment e, EnrolmentPreferences r)
    {
        e.RequestedClassId = r.RequestedClassId;
        e.PreferredDays = r.PreferredDays.Distinct().OrderBy(d => d).ToList();
        e.PreferredTimeFrom = r.PreferredTimeFrom;
        e.PreferredTimeTo = r.PreferredTimeTo;
        e.ParentNotes = string.IsNullOrWhiteSpace(r.ParentNotes) ? null : r.ParentNotes.Trim();
    }

    private async Task<EnrolmentDetailDto> BuildDetailAsync(int id, CancellationToken ct)
    {
        var e = await db.Enrolments.AsNoTracking()
            .Include(x => x.Child!).ThenInclude(c => c.Parent)
            .Include(x => x.RequestedClass!).ThenInclude(c => c.Coach)
            .Include(x => x.AssignedClass!).ThenInclude(c => c.Coach)
            .Include(x => x.FeeRecords)
            .FirstAsync(x => x.Id == id, ct);

        var latest = await db.Workflows.AsNoTracking()
            .Where(w => w.EnrolmentId == id)
            .OrderByDescending(w => w.Id)
            .Select(w => new
            {
                w.Id, w.Status,
                Note = w.ApprovalDecisions.OrderByDescending(d => d.DecidedAt).Select(d => d.Note).FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        return new EnrolmentDetailDto(
            e.Id, e.ChildId, e.Child!.FullName, e.Child.Parent!.FullName, e.Status,
            e.PreferredDays, e.PreferredTimeFrom, e.PreferredTimeTo, e.ParentNotes,
            ToSummary(e.RequestedClass), ToSummary(e.AssignedClass),
            e.FeeRecords.OrderByDescending(f => f.Month).Select(f => new FeeRecordDto(f.Month, f.Amount, f.SiblingDiscountApplied, f.Status)).ToList(),
            latest?.Id, latest?.Status, latest?.Note,
            EditableStatuses.Contains(e.Status), CancellableStatuses.Contains(e.Status),
            e.CreatedAt, e.UpdatedAt);
    }

    private static ClassSummaryDto? ToSummary(ChessClass? c) =>
        c is null ? null : new ClassSummaryDto(c.Id, c.Name, c.Level, c.DayOfWeek, c.StartTime, c.EndTime, c.Coach?.FullName ?? "");
}

/// <summary>
/// Turns a parent's request into the agents' objective, e.g.
/// "Place Kavindu Fernando (age 9, Lichess: thibault) in a suitable class on Saturday between 14:00 and 18:00."
/// Parent notes are deliberately NOT included: they are untrusted and travel separately as data.
/// </summary>
public static class ObjectiveBuilder
{
    public static string Build(Child child, Enrolment enrolment, ChessClass? requested)
    {
        var age = child.AgeOn(DateOnly.FromDateTime(DateTime.UtcNow));
        var lichess = child.LichessUsername is null ? "no Lichess account" : $"Lichess: {child.LichessUsername}";
        var days = string.Join(" or ", enrolment.PreferredDays);

        var time = (enrolment.PreferredTimeFrom, enrolment.PreferredTimeTo) switch
        {
            ({ } from, { } to) => $" between {from:HH\\:mm} and {to:HH\\:mm}",
            ({ } from, null) => $" after {from:HH\\:mm}",
            (null, { } to) => $" before {to:HH\\:mm}",
            _ => ""
        };
        var wish = requested is null ? "" : $" The parent asked for '{requested.Name}' if suitable.";

        return $"Place {child.FullName} (age {age}, {lichess}) in a suitable class on {days}{time}.{wish}";
    }
}
