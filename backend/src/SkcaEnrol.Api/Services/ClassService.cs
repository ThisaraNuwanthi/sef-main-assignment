using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SkcaEnrol.Api.Common;
using SkcaEnrol.Api.Data;
using SkcaEnrol.Api.Domain;
using SkcaEnrol.Api.Dtos;

namespace SkcaEnrol.Api.Services;

public interface IClassService
{
    Task<PagedResult<ClassDto>> ListAsync(ClassQuery query, bool isAdmin, CancellationToken ct = default);
    Task<ClassDto> GetAsync(int id, bool isAdmin, CancellationToken ct = default);
    Task<ClassDto> CreateAsync(SaveClassRequest request, CancellationToken ct = default);
    Task<ClassDto> UpdateAsync(int id, SaveClassRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task<List<CoachOptionDto>> ListCoachesAsync(CancellationToken ct = default);
}

public class ClassService(AppDbContext db) : IClassService
{
    // One projection used by every read, so seat maths is written exactly once.
    // Only Approved enrolments hold a seat; pending proposals do not.
    private static readonly Expression<Func<ChessClass, ClassDto>> ToDto = c => new ClassDto(
        c.Id, c.Name, c.Level, c.DayOfWeek, c.StartTime, c.EndTime, c.Capacity,
        c.AssignedEnrolments.Count(e => e.Status == EnrolmentStatus.Approved),
        c.Capacity - c.AssignedEnrolments.Count(e => e.Status == EnrolmentStatus.Approved),
        c.MonthlyFee, c.CoachId, c.Coach!.FullName, c.IsActive);

    public async Task<PagedResult<ClassDto>> ListAsync(ClassQuery q, bool isAdmin, CancellationToken ct = default)
    {
        var query = db.Classes.AsNoTracking();

        // Parents and coaches never see inactive classes, whatever they ask for.
        if (!(isAdmin && q.IncludeInactive))
            query = query.Where(c => c.IsActive);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var pattern = $"%{q.Search.Trim()}%";
            query = query.Where(c => EF.Functions.ILike(c.Name, pattern) || EF.Functions.ILike(c.Coach!.FullName, pattern));
        }
        if (q.Level is not null) query = query.Where(c => c.Level == q.Level);
        if (q.Day is not null) query = query.Where(c => c.DayOfWeek == q.Day);

        IOrderedQueryable<ChessClass> sorted = q.SortBy switch
        {
            "name" => q.Desc ? query.OrderByDescending(c => c.Name) : query.OrderBy(c => c.Name),
            "fee" => q.Desc ? query.OrderByDescending(c => c.MonthlyFee) : query.OrderBy(c => c.MonthlyFee),
            "seatsLeft" => q.Desc
                ? query.OrderByDescending(c => c.Capacity - c.AssignedEnrolments.Count(e => e.Status == EnrolmentStatus.Approved))
                : query.OrderBy(c => c.Capacity - c.AssignedEnrolments.Count(e => e.Status == EnrolmentStatus.Approved)),
            _ => q.Desc ? query.OrderByDescending(c => c.DayOfWeek) : query.OrderBy(c => c.DayOfWeek)
        };
        // Tie-breakers make paging stable: the same row never shows on two pages.
        sorted = sorted.ThenBy(c => c.StartTime).ThenBy(c => c.Id);

        return await PagedResult<ClassDto>.CreateAsync(sorted.Select(ToDto), q.Page, q.PageSize, ct);
    }

    public async Task<ClassDto> GetAsync(int id, bool isAdmin, CancellationToken ct = default)
    {
        var dto = await db.Classes.AsNoTracking()
            .Where(c => c.Id == id && (isAdmin || c.IsActive))
            .Select(ToDto)
            .FirstOrDefaultAsync(ct);
        return dto ?? throw new NotFoundException($"Class {id} was not found.");
    }

    public async Task<ClassDto> CreateAsync(SaveClassRequest request, CancellationToken ct = default)
    {
        await EnsureValidAsync(request, existingId: null, ct);

        var klass = new ChessClass();
        Apply(klass, request);
        db.Classes.Add(klass);
        await db.SaveChangesAsync(ct);

        return await GetAsync(klass.Id, isAdmin: true, ct);
    }

    public async Task<ClassDto> UpdateAsync(int id, SaveClassRequest request, CancellationToken ct = default)
    {
        var klass = await db.Classes.FirstOrDefaultAsync(c => c.Id == id, ct)
                    ?? throw new NotFoundException($"Class {id} was not found.");

        await EnsureValidAsync(request, existingId: id, ct);

        // Shrinking a class below the students already placed would overfill it.
        var seatsTaken = await db.Enrolments.CountAsync(e => e.AssignedClassId == id && e.Status == EnrolmentStatus.Approved, ct);
        if (request.Capacity < seatsTaken)
            throw new ConflictException($"Capacity cannot be lower than the {seatsTaken} students already placed.");

        Apply(klass, request);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, isAdmin: true, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var klass = await db.Classes.FirstOrDefaultAsync(c => c.Id == id, ct)
                    ?? throw new NotFoundException($"Class {id} was not found.");

        // Deleting would break enrolment history, so a used class can only be deactivated.
        var used = await db.Enrolments.AnyAsync(e => e.AssignedClassId == id || e.RequestedClassId == id, ct);
        if (used)
            throw new ConflictException("This class has enrolments. Deactivate it instead of deleting it.");

        db.Classes.Remove(klass);
        await db.SaveChangesAsync(ct);
    }

    public Task<List<CoachOptionDto>> ListCoachesAsync(CancellationToken ct = default) =>
        db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Coach)
            .OrderBy(u => u.FullName)
            .Select(u => new CoachOptionDto(u.Id, u.FullName, u.Email))
            .ToListAsync(ct);

    /// <summary>Business checks that need the database (DTO attributes cover the rest).</summary>
    private async Task EnsureValidAsync(SaveClassRequest r, int? existingId, CancellationToken ct)
    {
        var coachExists = await db.Users.AnyAsync(u => u.Id == r.CoachId && u.Role == UserRole.Coach, ct);
        if (!coachExists)
            throw new BadRequestException("The selected coach does not exist.");

        var name = r.Name.Trim();
        if (await db.Classes.AnyAsync(c => c.Name == name && c.Id != existingId, ct))
            throw new ConflictException($"A class named '{name}' already exists.");

        // A coach cannot teach two active classes at the same time.
        if (r.IsActive)
        {
            var clash = await db.Classes.AnyAsync(c =>
                c.CoachId == r.CoachId && c.IsActive && c.Id != existingId &&
                c.DayOfWeek == r.DayOfWeek && c.StartTime < r.EndTime && r.StartTime < c.EndTime, ct);
            if (clash)
                throw new ConflictException("This coach already teaches another class at that time.");
        }
    }

    private static void Apply(ChessClass klass, SaveClassRequest r)
    {
        // The ! is safe: [Required] on the DTO guarantees these are set before we get here.
        klass.Name = r.Name.Trim();
        klass.Level = r.Level!.Value;
        klass.DayOfWeek = r.DayOfWeek!.Value;
        klass.StartTime = r.StartTime!.Value;
        klass.EndTime = r.EndTime!.Value;
        klass.Capacity = r.Capacity;
        klass.MonthlyFee = r.MonthlyFee;
        klass.CoachId = r.CoachId;
        klass.IsActive = r.IsActive;
    }
}
