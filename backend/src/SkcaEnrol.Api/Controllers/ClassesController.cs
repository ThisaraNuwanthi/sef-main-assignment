using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkcaEnrol.Api.Auth;
using SkcaEnrol.Api.Common;
using SkcaEnrol.Api.Dtos;
using SkcaEnrol.Api.Services;

namespace SkcaEnrol.Api.Controllers;

[ApiController]
[Route("api/classes")]
[Authorize] // any logged-in role can browse; changes are Admin only (below)
public class ClassesController(IClassService classes) : ControllerBase
{
    private bool IsAdmin => User.IsInRole(Roles.Admin);

    /// <summary>Search, filter (level, day), sort and paginate classes.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ClassDto>>> List([FromQuery] ClassQuery query, CancellationToken ct) =>
        Ok(await classes.ListAsync(query, IsAdmin, ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClassDto>> Get(int id, CancellationToken ct) =>
        Ok(await classes.GetAsync(id, IsAdmin, ct));

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<ClassDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClassDto>> Create(SaveClassRequest request, CancellationToken ct)
    {
        var created = await classes.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClassDto>> Update(int id, SaveClassRequest request, CancellationToken ct) =>
        Ok(await classes.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await classes.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Coach list for the Admin class form's dropdown.</summary>
    [HttpGet("coaches")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<List<CoachOptionDto>>> Coaches(CancellationToken ct) =>
        Ok(await classes.ListCoachesAsync(ct));
}
