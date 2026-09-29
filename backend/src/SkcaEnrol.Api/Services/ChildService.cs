using Microsoft.EntityFrameworkCore;
using SkcaEnrol.Api.Common;
using SkcaEnrol.Api.Data;
using SkcaEnrol.Api.Domain;
using SkcaEnrol.Api.Dtos;

namespace SkcaEnrol.Api.Services;

public interface IChildService
{
    Task<List<ChildDto>> ListMineAsync(int parentId, CancellationToken ct = default);
    Task<ChildDto> GetAsync(int id, int userId, UserRole role, CancellationToken ct = default);
    Task<ChildDto> CreateAsync(int parentId, SaveChildRequest request, CancellationToken ct = default);
    Task<ChildDto> UpdateAsync(int id, int parentId, SaveChildRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, int parentId, CancellationToken ct = default);
    Task<ChildDto> SetPhotoAsync(int id, int parentId, IFormFile file, CancellationToken ct = default);
    Task<string> GetPhotoFullPathAsync(int id, int userId, UserRole role, CancellationToken ct = default);
}

public class ChildService(AppDbContext db, IPhotoStorage photos) : IChildService
{
    public async Task<List<ChildDto>> ListMineAsync(int parentId, CancellationToken ct = default)
    {
        var children = await db.Children.AsNoTracking()
            .Where(c => c.ParentId == parentId)
            .OrderBy(c => c.FullName)
            .ToListAsync(ct);
        return children.Select(ToDto).ToList();
    }

    public async Task<ChildDto> GetAsync(int id, int userId, UserRole role, CancellationToken ct = default) =>
        ToDto(await LoadAuthorisedAsync(id, userId, role, ct));

    public async Task<ChildDto> CreateAsync(int parentId, SaveChildRequest request, CancellationToken ct = default)
    {
        // ParentId always comes from the token, never the request body,
        // so a parent cannot create a child under someone else's account.
        var child = new Child { ParentId = parentId };
        Apply(child, request);
        db.Children.Add(child);
        await db.SaveChangesAsync(ct);
        return ToDto(child);
    }

    public async Task<ChildDto> UpdateAsync(int id, int parentId, SaveChildRequest request, CancellationToken ct = default)
    {
        var child = await LoadAuthorisedAsync(id, parentId, UserRole.Parent, ct);
        Apply(child, request);
        await db.SaveChangesAsync(ct);
        return ToDto(child);
    }

    public async Task DeleteAsync(int id, int parentId, CancellationToken ct = default)
    {
        var child = await LoadAuthorisedAsync(id, parentId, UserRole.Parent, ct);

        if (await db.Enrolments.AnyAsync(e => e.ChildId == id, ct))
            throw new ConflictException("This child has enrolments, so they cannot be deleted.");

        db.Children.Remove(child);
        await db.SaveChangesAsync(ct);
        photos.Delete(child.PhotoPath); // after the DB delete succeeds, so we never orphan a DB row
    }

    public async Task<ChildDto> SetPhotoAsync(int id, int parentId, IFormFile file, CancellationToken ct = default)
    {
        var child = await LoadAuthorisedAsync(id, parentId, UserRole.Parent, ct);

        var error = PhotoValidator.Validate(file);
        if (error is not null) throw new BadRequestException(error);

        var oldPath = child.PhotoPath;
        child.PhotoPath = await photos.SaveAsync(file, "children", ct);
        await db.SaveChangesAsync(ct);
        photos.Delete(oldPath); // remove the replaced photo only once the new one is saved

        return ToDto(child);
    }

    public async Task<string> GetPhotoFullPathAsync(int id, int userId, UserRole role, CancellationToken ct = default)
    {
        var child = await LoadAuthorisedAsync(id, userId, role, ct);
        var full = photos.GetFullPath(child.PhotoPath);
        if (full is null || !File.Exists(full))
            throw new NotFoundException("This child has no photo.");
        return full;
    }

    /// <summary>
    /// Ownership check: the role alone is not enough. A parent may only touch
    /// their own children; admins may read any child.
    /// </summary>
    private async Task<Child> LoadAuthorisedAsync(int id, int userId, UserRole role, CancellationToken ct)
    {
        var child = await db.Children.FirstOrDefaultAsync(c => c.Id == id, ct)
                    ?? throw new NotFoundException($"Child {id} was not found.");

        if (role != UserRole.Admin && child.ParentId != userId)
            throw new ForbiddenException("You can only access your own children.");

        return child;
    }

    private static void Apply(Child child, SaveChildRequest r)
    {
        child.FullName = r.FullName.Trim();
        child.DateOfBirth = r.DateOfBirth!.Value;
        // Store "no username" as null, not an empty string.
        child.LichessUsername = string.IsNullOrWhiteSpace(r.LichessUsername) ? null : r.LichessUsername.Trim();
    }

    private static ChildDto ToDto(Child c) => new(
        c.Id, c.FullName, c.DateOfBirth, c.AgeOn(DateOnly.FromDateTime(DateTime.UtcNow)),
        c.LichessUsername, c.PhotoPath is not null);
}
