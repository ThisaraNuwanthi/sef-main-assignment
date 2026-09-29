using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using SkcaEnrol.Api.Agents;
using SkcaEnrol.Api.Agents.Llm;
using SkcaEnrol.Api.Agents.Orchestration;
using SkcaEnrol.Api.Agents.Tools;
using SkcaEnrol.Api.Auth;
using SkcaEnrol.Api.Integrations;
using SkcaEnrol.Api.Common;
using SkcaEnrol.Api.Data;
using SkcaEnrol.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- Logging: Serilog writes structured JSON-friendly logs to the console ----------
builder.Host.UseSerilog((context, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// ---------- Configuration (secrets come from user-secrets or environment variables) ----------
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is not set. See README 'Environment variables'.");

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Section));
var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
// HMAC-SHA256 needs a key of at least 256 bits; fail fast instead of issuing weak tokens.
if (Encoding.UTF8.GetByteCount(jwt.Key) < 32)
    throw new InvalidOperationException("Jwt:Key must be at least 32 characters. Set it with user-secrets or the Jwt__Key env var.");

builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.Section));

// ---------- Database ----------
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// ---------- Authentication (JWT bearer) + role-based authorisation ----------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep claim names exactly as issued ("sub", "role") instead of .NET's long URIs.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = AppClaims.Name,
            RoleClaimType = AppClaims.Role // makes [Authorize(Roles = "Admin")] read our "role" claim
        };
    });
builder.Services.AddAuthorization();

// ---------- Application services (dependency injection) ----------
// Scoped = one instance per HTTP request, matching the DbContext's lifetime.
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IClassService, ClassService>();
builder.Services.AddScoped<IChildService, ChildService>();
builder.Services.AddSingleton<IPhotoStorage, LocalPhotoStorage>();
builder.Services.AddScoped<IFeeService, FeeService>();
builder.Services.AddScoped<IEnrolmentService, EnrolmentService>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddScoped<IReportService, ReportService>();

// ---------- Agentic AI subsystem ----------
builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.Section));
builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection(LlmOptions.Section));

// The 4 agents and the orchestrator (one per workflow run, via a DI scope).
builder.Services.AddScoped<PlannerAgent>();
builder.Services.AddScoped<SkillAssessmentAgent>();
builder.Services.AddScoped<PlacementAgent>();
builder.Services.AddScoped<ValidationSafetyAgent>();
builder.Services.AddScoped<WorkflowOrchestrator>();

// Tools. Agents cannot reach these directly: only through ToolGateway, which applies each agent's allow-list.
builder.Services.AddScoped<IAgentTool, LichessProfileTool>();
builder.Services.AddScoped<IAgentTool, ClassSearchTool>();
builder.Services.AddScoped<IAgentTool, FeeCalculatorTool>();

// Typed HttpClient for Lichess: 10s timeout; retries are handled inside LichessClient.
builder.Services.AddHttpClient<ILichessClient, LichessClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Lichess:BaseUrl"] ?? "https://lichess.org/");
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("SkcaEnrol/1.0 (student project)");
});

// LLM provider chosen by config: "Gemini" for real runs, "Fake" for tests and offline demos.
var llm = builder.Configuration.GetSection(LlmOptions.Section).Get<LlmOptions>() ?? new LlmOptions();
if (llm.Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<ILlmClient, GeminiLlmClient>(client =>
    {
        client.BaseAddress = new Uri(llm.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    });
}
else
{
    builder.Services.AddSingleton<FakeLlmClient>();
    builder.Services.AddSingleton<ILlmClient>(sp => sp.GetRequiredService<FakeLlmClient>());
}

// Background runner: POST /api/enrolments only queues the workflow id and returns.
builder.Services.AddSingleton<WorkflowQueue>();
if (builder.Configuration.GetValue("Agents:RunInBackground", true))
    builder.Services.AddHostedService<WorkflowWorker>();

// ---------- Errors: every failure becomes a ProblemDetails JSON body ----------
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// ---------- CORS: only our React app's origin(s) may call the API from a browser ----------
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

// ---------- Controllers + JSON (enums as text, e.g. "Beginner" not 0) ----------
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// ---------- Swagger with an "Authorize" button for the JWT ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "SKCA Enrol API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste the token from POST /api/auth/login (without the word Bearer)."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
    });
    var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xml)) options.IncludeXmlComments(xml);
});

// ---------- Health check: /health also proves the database is reachable ----------
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

var app = builder.Build();

// ---------- Database migrations + demo seed on startup (both switchable by config) ----------
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
        await db.Database.MigrateAsync();

    var demoPassword = app.Configuration["Seed:DemoPassword"];
    if (app.Configuration.GetValue("Seed:Enabled", false) && !string.IsNullOrEmpty(demoPassword))
        await DbSeeder.SeedAsync(db, demoPassword, app.Logger);
}

// ---------- HTTP pipeline (order matters) ----------
app.UseExceptionHandler();      // catches anything thrown further down
app.UseSerilogRequestLogging(); // one log line per request with status + duration

// Swagger stays on in production too: the assignment requires /swagger on the live API.
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Exposes Program to the test project's WebApplicationFactory.
public partial class Program;
