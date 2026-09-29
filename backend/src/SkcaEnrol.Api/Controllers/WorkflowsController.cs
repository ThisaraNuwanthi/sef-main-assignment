using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkcaEnrol.Api.Auth;
using SkcaEnrol.Api.Dtos;
using SkcaEnrol.Api.Services;

namespace SkcaEnrol.Api.Controllers;

/// <summary>Admin review of agent workflows and the human approval step.</summary>
[ApiController]
[Route("api/workflows")]
[Authorize(Roles = Roles.Admin)] // only admins see agent internals or make decisions
public class WorkflowsController(IWorkflowService workflows) : ControllerBase
{
    /// <summary>Status, plan, every step, tool calls, validation results, timings and errors.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkflowDto>> Get(int id, CancellationToken ct) =>
        Ok(await workflows.GetAsync(id, ct));

    /// <summary>Books the place in one transaction. 409 if the class filled up meanwhile.</summary>
    [HttpPost("{id:int}/approve")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WorkflowDto>> Approve(int id, DecisionRequest request, CancellationToken ct) =>
        Ok(await workflows.ApproveAsync(id, User.GetUserId(), request.Note, ct));

    [HttpPost("{id:int}/reject")]
    public async Task<ActionResult<WorkflowDto>> Reject(int id, DecisionRequest request, CancellationToken ct) =>
        Ok(await workflows.RejectAsync(id, User.GetUserId(), request.Note, ct));

    /// <summary>Sends the request back to the parent to edit and resubmit.</summary>
    [HttpPost("{id:int}/revise")]
    public async Task<ActionResult<WorkflowDto>> Revise(int id, DecisionRequest request, CancellationToken ct) =>
        Ok(await workflows.RequestRevisionAsync(id, User.GetUserId(), request.Note, ct));

    /// <summary>Starts a fresh run for a failed workflow.</summary>
    [HttpPost("{id:int}/retry")]
    [ProducesResponseType<RetryResultDto>(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<RetryResultDto>> Retry(int id, CancellationToken ct) =>
        Accepted(await workflows.RetryAsync(id, User.GetUserId(), ct));
}
