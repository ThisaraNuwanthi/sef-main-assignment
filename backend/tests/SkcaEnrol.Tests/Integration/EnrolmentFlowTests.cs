using System.Net;
using System.Net.Http.Json;
using SkcaEnrol.Api.Common;
using SkcaEnrol.Api.Domain;
using SkcaEnrol.Api.Dtos;
using SkcaEnrol.Api.Services;
using SkcaEnrol.Tests.Infrastructure;

namespace SkcaEnrol.Tests.Integration;

/// <summary>The enrolment component through its HTTP API: ownership, revision, approval with fees, reports.</summary>
public class EnrolmentFlowTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _api;

    public EnrolmentFlowTests(ApiFactory api)
    {
        _api = api;
        _api.Llm.Overrides.Clear();
        _api.Lichess.Fail = false;
        _api.Lichess.Rating = 1100;
    }

    [Fact]
    public async Task Approving_a_second_sibling_creates_a_discounted_fee_record()
    {
        await CreateClassAsync("Wednesday Wonders", DayOfWeek.Wednesday, fee: 3000m);

        // A brand-new family with two children, all through the public API.
        var reg = await _api.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { fullName = "Sibling Parent", email = "siblings@test.lk", password = ApiFactory.Password });
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);
        var parent = await _api.ClientForAsync("siblings@test.lk");

        var older = await AddChildAsync(parent, "Older Sibling");
        var younger = await AddChildAsync(parent, "Younger Sibling");
        var e1 = await SubmitAsync(parent, older, DayOfWeek.Wednesday);
        var e2 = await SubmitAsync(parent, younger, DayOfWeek.Wednesday);
        await _api.RunWorkflowAsync(e1.WorkflowId);
        await _api.RunWorkflowAsync(e2.WorkflowId);

        var admin = await _api.ClientForAsync("admin@test.lk");
        (await admin.PostAsJsonAsync($"/api/workflows/{e1.WorkflowId}/approve", new { })).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync($"/api/workflows/{e2.WorkflowId}/approve", new { })).EnsureSuccessStatusCode();

        var first = await parent.GetFromJsonAsync<EnrolmentDetailDto>($"/api/enrolments/{e1.EnrolmentId}", TestJson.Options);
        var second = await parent.GetFromJsonAsync<EnrolmentDetailDto>($"/api/enrolments/{e2.EnrolmentId}", TestJson.Options);

        Assert.Equal(EnrolmentStatus.Approved, second!.Status);
        Assert.Equal("Wednesday Wonders", second.AssignedClass!.Name);
        Assert.Equal(3000m, Assert.Single(first!.Fees).Amount);                       // first child: full fee
        var fee = Assert.Single(second.Fees);
        Assert.Equal(2700m, fee.Amount);                                               // second child: 10% off
        Assert.True(fee.SiblingDiscountApplied);

        // The history timeline shows every step, including who approved.
        var history = await parent.GetFromJsonAsync<List<StatusHistoryDto>>($"/api/enrolments/{e2.EnrolmentId}/history", TestJson.Options);
        Assert.Equal(
            new[] { EnrolmentStatus.Submitted, EnrolmentStatus.AgentProcessing, EnrolmentStatus.PendingAdminApproval, EnrolmentStatus.Approved },
            history!.Select(h => h.ToStatus));
        Assert.Equal("Test Admin", history.Last().ChangedBy);
    }

    [Fact]
    public async Task Revision_lets_the_parent_edit_and_resubmit_with_a_new_workflow()
    {
        await CreateClassAsync("Revision Saturday", DayOfWeek.Saturday);
        var parent = await _api.ClientForAsync("parent.a@test.lk");
        var childId = await AddChildAsync(parent, "Revise Kid");
        var created = await SubmitAsync(parent, childId, DayOfWeek.Saturday);
        await _api.RunWorkflowAsync(created.WorkflowId);

        var admin = await _api.ClientForAsync("admin@test.lk");
        // A reason is required so the parent knows what to change.
        var noNote = await admin.PostAsJsonAsync($"/api/workflows/{created.WorkflowId}/revise", new { });
        Assert.Equal(HttpStatusCode.BadRequest, noNote.StatusCode);
        (await admin.PostAsJsonAsync($"/api/workflows/{created.WorkflowId}/revise", new { note = "Please add Sunday as an option." }))
            .EnsureSuccessStatusCode();

        var detail = await parent.GetFromJsonAsync<EnrolmentDetailDto>($"/api/enrolments/{created.EnrolmentId}", TestJson.Options);
        Assert.Equal(EnrolmentStatus.RevisionRequested, detail!.Status);
        Assert.Equal("Please add Sunday as an option.", detail.LatestDecisionNote);
        Assert.True(detail.CanEdit);

        var update = await parent.PutAsJsonAsync($"/api/enrolments/{created.EnrolmentId}", new
        {
            preferredDays = new[] { "Saturday", "Sunday" }, preferredTimeFrom = "14:00:00", preferredTimeTo = "18:00:00"
        });
        update.EnsureSuccessStatusCode();
        var resubmitted = await update.Content.ReadFromJsonAsync<EnrolmentDetailDto>(TestJson.Options);
        Assert.Equal(EnrolmentStatus.Submitted, resubmitted!.Status);
        Assert.NotEqual(created.WorkflowId, resubmitted.LatestWorkflowId);

        // Once processing has finished, the request can no longer be edited.
        await _api.RunWorkflowAsync(resubmitted.LatestWorkflowId!.Value);
        var late = await parent.PutAsJsonAsync($"/api/enrolments/{created.EnrolmentId}", new { preferredDays = new[] { "Monday" } });
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
    }

    [Fact]
    public async Task Parents_only_see_their_own_enrolments()
    {
        var parentA = await _api.ClientForAsync("parent.a@test.lk");
        var childId = await AddChildAsync(parentA, "Private Kid");
        var created = await SubmitAsync(parentA, childId, DayOfWeek.Monday);

        var parentB = await _api.ClientForAsync("parent.b@test.lk");
        Assert.Equal(HttpStatusCode.Forbidden, (await parentB.GetAsync($"/api/enrolments/{created.EnrolmentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await parentB.GetAsync($"/api/enrolments/{created.EnrolmentId}/history")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await parentB.PostAsJsonAsync($"/api/enrolments/{created.EnrolmentId}/cancel", new { })).StatusCode);

        var list = await parentB.GetFromJsonAsync<PagedResult<EnrolmentListItemDto>>("/api/enrolments?pageSize=50", TestJson.Options);
        Assert.DoesNotContain(list!.Items, e => e.Id == created.EnrolmentId);

        // Parent B also cannot enrol parent A's child.
        var steal = await parentB.PostAsJsonAsync("/api/enrolments", new { childId, preferredDays = new[] { "Monday" } });
        Assert.Equal(HttpStatusCode.Forbidden, steal.StatusCode);
    }

    [Fact]
    public async Task A_child_cannot_have_two_open_requests_and_can_cancel()
    {
        var parent = await _api.ClientForAsync("parent.a@test.lk");
        var childId = await AddChildAsync(parent, "Double Kid");
        var created = await SubmitAsync(parent, childId, DayOfWeek.Monday);

        var duplicate = await parent.PostAsJsonAsync("/api/enrolments", new { childId, preferredDays = new[] { "Tuesday" } });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var cancel = await parent.PostAsJsonAsync($"/api/enrolments/{created.EnrolmentId}/cancel", new { reason = "Changed plans" });
        cancel.EnsureSuccessStatusCode();
        var detail = await cancel.Content.ReadFromJsonAsync<EnrolmentDetailDto>(TestJson.Options);
        Assert.Equal(EnrolmentStatus.Cancelled, detail!.Status);

        // The queued workflow notices and does nothing.
        await _api.RunWorkflowAsync(created.WorkflowId);
        Assert.Equal(EnrolmentStatus.Cancelled,
            (await parent.GetFromJsonAsync<EnrolmentDetailDto>($"/api/enrolments/{created.EnrolmentId}", TestJson.Options))!.Status);
    }

    [Fact]
    public async Task Enrolment_request_is_validated()
    {
        var parent = await _api.ClientForAsync("parent.a@test.lk");
        var childId = await AddChildAsync(parent, "Validation Kid");

        var noDays = await parent.PostAsJsonAsync("/api/enrolments", new { childId, preferredDays = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.BadRequest, noDays.StatusCode);

        var badTimes = await parent.PostAsJsonAsync("/api/enrolments",
            new { childId, preferredDays = new[] { "Monday" }, preferredTimeFrom = "18:00:00", preferredTimeTo = "14:00:00" });
        Assert.Equal(HttpStatusCode.BadRequest, badTimes.StatusCode);

        var longNotes = await parent.PostAsJsonAsync("/api/enrolments",
            new { childId, preferredDays = new[] { "Monday" }, parentNotes = new string('x', 501) });
        Assert.Equal(HttpStatusCode.BadRequest, longNotes.StatusCode);
    }

    [Fact]
    public async Task Summary_report_is_admin_only_and_lists_every_status()
    {
        var parent = await _api.ClientForAsync("parent.a@test.lk");
        Assert.Equal(HttpStatusCode.Forbidden, (await parent.GetAsync("/api/reports/enrolment-summary")).StatusCode);

        var admin = await _api.ClientForAsync("admin@test.lk");
        var summary = await admin.GetFromJsonAsync<EnrolmentSummaryDto>("/api/reports/enrolment-summary", TestJson.Options);
        Assert.Equal(Enum.GetValues<EnrolmentStatus>().Length, summary!.ByStatus.Count);
        Assert.Equal(6, summary.MonthlyFees.Count);
    }

    // ---------- helpers ----------

    private async Task CreateClassAsync(string name, DayOfWeek day, decimal fee = 3500m)
    {
        await _api.WithDbAsync(async db =>
        {
            db.Classes.Add(new ChessClass
            {
                Name = name, Level = ClassLevel.Beginner, DayOfWeek = day, StartTime = new TimeOnly(15, 0), EndTime = new TimeOnly(16, 0),
                Capacity = 8, MonthlyFee = fee, CoachId = _api.CoachId
            });
            await db.SaveChangesAsync();
        });
    }

    private static async Task<int> AddChildAsync(HttpClient parent, string name)
    {
        var response = await parent.PostAsJsonAsync("/api/children",
            new { fullName = name, dateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-9).ToString("yyyy-MM-dd"), lichessUsername = "some_kid" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ChildDto>(TestJson.Options))!.Id;
    }

    private static async Task<EnrolmentCreatedDto> SubmitAsync(HttpClient parent, int childId, DayOfWeek day)
    {
        var response = await parent.PostAsJsonAsync("/api/enrolments", new
        {
            childId, preferredDays = new[] { day.ToString() }, preferredTimeFrom = "14:00:00", preferredTimeTo = "18:00:00"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<EnrolmentCreatedDto>(TestJson.Options))!;
    }
}
