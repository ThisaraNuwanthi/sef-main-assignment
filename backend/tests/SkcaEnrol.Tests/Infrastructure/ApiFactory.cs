using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using SkcaEnrol.Api.Agents.Llm;
using SkcaEnrol.Api.Agents.Orchestration;
using SkcaEnrol.Api.Data;
using SkcaEnrol.Api.Integrations;
using SkcaEnrol.Api.Domain;
using SkcaEnrol.Api.Dtos;
using Testcontainers.PostgreSql;

namespace SkcaEnrol.Tests.Infrastructure;

/// <summary>
/// Boots the real API in memory against a real, throw-away PostgreSQL database.
///
/// Where PostgreSQL comes from:
///  - If TEST_DB_CONNECTION is set (CI's postgres service, or a local Postgres), use that server.
///  - Otherwise start a disposable postgres container with Testcontainers (needs Docker).
/// Either way each fixture gets its own freshly-migrated database, dropped afterwards.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "Test@12345";

    private PostgreSqlContainer? _container;
    private string _connectionString = "";

    public int AdminId { get; private set; }
    public int CoachId { get; private set; }
    public int ParentAId { get; private set; }
    public int ParentBId { get; private set; }

    public async Task InitializeAsync()
    {
        var server = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(server))
        {
            _container = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
            await _container.StartAsync();
            server = _container.GetConnectionString();
        }

        // Unique database name so parallel test classes never share data.
        _connectionString = new NpgsqlConnectionStringBuilder(server) { Database = $"skca_test_{Guid.NewGuid():N}" }.ConnectionString;

        // Touching Services starts the app, which runs the EF migrations (MigrateOnStartup).
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedUsersAsync(db);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" environment: user-secrets (Development only) are not loaded.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
        builder.UseSetting("Jwt:Key", "integration-test-signing-key-at-least-32-chars");
        builder.UseSetting("Seed:Enabled", "false");
        builder.UseSetting("Storage:UploadsPath", Path.Combine(Path.GetTempPath(), "skca-test-uploads"));

        // Agents: offline fake LLM, and no background worker. Tests run a workflow
        // themselves with RunWorkflowAsync, so results are deterministic.
        builder.UseSetting("Llm:Provider", "Fake");
        builder.UseSetting("Agents:RunInBackground", "false");
        builder.UseSetting("Agents:StepTimeoutSeconds", "10");
        builder.UseSetting("Agents:RetryDelaySeconds", "0");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ILichessClient>();
            services.AddSingleton(Lichess);
            services.AddSingleton<ILichessClient>(Lichess);
        });
    }

    /// <summary>Fake Lichess shared by the app and the test (tests flip Fail / Rating).</summary>
    public FakeLichessClient Lichess { get; } = new();

    /// <summary>The app's fake LLM, so tests can force answers through Overrides.</summary>
    public FakeLlmClient Llm => Services.GetRequiredService<FakeLlmClient>();

    /// <summary>Runs the agent workflow now, in a fresh scope, like the background worker would.</summary>
    public async Task RunWorkflowAsync(int workflowId)
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<WorkflowOrchestrator>().RunAsync(workflowId);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        using (var scope = Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();

        await base.DisposeAsync();
        if (_container is not null) await _container.DisposeAsync();
    }

    private async Task SeedUsersAsync(AppDbContext db)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(Password);
        var admin = new User { FullName = "Test Admin", Email = "admin@test.lk", Role = UserRole.Admin, PasswordHash = hash };
        var coach = new User { FullName = "Test Coach", Email = "coach@test.lk", Role = UserRole.Coach, PasswordHash = hash };
        var parentA = new User { FullName = "Parent A", Email = "parent.a@test.lk", Role = UserRole.Parent, PasswordHash = hash };
        var parentB = new User { FullName = "Parent B", Email = "parent.b@test.lk", Role = UserRole.Parent, PasswordHash = hash };
        db.Users.AddRange(admin, coach, parentA, parentB);
        await db.SaveChangesAsync();
        (AdminId, CoachId, ParentAId, ParentBId) = (admin.Id, coach.Id, parentA.Id, parentB.Id);
    }

    /// <summary>An HttpClient already carrying the JWT for the given account.</summary>
    public async Task<HttpClient> ClientForAsync(string email)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    /// <summary>Runs code against the test database directly (arrange/assert steps).</summary>
    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
