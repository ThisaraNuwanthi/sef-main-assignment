using Microsoft.EntityFrameworkCore;
using SkcaEnrol.Api.Data;
using SkcaEnrol.Api.Domain;

namespace SkcaEnrol.Api.Services;

public record StatusCountDto(EnrolmentStatus Status, int Count);
public record ClassFillDto(int ClassId, string ClassName, ClassLevel Level, int Capacity, int SeatsTaken, double FillRatePercent);
public record MonthlyFeeTotalDto(string Month, decimal Total, int Records);
public record AgentStatsDto(int TotalWorkflows, int AwaitingApproval, int Failed, double? AverageDurationMs, double? SuccessRatePercent);

public record EnrolmentSummaryDto(
    int TotalEnrolments,
    List<StatusCountDto> ByStatus,
    List<ClassFillDto> ClassFill,
    List<MonthlyFeeTotalDto> MonthlyFees,
    AgentStatsDto Agents);

public interface IReportService
{
    Task<EnrolmentSummaryDto> EnrolmentSummaryAsync(CancellationToken ct = default);
}

/// <summary>Numbers for the Admin dashboard. Read-only.</summary>
public class ReportService(AppDbContext db) : IReportService
{
    public async Task<EnrolmentSummaryDto> EnrolmentSummaryAsync(CancellationToken ct = default)
    {
        var counts = await db.Enrolments.GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        // Every status appears, even with 0, so charts keep a stable shape.
        var byStatus = Enum.GetValues<EnrolmentStatus>()
            .Select(s => new StatusCountDto(s, counts.FirstOrDefault(c => c.Status == s)?.Count ?? 0))
            .ToList();

        var fill = await db.Classes.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.DayOfWeek).ThenBy(c => c.StartTime)
            .Select(c => new
            {
                c.Id, c.Name, c.Level, c.Capacity,
                Taken = c.AssignedEnrolments.Count(e => e.Status == EnrolmentStatus.Approved)
            })
            .ToListAsync(ct);
        var classFill = fill
            .Select(c => new ClassFillDto(c.Id, c.Name, c.Level, c.Capacity, c.Taken, Math.Round(100.0 * c.Taken / c.Capacity, 1)))
            .ToList();

        // Last 6 months of fee records, including months with no fees (as 0).
        var now = DateTime.UtcNow;
        var firstMonth = new DateOnly(now.Year, now.Month, 1).AddMonths(-5);
        var fees = await db.FeeRecords.Where(f => f.Month >= firstMonth)
            .GroupBy(f => f.Month)
            .Select(g => new { Month = g.Key, Total = g.Sum(f => f.Amount), Count = g.Count() })
            .ToListAsync(ct);
        var monthlyFees = Enumerable.Range(0, 6)
            .Select(i => firstMonth.AddMonths(i))
            .Select(m =>
            {
                var row = fees.FirstOrDefault(f => f.Month == m);
                return new MonthlyFeeTotalDto(m.ToString("yyyy-MM"), row?.Total ?? 0, row?.Count ?? 0);
            })
            .ToList();

        var finished = await db.Workflows.AsNoTracking()
            .Where(w => w.StartedAt != null && w.CompletedAt != null)
            .Select(w => new { w.Status, w.StartedAt, w.CompletedAt })
            .ToListAsync(ct);
        var total = await db.Workflows.CountAsync(ct);
        var awaiting = await db.Workflows.CountAsync(w => w.Status == WorkflowStatus.AwaitingApproval, ct);
        var failed = await db.Workflows.CountAsync(w => w.Status == WorkflowStatus.Failed, ct);
        double? avgMs = finished.Count == 0 ? null
            : Math.Round(finished.Average(w => (w.CompletedAt!.Value - w.StartedAt!.Value).TotalMilliseconds), 0);
        double? successRate = finished.Count == 0 ? null
            : Math.Round(100.0 * finished.Count(w => w.Status != WorkflowStatus.Failed) / finished.Count, 1);

        return new EnrolmentSummaryDto(
            byStatus.Sum(s => s.Count), byStatus, classFill, monthlyFees,
            new AgentStatsDto(total, awaiting, failed, avgMs, successRate));
    }
}
