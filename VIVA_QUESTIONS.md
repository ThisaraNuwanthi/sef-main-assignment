# Viva Preparation — SKCA Enrol

Questions an examiner is likely to ask, grouped by build phase, with short
answers and the file to open when explaining. Practise answering **without**
reading the answer first, then open the file and trace the code yourself.

---

## Phase 1 — Scaffold and backend core

### 1. Why is capacity checked in the DTO *and* as a database check constraint?
The DTO rule (`[Range(1, 100)]` in `Dtos/ClassDtos.cs`) gives the user a friendly
400 message early. The check constraint (`CK_Classes_Capacity_Positive` in
`Data/AppDbContext.cs`) is the last line of defence: even if a future bug, a
seed script or someone using SQL directly bypasses the API, PostgreSQL refuses
the row. The test `Database_check_constraint_blocks_zero_capacity_even_if_api_validation_is_bypassed`
writes straight through the DbContext (skipping validation) and proves the DB rejects it.

### 2. A parent with a valid JWT requests `GET /api/children/7`, which belongs to someone else. What returns the 403?
1. `[Authorize(Roles = "Parent,Admin")]` on `ChildrenController.Get` passes — the role is fine.
2. `ChildService.GetAsync` calls `LoadAuthorisedAsync`, which loads child 7 and compares
   `child.ParentId` with the user id from the token (`User.GetUserId()` reads the `sub` claim).
3. They differ and the user is not Admin, so it throws `ForbiddenException`.
4. `GlobalExceptionHandler` maps that to a 403 ProblemDetails response.

Role checks answer "what kind of user are you?"; ownership checks answer "is this *your* record?". Both are needed.

### 3. Why is `ParentId` taken from the token and never from the request body?
The body is controlled by the client, so a malicious parent could send someone
else's id. The JWT is signed with our secret key, so its `sub` claim cannot be
changed without the signature failing. (`ChildService.CreateAsync`.)

### 4. Why does the photo validator read the first bytes of the file?
The `Content-Type` header is chosen by the client and can lie (e.g. a script
renamed to `.png`). Real JPEGs start with `FF D8 FF` and PNGs with `89 50 4E 47`
("magic bytes"). We also pick the saved file extension ourselves and generate
a GUID file name, so a user can never choose the path. (`Services/PhotoStorage.cs`.)

### 5. How does JWT authentication work here, step by step?
Login → `AuthService.LoginAsync` checks the password with `BCrypt.Verify` →
`TokenService.CreateToken` builds a token with `sub`, `email`, `name`, `role`
claims, signed with HMAC-SHA256 using `Jwt:Key`. The client sends
`Authorization: Bearer <token>`. `AddJwtBearer` in `Program.cs` validates the
signature, issuer, audience and expiry, then `RoleClaimType = "role"` makes
`[Authorize(Roles = ...)]` work.

### 6. Why BCrypt instead of SHA-256 for passwords?
BCrypt is deliberately slow and salts every hash automatically, so the same
password gives different hashes and brute-forcing a leaked table is expensive.
SHA-256 is fast, which helps attackers.

### 7. Why does login give the same error for "unknown email" and "wrong password"?
So attackers cannot use the login form to discover which emails have accounts
(user enumeration).

### 8. Where do secrets live?
Never in git. Locally: `dotnet user-secrets` (stored in your user profile, outside
the repo). In production: environment variables (`Jwt__Key`,
`ConnectionStrings__Default`) set on the hosting platform. `Program.cs` fails
fast at startup if the JWT key is missing or shorter than 32 characters.

### 9. How are errors returned?
Services throw typed exceptions (`Common/AppExceptions.cs`). One
`GlobalExceptionHandler` converts them to RFC 7807 ProblemDetails with the right
status (400/401/403/404/409). Unknown errors become a generic 500 and are logged
with the stack trace, which is never sent to the client. Postgres unique and
check violations are mapped to 409 and 400.

### 10. Why does `DayOfWeek` stay an integer in the DB while other enums are text?
Text enums are readable in the DB. But sorting "by day" on text would give
Friday, Monday, Saturday… (alphabetical). As an integer (0 = Sunday) the sort
follows the week.

### 11. What happens on startup?
`Program.cs` runs EF migrations (`Database:MigrateOnStartup`), then the seeder
if `Seed:Enabled` is true and the Users table is empty. Seeding is in code, not
`HasData`, because BCrypt hashes change on every run and would create a new
migration every time.

### 12. How do the integration tests get a real database?
`tests/.../Infrastructure/ApiFactory.cs`: if `TEST_DB_CONNECTION` is set (CI's
postgres service container) it uses that server, otherwise Testcontainers starts
a throw-away `postgres:16-alpine` container. Each test class gets its own
freshly-migrated database, deleted at the end. `WebApplicationFactory<Program>`
runs the real API in memory.

### 13. What does "Scoped" mean in `AddScoped<IClassService, ClassService>()`?
One instance per HTTP request. It matches `AppDbContext`'s lifetime: a
DbContext is not thread-safe and should not live longer than one request.

### 14. Why can't two `[Authorize]` attributes with different roles on class and action give "either"?
Multiple `[Authorize]` attributes are combined with AND. That's why
`ChildrenController` has plain `[Authorize]` on the class and the roles on each
action.

---

## Phase 2 — Enrolment component and Agentic AI

### 15. Walk through what happens after a parent taps "Submit" (POST /api/enrolments).
1. `EnrolmentsController.Create` → `EnrolmentService.CreateAsync` checks ownership (the child's
   ParentId must equal the token's user id), that the child has no other open request, and that the
   requested class is active.
2. It saves the Enrolment (Submitted), its first history row, and an `AgentWorkflow` (Queued) in one `SaveChanges`.
3. It puts the workflow id on `WorkflowQueue` (a `Channel<int>`) and returns **201** with `enrolmentId` + `workflowId` straight away.
4. `WorkflowWorker` (a `BackgroundService`) reads the id, creates a DI scope and calls `WorkflowOrchestrator.RunAsync`.

### 16. What are the 4 agents, and what makes them "distinct"?
Each has its own class, its own typed input/output records (`Agents/AgentContracts.cs`), its own
`AllowedTools` set, and its own `AgentStep` rows.
| Agent | Uses LLM? | Tools | Output |
|---|---|---|---|
| PlannerAgent | yes | none | plan (list of allowed steps) |
| SkillAssessmentAgent | yes (only when Lichess data exists) | LichessProfile | level, confidence, rationale |
| PlacementAgent | yes | ClassSearch, FeeCalculator | chosen class, reason, fee |
| ValidationSafetyAgent | **no**, deterministic | none (read-only DB) | rule results |

### 17. How is "least privilege" enforced for tools?
Agents never receive tool objects. They only get a `ToolGateway` (`Agents/Tools/ToolGateway.cs`)
created by the orchestrator with that agent's allow-list. `CallAsync` checks the list first; a
forbidden call is recorded as a failed `ToolCall` ("Denied…") and throws `AgentToolNotAllowedException`,
which is **not retried** and fails the workflow. Test: `ToolGatewayTests`.

### 18. The LLM answers `{"classId": 999999}`. What happens?
`PlacementAgent.ProposeAsync` looks the id up in the candidate list the ClassSearch tool returned.
It isn't there, so it throws `AgentOutputException`. The orchestrator retries the step (max 2 retries,
3 attempts total). Still wrong → step Failed, workflow Failed with the reason, enrolment Failed.
Nothing is booked. An admin can later `POST /api/workflows/{id}/retry`, which creates a **new** workflow.
Golden test: `Invalid_class_id_from_the_LLM_is_rejected_retried_and_fails_safely`.

### 19. How do you defend against prompt injection in parent notes?
Three layers:
1. **Separation**: notes are never put in the objective or instructions. `AgentPrompt.WithData` puts
   them inside a `<data>…</data>` block, JSON-encoded (`<` becomes `<`, so the note can't close
   the tag), with an instruction that data is not instructions.
2. **Constrained output**: even if the model were fooled, it can only return a class id from the
   candidate list, the fee comes from deterministic code, and nothing is approved automatically.
3. **Detection**: `PromptInjectionDetector` (regex) flags phrases like "ignore previous instructions" or
   "approve automatically" as a **Warning** that the admin sees on the review page.

### 20. Why is the ValidationSafetyAgent deterministic (no LLM)?
It is the safety net that checks the LLM agents' work. If it were an LLM it could be fooled or be
wrong in the same way. Its rules are pure functions (`ValidationRules`) that are unit-tested:
schema checks, candidate membership, class active, capacity, time clash, level fit (±1 only with a
reason), fee recomputation match, and prompt injection.

### 21. Explain the approval transaction. Why `FOR UPDATE`?
`WorkflowService.ApproveAsync` opens a transaction, then:
lock the class row (`SELECT … FOR UPDATE`) → re-check active, capacity and time clash → recompute the fee →
set `AssignedClassId` and status Approved (+ history) → add FeeRecord → add ApprovalDecision →
`SaveChanges` → `Commit`. Any `ConflictException` (e.g. full → **409**) leaves before `Commit`,
so the transaction is rolled back and nothing is saved.
`FOR UPDATE` makes a second approval for the same class **wait** until the first commits, so it
then sees the updated seat count. Without the lock, two admins could both see "1 seat left".
Golden test: `Capacity_exceeded_at_approval_returns_409_and_rolls_back`.

### 22. What is the `Version`/`xmin` property on Enrolment?
Optimistic concurrency. PostgreSQL changes a row's hidden `xmin` on every update. EF includes it in
the `WHERE` of each UPDATE; if someone else changed the row in between, 0 rows match and EF throws
`DbUpdateConcurrencyException`, which the global handler returns as 409.

### 23. Where is the fee rule, and who uses it?
Only in `Services/FeeService.cs` (`FeeCalculator.Calculate`): class fee, minus 10% if another child of
the same parent already has an approved place, rounded to 2 decimals. Used by the FeeCalculator tool
(PlacementAgent), the ValidationSafetyAgent (to recompute and compare) and the approval transaction.
The LLM never calculates money.

### 24. What happens if Lichess is down or rate-limits you?
`LichessClient` (typed HttpClient, 10s timeout) retries 5xx/timeouts up to 2 times; a 429 is not
retried (Lichess asks you to wait a minute); 404 returns "not found"; bad JSON becomes a
`ToolFailedException`. `SkillAssessmentAgent` catches the tool failure and falls back to the
age-based default with **Low** confidence. The failed call is still recorded. Golden test:
`Lichess_failure_falls_back_to_age_default_and_is_recorded`.

### 25. Why can't the plan skip the Validate step?
`PlanValidator.EnsureValid` requires the plan to be exactly the allowed steps in dependency order
(each step needs the previous one's output). Unknown steps, repeats or missing steps →
`AgentOutputException` → retried → fails safely. The LLM cannot remove the safety checks or the
human approval.

### 26. How do you test the agents without calling Gemini?
`ILlmClient` has two implementations chosen by `Llm:Provider`: `GeminiLlmClient` (JSON mode, key from
`GEMINI_API_KEY` sent in a header) and `FakeLlmClient` (fixed-shape answers, no network). Tests use
the fake plus `FakeLichessClient`, and can force bad answers through `FakeLlmClient.Overrides`.
The 6 golden cases run the real orchestrator against real PostgreSQL.

### 27. What is stored for each workflow, and what is deliberately NOT stored?
Stored: objective, plan, every step's input/output JSON (jsonb), status, duration, retries, errors,
every tool call with timing, validation results, and the admin decision.
Not stored: hidden chain-of-thought (we only ask for a short rationale), passwords, tokens or API keys.

### 28. What happens if the server restarts while a workflow is running?
`WorkflowWorker.RecoverAfterRestartAsync`: workflows still `Queued` are put back on the queue;
workflows that were `Running` are marked Failed ("Interrupted by a server restart") so they don't hang
forever, and an admin can retry them. The database, not the in-memory channel, is the source of truth.

### 29. Which status changes are allowed?
Only those in `EnrolmentStateMachine.Allowed`, e.g. Submitted → AgentProcessing → PendingAdminApproval →
Approved/Rejected/RevisionRequested; RevisionRequested → Submitted (parent edits); Failed → Submitted
(admin retry). Rejected and Cancelled are final. Every move writes an `EnrolmentStatusHistory` row.

### 30. The class search AND the validation agent both check time clashes. Isn't that duplication?
It is deliberate defence in depth, and each check has a different job:
- **ClassSearchTool** leaves out classes that clash with the child's current timetable, so the
  agents only propose places that can actually work (a helpful search).
- **ValidationSafetyAgent** re-checks the final proposal against fresh data, so even a bug in the
  search, or an LLM choosing badly, can never reach the admin as a valid proposal (a safety net).
- **ApproveAsync** checks once more inside the transaction, because the timetable may change
  between the proposal and the approval.
Found in manual testing: before the search filter existed, Sithmi (already in Pawn Stars) was
proposed Pawn Stars again. The validation agent caught it and the workflow failed safely, which
proved the safety net worked, but the proposal was useless. Test:
`Class_search_skips_classes_that_clash_with_the_childs_existing_class`.

---

## Phase 3 — React admin and coach web app

### 31. Why React Context for auth but TanStack Query for data? (ADR 0001)
They are different kinds of state.
- **Who is logged in** is small client state that rarely changes and is needed everywhere → a
  Context (`AuthContext.tsx`) is enough; Redux/Zustand would add a library for one value.
- **Classes, enrolments, workflows** are *server* state: they live in the API, can be stale, need
  loading/error states, caching, refetching and polling. TanStack Query does all of that, so pages
  don't need hand-written `useEffect` + `useState` fetching code.

### 32. How does the Workflow Review page update while the agents are running?
`useQuery` has `refetchInterval: (query) => status is Queued/Running ? 2000 : false` — it polls
every 2 s and stops by itself once the workflow finishes.

### 33. How does a protected route work? Is it security?
`ProtectedRoute` (`src/auth/ProtectedRoute.tsx`) redirects to `/login` if nobody is logged in, and
to `/forbidden` if the role is wrong. It is **only UX**: anyone can edit browser code, so the API
checks the JWT and role on every request anyway. Tests: `ProtectedRoute.test.tsx`.

### 34. What happens when the token expires?
`authStorage.loadSession` treats an expired token as logged out. If the API still answers 401,
`api()` in `client.ts` clears the session and fires a `skca:unauthorized` event; `AuthContext`
listens and sets the user to null, so routes redirect to login.

### 35. Where is the JWT stored, and what is the risk?
localStorage, so a refresh keeps you logged in. Risk: any JavaScript running on the page (XSS)
could read it. Mitigations: React escapes all text (parent notes are shown as text, never HTML),
no `dangerouslySetInnerHTML`, short token lifetime. An httpOnly cookie would be safer but needs
CSRF protection and same-site hosting — more complexity than this project needs.

### 36. How are API errors shown to the user?
`client.ts` turns every non-2xx response into an `ApiError` using the ProblemDetails `detail` (and
validation `errors`). `ErrorAlert` shows it with `role="alert"`. Example: approving a full class
shows "'Pawn Stars' is full (3/3)…" — tested in `WorkflowReviewPage.test.tsx`.

### 37. Why validate forms on the client if the server validates anyway?
Instant feedback without a network round trip. The server rules are the real ones; the client
copy (`utils/validation.ts`) is a convenience and never trusted by the API.

### 38. What accessibility features did you add?
Every input has a `<label>`; invalid fields get `aria-invalid` and `aria-describedby` pointing to the
error text; alerts use `role="alert"` / `role="status"`; tables have captions and `scope`;
a skip link; visible focus outlines; the chart has an `aria-label` summary; reduced-motion respected.

### 39. Why can't parents log in to the website?
The spec splits the roles by client: parents use the Flutter app, staff use the web app.
`AuthContext.login` refuses Parent accounts with a clear message (the API itself would allow the
login; it's the web app that decides it's the wrong client).

---

## Phase 4 — Flutter parent app

### 40. Why Provider (and not Riverpod)? (ADR 0002)
The app has three pieces of shared state (`AuthState`, `ChildrenState`, `EnrolmentsState`), each a
`ChangeNotifier`. Provider is the simplest option that the Flutter team documents: `context.watch<T>()`
rebuilds a widget when `notifyListeners()` is called, `context.read<T>()` calls methods without
listening. Riverpod adds compile-time safety and no BuildContext dependency, but brings more concepts
(providers of providers, `ref`, code generation) than a three-notifier app needs.

### 41. How are the screens protected?
`router.dart`: go_router's `redirect` runs before every navigation and whenever `AuthState` notifies
(`refreshListenable: auth`). Not logged in → `/login`; logged in on `/login` → `/enrolments`; still
reading the saved session → `/splash`. The API enforces the real security (JWT + ownership checks).

### 42. Where is the JWT stored on the phone, and why there?
`flutter_secure_storage` (`state/token_storage.dart`), which encrypts it with a key held in the
Android Keystore. SharedPreferences would store it as plain text readable from a rooted device or a
backup. On startup `AuthState.restore()` reads it and ignores it if expired.

### 43. How does the photo upload work end to end?
`image_picker` picks from camera/gallery with `maxWidth: 1024, imageQuality: 80` so the file is small →
`ApiClient.uploadFile` sends `multipart/form-data` with field `photo` and an `image/jpeg|png` content type
→ `POST /api/children/{id}/photo`, where the API checks size (≤2 MB), content type and the file's magic
bytes, then stores it with a server-generated name. Photos are displayed with `NetworkImage(url, headers:
{Authorization: ...})` because the photo endpoint checks ownership too.

### 44. How does the detail screen track status while the agents run?
`EnrolmentDetailScreen` polls every 3 s with `Timer.periodic` while the status is `Submitted` or
`AgentProcessing`, and cancels the timer when it moves on (and in `dispose()` so it never leaks).
Pull-to-refresh also reloads.

### 45. How do you test widgets that call the API?
`ApiClient` takes an `http.Client` in its constructor (dependency injection). Tests pass
`MockClient` from `package:http/testing.dart`, which returns canned responses and records requests, so
tests can assert what was sent (e.g. the POST body in `enrolment_form_test.dart`) with no network.
`InMemoryTokenStorage` replaces secure storage the same way.

### 46. Why does the release APK need the INTERNET permission added manually?
Flutter only adds it to the **debug** manifest (for hot reload). Without it in `src/main/AndroidManifest.xml`
a release build cannot make any network calls. Plain `http` is allowed only in the debug manifest
(`usesCleartextTraffic`), so the release app talks to the API over https only.

### 47. What does `10.0.2.2` mean in `API_BASE_URL`?
Inside the Android emulator, `localhost` is the emulator itself. `10.0.2.2` is the emulator's alias
for the host computer, where the API runs. The URL is passed at build time with
`--dart-define=API_BASE_URL=...` and read with `String.fromEnvironment` (`lib/config.dart`).

---

## Phase 5 — Deployment, performance, documentation

### 48. How is the system deployed, and what happens on a `git push`?
GitHub Actions CI runs all three test suites. Railway rebuilds `backend/Dockerfile` (root directory `backend`,
`railway.json` sets the health check) and redeploys the API; Vercel rebuilds `web/`. On startup the API applies EF
migrations and seeds an empty database, so no manual migration step. Secrets are Railway environment variables
(`ConnectionStrings__Default`, `Jwt__Key`, `GEMINI_API_KEY`…) — never in git. See ADR 0005.

### 49. Why Railway and not Render (which the plan first used)?
Render's free web service still needed card verification and the card was refused; Hugging Face Docker Spaces
turned out to be paid. Railway deploys from GitHub with our existing Dockerfile and no card on the trial. Because
the app is a standard container configured only by environment variables, switching host needed **no code
change** except honouring the host's `$PORT`. Trade-off: trial credit runs out; photos on the container disk
are lost on redeploy (would move to object storage behind `IPhotoStorage`).

### 50. Why does a multi-stage Dockerfile matter?
The first stage uses the big .NET SDK image to restore and publish; the final image only contains the ASP.NET
runtime and the published DLLs (~366 MB instead of >1 GB), has no compilers, and runs as the non-root `app`
user. Restoring the `.csproj` before copying the source lets Docker cache the package download layer.

### 51. What did the performance test show and what is the bottleneck?
k6, 30 virtual users (`perf/README.md`): `GET /api/classes` p95 24 ms with 0 % errors; 10 simultaneous enrolments
all reached `PendingAdminApproval`, agent latency p95 1.4 s with the fake model. The bottleneck is by design: one
background worker processes workflows sequentially, and with Gemini each takes ~10 s. That protects the free-tier
rate limit; scaling would mean several `Channel` readers.

### 52. Why test performance locally with the fake LLM instead of the live system?
Repeatable numbers (no network/LLM variance), no cost, no Gemini quota burned, and no test accounts left in the
production database. The live system was checked separately with a real Gemini workflow (≈ 9.6 s end to end).

### 53. How do you keep secrets out of a public repository?
`appsettings.json` has empty values; local secrets live in `dotnet user-secrets` (outside the repo); production
secrets are platform environment variables; `.gitignore` excludes `.env*`, `appsettings.Development.json`,
keystores. Before making the repo public the whole history was scanned for key patterns. Only placeholder
templates (`.env.example`, `appsettings.Development.example.json`) are committed.
