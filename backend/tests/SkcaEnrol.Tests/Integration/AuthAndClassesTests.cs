using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SkcaEnrol.Api.Common;
using SkcaEnrol.Api.Domain;
using SkcaEnrol.Api.Dtos;
using SkcaEnrol.Tests.Infrastructure;

namespace SkcaEnrol.Tests.Integration;

public class AuthAndClassesTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private object NewClass(string name, int capacity = 8) => new
    {
        name,
        level = "Beginner",
        dayOfWeek = "Saturday",
        startTime = "09:00:00",
        endTime = "10:00:00",
        capacity,
        monthlyFee = 3500,
        coachId = api.CoachId,
        isActive = true
    };

    [Fact]
    public async Task Register_creates_a_parent_and_returns_a_token()
    {
        var client = api.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "New Parent", email = "new.parent@test.lk", password = "Secret123" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options);
        Assert.False(string.IsNullOrEmpty(body!.Token));
        Assert.Equal("Parent", body.User.Role); // self-registration can never create an Admin
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        var response = await api.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = "admin@test.lk", password = "wrong-password1" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_users_cannot_list_classes()
    {
        var response = await api.CreateClient().GetAsync("/api/classes");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Parent_cannot_create_a_class()
    {
        var parent = await api.ClientForAsync("parent.a@test.lk");

        var response = await parent.PostAsJsonAsync("/api/classes", NewClass("Parent Made Class"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_creates_a_class_and_it_appears_in_the_filtered_list()
    {
        var admin = await api.ClientForAsync("admin@test.lk");

        var create = await admin.PostAsJsonAsync("/api/classes", NewClass("Saturday Starters"));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var list = await admin.GetFromJsonAsync<PagedResult<ClassDto>>(
            "/api/classes?search=starters&level=Beginner&day=Saturday", TestJson.Options);
        var found = Assert.Single(list!.Items);
        Assert.Equal(8, found.SeatsLeft);
    }

    [Fact]
    public async Task Invalid_class_is_rejected_with_400_problem_details()
    {
        var admin = await api.ClientForAsync("admin@test.lk");

        var response = await admin.PostAsJsonAsync("/api/classes", NewClass("Zero Seats", capacity: 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Database_check_constraint_blocks_zero_capacity_even_if_api_validation_is_bypassed()
    {
        await api.WithDbAsync(async db =>
        {
            db.Classes.Add(new ChessClass
            {
                Name = "Bypass", Level = ClassLevel.Beginner, DayOfWeek = DayOfWeek.Monday,
                StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(10, 0),
                Capacity = 0, MonthlyFee = 100, CoachId = api.CoachId
            });

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            var pg = Assert.IsType<PostgresException>(ex.InnerException);
            Assert.Equal("CK_Classes_Capacity_Positive", pg.ConstraintName);
        });
    }

    [Fact]
    public async Task Parent_cannot_read_another_parents_child()
    {
        var parentA = await api.ClientForAsync("parent.a@test.lk");
        var created = await parentA.PostAsJsonAsync("/api/children",
            new { fullName = "Kid of A", dateOfBirth = "2016-01-01" });
        var child = await created.Content.ReadFromJsonAsync<ChildDto>(TestJson.Options);

        var parentB = await api.ClientForAsync("parent.b@test.lk");
        var response = await parentB.GetAsync($"/api/children/{child!.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        var response = await api.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
