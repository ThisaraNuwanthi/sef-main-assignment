# Requirements checklist

Where each requirement is satisfied. Paths are relative to the repository root
(`backend/src/SkcaEnrol.Api` is shortened to `api/`).

## Stack and architecture

| Requirement | Where |
|---|---|
| ASP.NET Core Web API (C#, .NET 8) | `backend/src/SkcaEnrol.Api` |
| EF Core + PostgreSQL | `api/Data/AppDbContext.cs`, Npgsql provider, Neon in production |
| React client (calls only the API) | `web/` — `web/src/api/client.ts` is the only network code |
| Flutter client (calls only the API) | `mobile/` — `mobile/lib/api/api_client.dart` |
| Agentic AI workflow | `api/Agents/**`, ADR 0003 |
| Layered: Controllers → DTOs → Services → DbContext, DI | `api/Controllers`, `api/Dtos`, `api/Services`, registrations in `api/Program.cs` |
| Async everywhere | all controller/service methods are `async Task` with `CancellationToken` |
| ≥ 3 roles | Admin, Coach, Parent — `api/Domain/Enums.cs`, `api/Auth/AppClaims.cs` |

## Database

| Requirement | Where |
|---|---|
| Entities with audit fields | `api/Domain/*.cs` (`BaseEntity`), stamped in `AppDbContext.SaveChangesAsync` |
| Constraints (check, unique, FK rules) and indexes | `AppDbContext.OnModelCreating`; summary in `docs/er-diagram.md` |
| jsonb for plan/tool payloads | `AgentWorkflows.PlanJson/FinalOutcome`, `AgentSteps`, `ToolCalls` |
| Migrations | `api/Data/Migrations` (`InitialCreate`, `AgentWorkflowState`), applied on startup |
| Seed data (1 admin, 2 coaches, 3 parents, children with real Lichess users, 7 classes incl. a nearly-full one) | `api/Data/DbSeeder.cs` |
| ER diagram | `docs/er-diagram.md` |

## API

| Requirement | Where |
|---|---|
| Register (Parent) / Login → JWT | `AuthController`, `AuthService`, `TokenService`; BCrypt hashing |
| Classes CRUD (Admin) + search/filter/sort/pagination | `ClassesController`, `ClassService.ListAsync` |
| Children CRUD (own) + photo upload (type/size/magic-byte checks) | `ChildrenController`, `ChildService`, `PhotoStorage.cs` |
| Enrolments: create (starts workflow), list (Admin all / Parent own, search/status/sort/page), get, update, cancel, history | `EnrolmentsController`, `EnrolmentService` |
| Business operation beyond CRUD | agent placement workflow + transactional approval (`WorkflowService.ApproveAsync`) |
| Workflow endpoints: get, approve, reject, revise, retry | `WorkflowsController`, `WorkflowService` |
| Report endpoint | `GET /api/reports/enrolment-summary` — `ReportService` |
| Coach roster | `GET /api/classes/mine` |
| `/health` | `Program.cs` (`AddHealthChecks().AddDbContextCheck`) |
| Status codes 201/204/400/401/403/404/409 | controllers + `Common/AppExceptions.cs` + `GlobalExceptionHandler` |
| Validation | DataAnnotations + `IValidatableObject` in `api/Dtos` |
| ProblemDetails global error handling | `api/Common/GlobalExceptionHandler.cs` |
| Serilog structured logging | `Program.cs` (`UseSerilog`, `UseSerilogRequestLogging`) |
| CORS for the React origin | `Cors:AllowedOrigins` in `Program.cs` |
| Swagger with JWT Authorize button | `Program.cs` (`AddSwaggerGen` + Bearer scheme), live at `/swagger` |
| Secure config (env vars / user-secrets) | `appsettings.json` has no secrets; `.env.example`, `appsettings.Development.example.json` |

## Business rules (server-side)

| Rule | Where |
|---|---|
| Capacity never exceeded (re-checked in the approval transaction with a row lock) | `WorkflowService.ApproveAsync` (`FOR UPDATE`), `ValidationRules.Capacity`, DB check `Capacity > 0` |
| No overlapping classes for a child | `ClassSearchTool` (filters), `ValidationRules.TimeClash`, re-check in `ApproveAsync` |
| Level fit, ±1 only with a recorded reason | `ValidationRules.LevelFit`, `PlacementAgent` |
| Fee rule with 10 % sibling discount, in one service | `api/Services/FeeService.cs` |
| Ownership checks (not just roles) | `ChildService.LoadAuthorisedAsync`, `EnrolmentService.LoadAuthorisedAsync` |
| Legal status transitions + history | `api/Services/EnrolmentStateMachine.cs` |

## Agentic AI

| Requirement | Where |
|---|---|
| Objective built from the request | `ObjectiveBuilder` in `EnrolmentService.cs` |
| 4 distinct agents with typed contracts | `PlannerAgent`, `SkillAssessmentAgent`, `PlacementAgent`, `ValidationSafetyAgent`; `AgentContracts.cs` |
| Plan from a fixed step set; unknown steps rejected | `PlanValidator` |
| Lichess tool (typed HttpClient, 10 s timeout, limited retry, 404/429/invalid JSON) | `api/Integrations/LichessClient.cs`, `LichessProfileTool.cs` |
| Class search tool + deterministic fee tool | `ClassSearchTool.cs`, `FeeCalculatorTool.cs` |
| LLM may only choose a returned class id | `PlacementAgent.ProposeAsync` |
| Validation agent: schemas, capacity, clash, level, fee match, prompt injection | `ValidationSafetyAgent.cs` (`ValidationRules`, `PromptInjectionDetector`) |
| Notes passed as delimited DATA | `AgentPrompt.WithData` |
| Tool allow-list per agent, timeouts, max 2 retries, safe failure | `ToolGateway.cs`, `WorkflowOrchestrator.RunStepAsync` |
| Workflow state tables | `api/Domain/AgentWorkflow.cs`, ADR 0004 |
| Human approval; single transaction | `WorkflowService.ApproveAsync` |
| Gemini client (JSON mode, env key) + fake client by config | `api/Agents/Llm/GeminiLlmClient.cs`, `FakeLlmClient.cs`, `Llm:Provider` |
| Background execution (Channel + BackgroundService) | `WorkflowQueue.cs`, `WorkflowWorker.cs` |
| No chain-of-thought/secrets stored | ADR 0004; only inputs/outputs/short rationales |

## Clients

| Requirement | Where |
|---|---|
| React: Vite + TS, router, protected role-based routes | `web/src/App.tsx`, `web/src/auth/ProtectedRoute.tsx` |
| React: state approach justified | Context + TanStack Query — ADR 0001 |
| React pages: login, dashboard + chart, classes list/form, enrolments, workflow review, coach roster | `web/src/pages/*` |
| React: loading/empty/success/error states, responsive, accessible | `web/src/components/States.tsx`, `index.css`, labels/aria throughout |
| Flutter: Provider state (justified) | `mobile/lib/state/*`, ADR 0002 |
| Flutter: secure token storage, protected screens | `token_storage.dart` (flutter_secure_storage), `router.dart` redirect |
| Flutter screens: register, login, logout, children + photo, enrolment form (pickers, validation), list with filter/search, detail with timeline/class/fee | `mobile/lib/screens/*` |
| `--dart-define=API_BASE_URL` | `mobile/lib/config.dart` |
| Release APK | `flutter build apk --release …` (verified) |

## Tests, performance, CI/CD, deployment

| Requirement | Where |
|---|---|
| Backend unit tests (fee, validation rules, safety, state machine…) | `backend/tests/SkcaEnrol.Tests/Unit` |
| Golden agent cases 1–6 (+ timetable clash) | `backend/tests/.../Integration/AgentWorkflowGoldenTests.cs` |
| Integration tests on real PostgreSQL (auth, constraint, approval transaction) | `Integration/*` with `ApiFactory` (Testcontainers or `TEST_DB_CONNECTION`) — **70 tests** |
| React tests (login validation, protected route, approval error) | `web/src/**/*.test.tsx` — **11 tests** |
| Flutter tests (form validation widget, navigation, API client with mock HTTP) | `mobile/test/*` — **12 tests** |
| Performance test | `perf/load-test.js`, results in `perf/README.md` |
| CI (backend with postgres service, web, Flutter) | `.github/workflows/ci.yml` |
| Dockerfile, deploy config | `backend/Dockerfile`, `backend/railway.json`, `web/vercel.json`, ADR 0005 |
| Documentation | `README.md`, `docs/`, `VIVA_QUESTIONS.md` |
| AI usage declaration | `docs/ai-usage-log.md` (to be completed by the student) |
