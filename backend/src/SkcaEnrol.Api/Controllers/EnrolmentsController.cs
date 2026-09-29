using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkcaEnrol.Api.Auth;
using SkcaEnrol.Api.Common;
using SkcaEnrol.Api.Dtos;
using SkcaEnrol.Api.Services;

namespace SkcaEnrol.Api.Controllers;

/// <summary>The primary business component: enrolment requests and their tracking.</summary>
[ApiController]
[Route("api/enrolments")]
[Authorize(Roles = $"{Roles.Parent},{Roles.Admin}")]
public class EnrolmentsController(IEnrolmentService enrolments, IWorkflowService workflows) : ControllerBase
{
    /// <summary>
    /// Parent submits a request. It is saved, the agent workflow is queued, and the
    /// response comes back straight away (the agents run in the background).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Roles.Parent)]
    [ProducesResponseType<EnrolmentCreatedDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrolmentCreatedDto>> Create(CreateEnrolmentRequest request, CancellationToken ct)
    {
        var created = await enrolments.CreateAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.EnrolmentId }, created);
    }

    /// <summary>Admin: all requests. Parent: only their own. Search, status filter, sort and pagination.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<EnrolmentListItemDto>>> List([FromQuery] EnrolmentQuery query, CancellationToken ct) =>
        Ok(await enrolments.ListAsync(query, User.GetUserId(), User.GetRole(), ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EnrolmentDetailDto>> Get(int id, CancellationToken ct) =>
        Ok(await enrolments.GetAsync(id, User.GetUserId(), User.GetRole(), ct));

    /// <summary>Parent edits a request (only while Submitted or RevisionRequested; the latter resubmits it).</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Parent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrolmentDetailDto>> Update(int id, UpdateEnrolmentRequest request, CancellationToken ct) =>
        Ok(await enrolments.UpdateAsync(id, User.GetUserId(), request, ct));

    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrolmentDetailDto>> Cancel(int id, CancelEnrolmentRequest request, CancellationToken ct) =>
        Ok(await enrolments.CancelAsync(id, User.GetUserId(), User.GetRole(), request.Reason, ct));

    /// <summary>The status timeline (who changed what, when, and why).</summary>
    [HttpGet("{id:int}/history")]
    public async Task<ActionResult<List<StatusHistoryDto>>> History(int id, CancellationToken ct) =>
        Ok(await enrolments.HistoryAsync(id, User.GetUserId(), User.GetRole(), ct));

    /// <summary>Admin: every agent run for this request (including failed ones).</summary>
    [HttpGet("{id:int}/workflows")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<List<WorkflowListItemDto>>> Workflows(int id, CancellationToken ct) =>
        Ok(await workflows.ListForEnrolmentAsync(id, ct));
}
