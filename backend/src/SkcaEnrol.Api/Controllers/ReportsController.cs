using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkcaEnrol.Api.Auth;
using SkcaEnrol.Api.Services;

namespace SkcaEnrol.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = Roles.Admin)]
public class ReportsController(IReportService reports) : ControllerBase
{
    /// <summary>Counts by status, class fill rates, monthly fee totals and agent statistics.</summary>
    [HttpGet("enrolment-summary")]
    public async Task<ActionResult<EnrolmentSummaryDto>> EnrolmentSummary(CancellationToken ct) =>
        Ok(await reports.EnrolmentSummaryAsync(ct));
}
