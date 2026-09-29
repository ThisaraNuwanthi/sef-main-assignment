using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkcaEnrol.Api.Auth;
using SkcaEnrol.Api.Dtos;
using SkcaEnrol.Api.Services;

namespace SkcaEnrol.Api.Controllers;

[ApiController]
[Route("api/children")]
// Roles are set per action: several [Authorize] attributes are ANDed together,
// so a Parent-only rule here would also lock Admins out of the read endpoints.
[Authorize]
public class ChildrenController(IChildService children) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = Roles.Parent)]
    public async Task<ActionResult<List<ChildDto>>> ListMine(CancellationToken ct) =>
        Ok(await children.ListMineAsync(User.GetUserId(), ct));

    [HttpGet("{id:int}")]
    [Authorize(Roles = $"{Roles.Parent},{Roles.Admin}")]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChildDto>> Get(int id, CancellationToken ct) =>
        Ok(await children.GetAsync(id, User.GetUserId(), User.GetRole(), ct));

    [HttpPost]
    [Authorize(Roles = Roles.Parent)]
    [ProducesResponseType<ChildDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ChildDto>> Create(SaveChildRequest request, CancellationToken ct)
    {
        var created = await children.CreateAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Parent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ChildDto>> Update(int id, SaveChildRequest request, CancellationToken ct) =>
        Ok(await children.UpdateAsync(id, User.GetUserId(), request, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Parent)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await children.DeleteAsync(id, User.GetUserId(), ct);
        return NoContent();
    }

    /// <summary>Upload a JPEG or PNG photo (max 2 MB) as multipart/form-data field "photo".</summary>
    [HttpPost("{id:int}/photo")]
    [Authorize(Roles = Roles.Parent)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(3 * 1024 * 1024)] // reject huge bodies before they are buffered
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ChildDto>> UploadPhoto(int id, IFormFile photo, CancellationToken ct) =>
        Ok(await children.SetPhotoAsync(id, User.GetUserId(), photo, ct));

    /// <summary>Photos are served through here (not as public static files) so ownership is checked.</summary>
    [HttpGet("{id:int}/photo")]
    [Authorize(Roles = $"{Roles.Parent},{Roles.Admin}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPhoto(int id, CancellationToken ct)
    {
        var path = await children.GetPhotoFullPathAsync(id, User.GetUserId(), User.GetRole(), ct);
        var contentType = path.EndsWith(".png") ? "image/png" : "image/jpeg";
        return PhysicalFile(path, contentType);
    }
}
