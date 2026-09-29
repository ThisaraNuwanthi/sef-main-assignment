using Microsoft.EntityFrameworkCore;
using SkcaEnrol.Api.Data;
using SkcaEnrol.Api.Domain;

namespace SkcaEnrol.Api.Agents.Tools;

/// <summary>
/// Read-only database query: active classes with free seats that match the
/// assessed level (or one level either side), the preferred days and the time window.
/// The PlacementAgent's LLM may only pick from what this returns.
/// </summary>
public class ClassSearchTool(AppDbContext db) : IAgentTool<CandidateSearchInput, CandidateSearchOutput>
{
    public const string ToolName = "ClassSearch";
    public string Name => ToolName;

    private const int MaxCandidates = 5;

    public async Task<CandidateSearchOutput> RunAsync(CandidateSearchInput input, CancellationToken ct)
    {
        var level = (int)input.AssessedLevel;
        // Within one level: the exact level, or one above/below (which then needs a recorded reason).
        // Worked out in C# because Level is stored as text, so SQL cannot do the arithmetic.
        var allowedLevels = Enum.GetValues<ClassLevel>().Where(l => Math.Abs((int)l - level) <= 1).ToList();

        var query = db.Classes.AsNoTracking()
            .Where(c => c.IsActive)
            .Where(c => allowedLevels.Contains(c.Level))
            .Where(c => c.Capacity > c.AssignedEnrolments.Count(e => e.Status == EnrolmentStatus.Approved));

        if (input.PreferredDays.Count > 0)
            query = query.Where(c => input.PreferredDays.Contains(c.DayOfWeek));
        if (input.From is not null)
            query = query.Where(c => c.StartTime >= input.From);
        if (input.To is not null)
            query = query.Where(c => c.EndTime <= input.To);

        var rows = await query
            .Select(c => new
            {
                c.Id, c.Name, c.Level, c.DayOfWeek, c.StartTime, c.EndTime, c.MonthlyFee,
                SeatsLeft = c.Capacity - c.AssignedEnrolments.Count(e => e.Status == EnrolmentStatus.Approved)
            })
            .ToListAsync(ct);

        // Best fit first: exact level before neighbouring levels, then the emptiest class.
        var candidates = rows
            .Select(r => new CandidateClass(r.Id, r.Name, r.Level, r.DayOfWeek, r.StartTime, r.EndTime,
                r.SeatsLeft, r.MonthlyFee, Math.Abs((int)r.Level - level)))
            .OrderBy(c => c.LevelDistance)
            .ThenByDescending(c => c.SeatsLeft)
            .ThenBy(c => c.Id)
            .Take(MaxCandidates)
            .ToList();

        return new CandidateSearchOutput(candidates);
    }
}
