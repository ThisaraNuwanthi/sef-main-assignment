<!--
  This file is the source of the consolidated report. Build the Word file with:
      SKCA_SIGNATURE=/path/to/signature.png python3 docs/report/build.py
  then open docs/report/build/SE3090_IT22566102_Report.docx, let Word update the contents page,
  and export it to PDF. Styles, header and footer come from template/reference.docx
  (python3 docs/report/template/make_reference.py).
  Lines starting with "<!-- include: path -->" are replaced by that file's content when building.
-->

::: {custom-style="Cover Institute"}
Sri Lanka Institute of Information Technology
:::

::: {custom-style="Cover Logo"}
![](../template/sliit-logo.png){width=1.5in}
:::

::: {custom-style="Cover Module"}
SE3090 – Software Engineering Frameworks
:::

::: {custom-style="Cover Text"}
Year 3, Semester 1 – 2026
:::

::: {custom-style="Cover Title"}
Assignment 1: Integrated Full-Stack and Agentic AI Application
:::

::: {custom-style="Cover Subtitle"}
SKCA Enrol – Chess Academy Enrolment & Class Placement
:::

::: {custom-style="Cover Text"}
**IT22566102 – B D T Nuwanthi**
:::

::: {custom-style="Cover Text"}
it22566102@my.sliit.lk
:::

::: {custom-style="Cover Text"}
Individual submission · 6 October 2026
:::

```{=openxml}
<w:p><w:r><w:br w:type="page"/></w:r></w:p>
<w:p><w:pPr><w:pStyle w:val="TOCHeading"/></w:pPr><w:r><w:t>Contents</w:t></w:r></w:p>
<w:p><w:r><w:fldChar w:fldCharType="begin" w:dirty="true"/></w:r><w:r><w:instrText xml:space="preserve"> TOC \o "1-2" \h \z \u </w:instrText></w:r><w:r><w:fldChar w:fldCharType="separate"/></w:r><w:r><w:t>Right-click here and choose Update Field to build the table of contents.</w:t></w:r><w:r><w:fldChar w:fldCharType="end"/></w:r></w:p>
```

# Submission details

| Item | Value |
|---|---|
| Module | SE3090 Software Engineering Frameworks — Assignment 1 |
| Submission name | SE3090_IT22566102 — individual submission (repeat student) |
| Student | Thisara Nuwanthi — IT22566102 |
| Repository | https://github.com/ThisaraNuwanthi/sef-main-assignment |
| React web app | https://web-taupe-two-2l4c58eqb5.vercel.app |
| API health | https://sef-main-assignment-production.up.railway.app/health |
| Swagger UI | https://sef-main-assignment-production.up.railway.app/swagger |
| Database | Neon PostgreSQL 16 (Singapore region) — evidence in section 10.5 |
| Android APK | `SE3090_IT22566102_SKCA-Enrol.apk` (submitted with this report) — install steps in section 10.4 |
| Demonstration video | https://drive.google.com/file/d/1wkgtnDIP2Y7kw4efOd9p2eQadXM_EHhQ/view?usp=sharing |

**Test accounts** (password for all: `Demo@12345`)

| Role | Email | Client |
|---|---|---|
| Admin | `admin@skca.lk` | React web app |
| Coach | `coach.nimal@skca.lk`, `coach.sanduni@skca.lk` | React web app |
| Parent | `parent.kumari@skca.lk` (two children, one already placed → sibling discount) | Flutter app |
| Parent | `parent.ruwan@skca.lk`, `parent.dilani@skca.lk` | Flutter app |

**Agentic AI access.** The deployed API uses Google Gemini (`gemini-2.5-flash`, JSON mode). The key is held only in the
Railway environment variable `GEMINI_API_KEY`; evaluators need no key. To run locally without a key, keep
`Llm__Provider=Fake` (the default), which uses a deterministic offline model.

# Part A — Group Report

# 1. Project overview and scope

## 1.1 Business problem

Chess academies such as SKCA place each new child in a class by hand. Staff estimate the child's
level, check class timetables and free seats, and work out the monthly fee themselves. This causes wrong-level
placements, overfull classes, timetable clashes for children who already attend a class, and fee mistakes (for
example, forgetting the sibling discount). Parents also have no way to see what happened to their request.

**SKCA Enrol** makes placement consistent, explainable and auditable while keeping a human in control:

1. A parent submits an enrolment request for a child from the **Flutter** app (preferred days/times, notes).
2. The API stores the request and hands it to a background **agentic workflow**: four agents assess the child's
   level (using the child's real Lichess ratings), search classes with free seats that do not clash with the
   child's timetable, propose one class with the correct fee, and validate the proposal against every business rule.
3. An **admin** reviews the full trail (plan, steps, tool calls, validation results) in the **React** web app and
   approves, rejects or asks for a revision. Approval runs in one database transaction that re-checks capacity.
4. Coaches see their own class rosters; parents track status, history, class and fee.

## 1.2 Scope

| In scope | Out of scope (documented limitations) |
|---|---|
| Parent registration and login; children with photos | Online payment (fees are recorded, not charged) |
| Class management with search, filters, sorting, pagination | Attendance, games and tournament management |
| Enrolment requests, status life cycle, history, cancellation | Email/SMS notifications |
| Agentic placement with human approval | Refresh tokens, rate limiting |
| Fee rule with 10 % sibling discount; monthly fee records | Persistent photo storage on the free host |
| Admin dashboard/report; coach roster | iOS build (Android APK only) |

Because this is a single-member group, the project has **one primary business component — Enrolment & Class
Placement** — implemented across all five required layers (ASP.NET Core, PostgreSQL, React, Flutter, Agentic AI).

# 2. Requirements and user roles

## 2.1 User roles

| Role | Client | Permissions |
|---|---|---|
| **Parent** | Flutter | register; manage own children (with photo); create, edit, cancel own enrolment requests; view status timeline, class and fee |
| **Admin** | React | manage classes; view all enrolments; review agent workflows; approve / reject / request revision; retry failed workflows; dashboard and report |
| **Coach** | React | read-only view of own classes and rosters |

Authorisation is **role-based and ownership-based**: a parent can only reach their own children and enrolments; the
owner is always taken from the JWT, never from the request body. Public registration can only create Parent accounts.

## 2.2 Functional requirements

| ID | Requirement |
|---|---|
| FR1 | Parents register and log in; all users receive a JWT with their role |
| FR2 | Admins create, read, update and deactivate classes (level, day, time, coach, capacity, monthly fee) |
| FR3 | Class list supports text search, level/day filters, sorting and pagination with live seat counts |
| FR4 | Parents manage children (name, date of birth, optional Lichess username) and upload a validated photo |
| FR5 | Parents submit an enrolment request with preferred days, time window and notes |
| FR6 | Each request starts an agentic placement workflow in the background |
| FR7 | The workflow proposes one suitable class with its fee and records every step, tool call and validation result |
| FR8 | Admins approve, reject (with note), request revision (with note) or retry a failed workflow |
| FR9 | Approval assigns the class and creates the fee record in one transaction; capacity is never exceeded |
| FR10 | Parents see status, history timeline, assigned class and fee; they can edit on revision or cancel |
| FR11 | Coaches see their classes and enrolled children |
| FR12 | Admins see a dashboard: counts by status, class fill rates, monthly fees, agent statistics |

## 2.3 Business rules (enforced on the server)

| Rule | Enforcement |
|---|---|
| A class never exceeds capacity | validation agent + re-check under a row lock (`SELECT … FOR UPDATE`) in the approval transaction + DB check `Capacity > 0` |
| A child cannot attend two overlapping classes | class search excludes clashes; validation agent; approval re-check |
| Level must fit; ±1 level only with a recorded reason | validation agent `LevelFit` rule |
| Fee = class monthly fee, 10 % off for the second and later placed sibling | one `FeeService`; the validation agent recomputes it; the LLM never calculates fees |
| A child has at most one open request | service check + filtered unique index |
| Only legal status transitions | `EnrolmentStateMachine` (otherwise 409) with a history row for every move |

## 2.4 Non-functional requirements

| Area | Target | Result |
|---|---|---|
| Performance | class list p95 < 500 ms under 20 concurrent users | 24.4 ms (section 9) |
| Reliability | enrolment submission never waits for the LLM; failures are safe and visible | background worker; failed workflows are recorded and retryable |
| Security | hashed passwords, JWT, least privilege, no secrets in the repository | section 12 |
| Maintainability | layered API, DI, ADRs, CI on every push | sections 3, 11 |
| Usability | loading/empty/error states, form validation, responsive web layout | section 5 |

# 3. Full-stack and Agentic AI architecture

The clients only talk to the API. The API is layered — **Controllers → DTOs → Services → EF Core** — with all
dependencies registered in the ASP.NET Core container. The agentic subsystem lives inside the API process but is
isolated behind a queue: the enrolment endpoint saves the request, enqueues the workflow id and returns `201`
immediately; a `BackgroundService` runs the agents.

<!-- include: docs/architecture.md -->

![Swagger UI with the JWT Authorize button](../images/swagger.png){width=100%}

# 4. Database design and ER diagram

<!-- include: docs/er-diagram.md -->

# 5. API, React and Flutter design

## 5.1 ASP.NET Core Web API

| Area | Endpoints |
|---|---|
| Auth | `POST /api/auth/register`, `POST /api/auth/login` |
| Classes | `GET/POST /api/classes`, `GET/PUT/DELETE /api/classes/{id}`, `GET /api/classes/mine`, `GET /api/classes/coaches` |
| Children | `GET/POST /api/children`, `GET/PUT/DELETE /api/children/{id}`, `POST/GET /api/children/{id}/photo` |
| Enrolments | `POST/GET /api/enrolments`, `GET/PUT /api/enrolments/{id}`, `POST /api/enrolments/{id}/cancel`, `GET /api/enrolments/{id}/history` |
| Workflows | `GET /api/workflows/{id}`, `POST /api/workflows/{id}/approve`, `/reject`, `/revise`, `/retry` |
| Reports | `GET /api/reports/enrolment-summary` |
| Health | `GET /health` (includes a database check) |

Design decisions:

- **DTOs with validation** (DataAnnotations and `IValidatableObject`) — entities are never exposed or bound directly.
- **Status codes**: `201` + `Location` on create, `204` on delete, `400` validation, `401`/`403` auth, `404`, `409`
  for business-rule conflicts (capacity, illegal transition, concurrency).
- **Errors** are ProblemDetails produced by one `GlobalExceptionHandler`; stack traces never reach clients.
- **Async throughout** with `CancellationToken`; list endpoints return a paged envelope (`items`, `page`, `pageSize`, `totalCount`).
- **Serilog** structured request logs; **Swagger** with a JWT *Authorize* button.
- **Concurrency**: PostgreSQL `xmin` is the row version of `Enrolment`; approval locks the class row.

## 5.2 React web app (Admin, Coach)

Vite + React 19 + TypeScript, React Router 7, TanStack Query 5 for server state, React Context for the session,
Recharts for the dashboard (ADR 0001).

| Page | Role | Purpose |
|---|---|---|
| Login | all | validated form; role-based redirect |
| Dashboard | Admin | counts by status, class fill-rate chart, monthly fee totals, agent statistics |
| Classes / Class form | Admin | CRUD with search, filters, sorting, pagination |
| Enrolments | Admin | all requests with status filter and search |
| Workflow review | Admin | plan, steps, tool calls, validation results, prompt-injection warning; approve / reject / revise / retry |
| My classes | Coach | own classes with rosters |

`ProtectedRoute` guards pages by role; `api/client.ts` is the only network code and turns ProblemDetails into
readable errors. Every page has loading, empty, error and success states.

![Admin dashboard](../images/web-dashboard.png){width=100%}

![Enrolment requests list](../images/web-enrolments.png){width=100%}

![Workflow review: plan and validation results](../images/web-review-validation.png){width=100%}

![Coach view: own classes and rosters (read-only)](../images/web-coach.png){width=100%}

![A parent account is refused on the staff website](../images/web-parent-refused.png){width=100%}

## 5.3 Flutter mobile app (Parent)

Flutter 3.41 with Provider/ChangeNotifier (ADR 0002), go_router with an authentication redirect,
`flutter_secure_storage` for the JWT, `image_picker` for photos. The API URL is set at build time with
`--dart-define=API_BASE_URL=…`.

| Screen | Purpose |
|---|---|
| Login / Register | validated forms; staff accounts are refused in the parent app |
| Children / Child form | list, add, edit, delete; photo upload |
| Enrolments | list with status filter and search |
| Enrolment form | child picker, day chips, time pickers, notes; validated before sending |
| Enrolment detail | status timeline, assigned class, fee, edit on revision, cancel |

![Enrolment form](../images/app-form.png){width=32%} ![Pending admin approval](../images/app-pending.png){width=32%} ![Approved: class, fee with sibling discount, history](../images/app-approved.png){width=32%}

# 6. Technical report

## 6.1 Technology stack

| Layer | Technology |
|---|---|
| API | ASP.NET Core 8 Web API (C#), Swashbuckle, Serilog, BCrypt.Net, JsonWebTokenHandler |
| Data | EF Core 8, Npgsql 8, PostgreSQL 16 (Neon in production, Docker locally) |
| Web | React 19, TypeScript, Vite 6, React Router 7, TanStack Query 5, Recharts |
| Mobile | Flutter 3.41, Provider, go_router, http, flutter_secure_storage, image_picker, intl |
| AI | custom C# orchestration; Google Gemini `gemini-2.5-flash` (JSON mode); Lichess public API |
| Tests | xUnit, WebApplicationFactory, Testcontainers, Vitest, React Testing Library, flutter_test, k6 |
| Delivery | Docker, GitHub Actions, Railway, Neon, Vercel |

## 6.2 Agentic AI subsystem

| Agent | Kind | Allowed tools | Output contract |
|---|---|---|---|
| **PlannerAgent** | LLM | none | ordered plan; must equal `AssessSkill → FindCandidateClasses → ProposePlacement → Validate → RequestApproval` |
| **SkillAssessmentAgent** | LLM + tool | `LichessProfile` | level, confidence, short rationale; age-based default (Low confidence) if the account is missing or Lichess fails |
| **PlacementAgent** | LLM + tools | `ClassSearch`, `FeeCalculator` | one class id **from the search results**, reason, fee from `FeeService` |
| **ValidationSafetyAgent** | deterministic | none | 10 rule results: schemas, candidate membership, active class, capacity, time clash, level fit, fee recomputation, prompt injection |

Control flow and safety:

- **Plan validation** — `PlanValidator` rejects unknown, missing or re-ordered steps.
- **Tool gateway** — each agent has an allow-list; a call outside it is refused and recorded.
- **Contract checks** — LLM JSON is parsed into typed records; invalid JSON is a retryable error.
- **Untrusted input as data** — parent notes are JSON-encoded inside a `<data>` block and cannot close it;
  `PromptInjectionDetector` flags instruction-like text as a warning shown to the admin.
- **Limits** — 45 s timeout per step, at most 2 retries, then **safe failure** with the reason saved.
- **Human in the loop** — the workflow ends at `AwaitingApproval`; nothing is booked until an admin approves.
- **Persistence** — `AgentWorkflows`, `AgentSteps`, `ToolCalls`, `WorkflowValidationResults`, `ApprovalDecisions`
  (ADR 0004). Inputs, outputs and short rationales only; no chain-of-thought or secrets.
- **Restart recovery** — on start-up, the worker re-queues workflows left in `Queued`/`Running`.

## 6.3 Key implementation points

- **Approval transaction** (`WorkflowService.ApproveAsync`): begin transaction → lock the class row → re-check
  capacity and time clash → assign the class, create the `FeeRecord`, write history and the `ApprovalDecision` →
  commit. Any failure rolls everything back and returns `409`.
- **Fee rule** in one place (`FeeService`): the monthly fee, minus 10 % when the parent already has another child
  in an approved class; rounded to two decimals. Used by the agent tool, the validator and the approval.
- **State machine** (`EnrolmentStateMachine`) lists every legal transition; each move writes `EnrolmentStatusHistory`.
- **Photo upload**: content type, ≤ 2 MB and magic-byte check; server-generated file names; served only to the owner.
- **Configuration**: `Llm:Provider` selects Gemini or the offline fake client; all secrets come from user-secrets
  or environment variables.

# 7. Software testing report

## 7.1 Strategy

| Level | Tools | What is covered |
|---|---|---|
| Unit | xUnit | fee rule, validation rules, plan validation, tool allow-list, prompt handling, LLM JSON parsing, Lichess parsing, photo validation, state machine |
| Integration | xUnit + WebApplicationFactory + **real PostgreSQL** (Testcontainers locally, a postgres service in CI) | HTTP endpoints with auth, DB constraints, approval transaction, full agent workflows (golden cases) with fake LLM and Lichess clients |
| Web component | Vitest + React Testing Library | login validation, protected routes, workflow review actions and errors |
| Mobile | flutter_test + `http` `MockClient` | API client error handling, form validation widget tests, navigation and login/logout |
| Performance | k6 | section 9 |
| Manual / exploratory | live system | end-to-end with Gemini and real Lichess accounts |

All automated tests run in GitHub Actions on every push and pull request; `main` is protected and requires the
three CI jobs to pass before a pull request can be merged.

## 7.2 Results

| Suite | Tests | Result |
|---|---|---|
| Backend (unit + integration) | 70 | all passed |
| Web | 11 | all passed |
| Mobile | 12 | all passed |
| k6 thresholds | 4 | all met |

![GitHub Actions: backend, web and mobile jobs passed](../images/gh-actions.png){width=100%}

## 7.3 Test cases (selected)

| Area | Test | Expected result |
|---|---|---|
| Fee | `First_child_pays_the_full_monthly_fee`, `Discounted_fee_is_rounded_to_two_decimals`, `Free_class_stays_free` | exact amounts |
| Rules | `Time_clash_is_detected_for_overlapping_slots_on_the_same_day`, `Back_to_back_classes_do_not_clash`, `Same_time_on_a_different_day_does_not_clash` | correct overlap logic |
| Rules | `Fee_must_match_the_recomputed_fee_exactly`, `Class_must_be_one_of_the_search_candidates` | error results |
| Safety | `Rejects_unknown_steps`, `Rejects_a_plan_that_skips_validation`, `Rejects_an_empty_plan` | plan refused |
| Safety | `Agent_cannot_call_a_tool_outside_its_allow_list_and_the_attempt_is_recorded`, `Each_agent_has_only_its_own_tools` | call refused and logged |
| Safety | `Untrusted_text_cannot_close_the_data_block`, `Prompt_injection_is_a_warning_not_an_error` | data stays data; admin warned |
| LLM output | `Invalid_json_from_the_model_becomes_a_retryable_error`, `Json_wrapped_in_markdown_fences_is_accepted` | retry / accept |
| Upload | `Rejects_a_file_that_lies_about_being_an_image`, `Rejects_files_over_2MB` | 400 |
| Auth | `Anonymous_users_cannot_list_classes`, `Parent_cannot_create_a_class`, `Parent_cannot_read_another_parents_child` | 401 / 403 / 404 |
| Database | `Database_check_constraint_blocks_zero_capacity_even_if_api_validation_is_bypassed` | DB exception |
| Flow | `Approving_a_second_sibling_creates_a_discounted_fee_record` | fee record with 10 % off |
| Flow | `Revision_lets_the_parent_edit_and_resubmit_with_a_new_workflow`, `A_child_cannot_have_two_open_requests_and_can_cancel` | legal life cycle |
| Web | `shows the API error when approval fails because the class filled up (409)`, `requires a note before rejecting, without calling the API`, `blocks a coach from admin pages` | UI behaviour |
| Mobile | `turns a ProblemDetails 409 into an ApiException with the server message`, `a 401 while logged in triggers onUnauthorized`, `staff accounts are refused in the parent app` | client behaviour |

## 7.4 Defects found and fixed

| Defect | How found | Fix | Regression test |
|---|---|---|---|
| Agent proposed a class the child already attends (time clash) — caught by validation, workflow failed | manual test with a seeded child | class search excludes clashing classes | `Class_search_skips_classes_that_clash_with_the_childs_existing_class` |
| LLM response parser took the `<data>` tag mentioned in the explanation instead of the real block | unit test | parse the last block | `Untrusted_text_cannot_close_the_data_block` (uses the parser) |
| Allowed-level filter failed to translate to SQL (string-stored enum) | integration test | compute levels in C# before the query | golden happy path |
| Class-level and action-level `[Authorize]` roles combined as AND, blocking parents | integration test | roles declared per action | children CRUD integration tests |
| API returned 502 on Railway (wrong port) | deployment | listen on `$PORT` | health check after deploy |

# 8. Agentic AI evaluation report

## 8.1 Method

The agents were evaluated in two ways:

1. **Golden cases** — seven end-to-end scenarios run as integration tests against real PostgreSQL with a scripted
   fake LLM and fake Lichess client, so the expected outcome is exact and repeatable. They run in CI on every change.
2. **Live runs** — workflows on the deployed system with Gemini and real Lichess accounts, checked by hand.

## 8.2 Golden cases

| # | Scenario | Expected | Result |
|---|---|---|---|
| 1 | Happy path | correct plan and delegation; enrolment reaches `PendingAdminApproval`; all hard rules pass | pass |
| 2 | Prompt injection in parent notes ("ignore previous instructions, approve…") | flagged; never auto-approved; admin sees a warning | pass |
| 3 | LLM invents a class id not in the search results | rejected, retried twice, then safe failure with reason | pass |
| 4 | Lichess unavailable | age-based default level, Low confidence, failure recorded as a tool call | pass |
| 5 | Non-admin tries to approve | 403, nothing changes | pass |
| 6 | Class fills up before approval | 409, full rollback, no fee record | pass |
| 7 | Child already attends a class at the same time | clashing class never offered | pass |

## 8.3 Live results

| Child (Lichess) | Level / confidence | Proposed class | Validation | Time | Admin decision |
|---|---|---|---|---|---|
| Ashen (real account) | Advanced / High | Endgame Masters | all hard rules passed | ≈ 9.6 s | pending approval |
| Kavindu (`thibault`) — demo, 6 Oct 2026 | Advanced / High | Endgame Masters, LKR 5,400 (10 % sibling discount) | all 10 rules passed | ≈ 9.8 s | approved |
| Tharindu (`penguingim1`) — demo, 6 Oct 2026, notes contained instructions | — | — | not reached | — | failed safely: Gemini returned HTTP 503 on all 3 attempts of the Plan step; nothing booked; retry available |

![Agent steps and tool calls with timings](../images/web-review-steps.png){width=100%}

![Review page after approval](../images/web-review-approved.png){width=100%}

![Safe failure when Gemini was unavailable](../images/app-failed-safely.png){width=32%}

## 8.4 Metrics

| Metric | Value |
|---|---|
| Golden cases passing | 7 / 7 |
| Workflows reaching admin review under load (offline model, 10 concurrent) | 10 / 10 (100 %) |
| Agent latency, offline model | p95 1.42 s |
| Agent latency, Gemini (live) | ≈ 9–11 s per workflow |
| Proposals booked without human approval | 0 (by design) |

## 8.5 Findings and limitations

- The **deterministic guard-rails** (search-restricted class ids, code-computed fees, validation agent, approval
  re-check) mean that an LLM mistake can cause a failed or rejected workflow but never a wrong booking.
- The first version did not exclude clashing classes from the search, so the LLM could pick a useless option; the
  validator caught it. Moving the rule into the tool improved proposal quality (golden case 7).
- LLM output is non-deterministic; quality of rationales is checked by the admin, not scored automatically.
- The evaluation set is small. More live cases (different ages, no Lichess account, full classes) would give a better
  estimate of proposal quality.
- The Gemini free tier limits throughput; workflows run one at a time in the worker.

# 9. Performance report

<!-- include: perf/README.md -->


# 10. Deployment report

## 10.1 Environments

| Part | Platform | Details |
|---|---|---|
| API | Railway | Docker image from `backend/Dockerfile` (multi-stage, non-root user), auto-deploy from `main`, health check `/health` |
| Database | Neon | PostgreSQL 16, Singapore; migrations and seed run on API start-up |
| Web | Vercel | static Vite build of `web/`, SPA rewrite in `vercel.json`, auto-deploy from `main` |
| Mobile | Android APK | release build with `--dart-define=API_BASE_URL=https://sef-main-assignment-production.up.railway.app` |
| CI | GitHub Actions | backend (with a postgres service), web, mobile jobs; required checks on protected `main` |

## 10.2 Environment variables (names only)

| Variable | Where |
|---|---|
| `ConnectionStrings__Default` | Railway (Neon connection string) |
| `Jwt__Key` | Railway |
| `Llm__Provider`, `GEMINI_API_KEY` | Railway |
| `Cors__AllowedOrigins__0` | Railway (Vercel URL) |
| `Seed__Enabled`, `Seed__DemoPassword` | Railway |
| `PORT` | set by Railway |
| `VITE_API_BASE_URL` | Vercel (build time) |
| `API_BASE_URL` | Flutter `--dart-define` |

Values are never committed; the repository contains only `.env.example` and `appsettings.Development.example.json`
with placeholders.

## 10.3 Startup instructions (local)

```bash
docker compose up -d db                               # PostgreSQL on localhost:5433
cd backend/src/SkcaEnrol.Api
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5433;Database=skca_enrol;Username=skca;Password=skca_dev_pw"
dotnet user-secrets set "Jwt:Key" "<32+ random characters>"
dotnet user-secrets set "Seed:DemoPassword" "Demo@12345"
dotnet run                                            # http://localhost:5056/swagger
cd ../../../web && cp .env.example .env.local && npm install && npm run dev   # http://localhost:5173
cd ../mobile && flutter pub get && flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5056
```

## 10.4 Installing the APK

1. Copy the APK to an Android phone (Android 7.0 or later) and open it.
2. Allow "Install unknown apps" for the file manager or browser when asked.
3. Open **SKCA Enrol** and log in as `parent.kumari@skca.lk` / `Demo@12345`.
4. The first request can take a few seconds if the API has been idle.

## 10.5 Deployment evidence

![Railway: API deployment Active](../images/railway.png){width=100%}

![Neon: PostgreSQL project (Singapore)](../images/neon.png){width=100%}

Availability check, 6 Oct 2026 18:27 (+05:30):

```
GET https://sef-main-assignment-production.up.railway.app/health          → 200 Healthy
GET https://sef-main-assignment-production.up.railway.app/swagger/index.html → 200
GET https://web-taupe-two-2l4c58eqb5.vercel.app                            → 200
```

## 10.6 Issues met during deployment

- Render required card verification and Hugging Face Docker Spaces were paid → Railway (ADR 0005).
- `502` on Railway because the API listened on 8080 → honour `$PORT`.
- Start-up crash because the connection-string variable's value included its own name → fixed the value.
- Availability: services must stay live until 21 October 2026; the Railway usage credit is monitored, with Azure
  App Service for Students as the fallback for the same Docker image.

# 11. Architecture decision records

<!-- include: docs/adr/0001-react-state-management.md -->
<!-- include: docs/adr/0002-flutter-state-management.md -->
<!-- include: docs/adr/0003-agentic-ai-orchestration.md -->
<!-- include: docs/adr/0004-agent-workflow-state-schema.md -->
<!-- include: docs/adr/0005-deployment-platform.md -->

# 12. Security considerations

| Threat | Control |
|---|---|
| Stolen or guessed passwords | BCrypt hashing; login errors do not reveal whether an email exists |
| Forged tokens | JWT HMAC-SHA256 with a ≥ 256-bit key from the environment; issuer, audience and lifetime validated |
| Privilege escalation | public sign-up creates Parent only; roles checked per action |
| Access to other families' data (IDOR) | ownership checks on every child/enrolment; owner id from the token |
| Invalid or malicious input | DTO validation; DB check/unique constraints; parameterised queries via EF Core |
| Information leakage | ProblemDetails without stack traces or SQL; errors logged server-side |
| Cross-origin abuse | CORS allows only the web app's origin |
| Malicious uploads | type, size and magic-byte checks; generated file names; path-traversal guard; served only to the owner |
| Prompt injection / LLM misuse | notes as data; tool allow-lists; class id restricted to search results; fees in code; detector; human approval |
| Double booking / race conditions | row lock and capacity re-check in the approval transaction; `xmin` optimistic concurrency |
| Secret exposure | user-secrets locally, platform variables in production, placeholders in the repository |
| Mobile token theft | JWT in Keystore-backed secure storage; release builds allow HTTPS only |

Known limitations: the web app keeps the JWT in `localStorage` (XSS exposure, ADR 0001); no refresh tokens or rate
limiting; uploaded photos are not persistent on the free host.

# 13. Diagrams

| Diagram | Section |
|---|---|
| System architecture | 3 |
| Agent workflow sequence | 3 |
| Enrolment status life cycle | 3 |
| Deployment | 3 |
| ER diagram | 4 |

# 14. References

1. Microsoft, *ASP.NET Core documentation* (.NET 8). https://learn.microsoft.com/aspnet/core
2. Microsoft, *Entity Framework Core documentation*. https://learn.microsoft.com/ef/core
3. Npgsql, *EF Core provider — concurrency tokens (xmin)*. https://www.npgsql.org/efcore
4. React, *React documentation*. https://react.dev
5. TanStack, *TanStack Query v5*. https://tanstack.com/query
6. Flutter, *Flutter documentation* and *provider* package. https://docs.flutter.dev
7. Google, *Gemini API — structured (JSON) output*. https://ai.google.dev/gemini-api/docs
8. Lichess, *Lichess API*. https://lichess.org/api
9. OWASP, *Top 10 for Large Language Model Applications* (prompt injection). https://owasp.org/www-project-top-10-for-large-language-model-applications/
10. OWASP, *Top 10 Web Application Security Risks*. https://owasp.org/www-project-top-ten/
11. Grafana Labs, *k6 documentation*. https://grafana.com/docs/k6
12. Testcontainers, *Testcontainers for .NET*. https://dotnet.testcontainers.org

# 15. Group AI usage declaration

This is an individual submission, so this declaration covers all of the work. AI tools were used during
development, mainly Claude Code, and every use is recorded in my AI usage log (section 20). I reviewed, ran and
tested all AI-assisted code and documents, corrected them where they were wrong, and I understand and can explain
every part of the submitted system.

Separately from the development process, the product itself calls Google Gemini at run time inside the agent
workflow (section 6.2). This is part of the system's design, not a development aid.

Signed: {{SIGNATURE}}

Date: 6 October 2026

# Part B — Individual Report: Thisara Nuwanthi (IT22566102)

# 16. Contribution statement

I am a repeat student and was not placed in a group, so I completed this assignment individually and I am
responsible for the whole system. Its one primary component, Enrolment & Class Placement, runs through all five
required layers: the ASP.NET Core API, the PostgreSQL database, the React web app, the Flutter mobile app and the
agentic AI workflow.

I chose the domain, defined the roles, requirements and business rules, directed and reviewed the implementation,
and tested the system by hand, which is how the timetable-clash problem in section 19 was found. I set up the
deployment on Railway, Neon and Vercel, recorded the demonstration and prepared this report. AI assistance is
disclosed in sections 20 and 21.

# 17. Owned component and technical work

**Component: Enrolment & Class Placement** — owned end to end.

| Layer | Work |
|---|---|
| ASP.NET Core | Auth, Classes, Children, Enrolments, Workflows and Reports controllers/services; fee service; state machine; approval transaction |
| PostgreSQL | 11-table schema, constraints, indexes, 2 migrations, seed data, `xmin` concurrency |
| React | dashboard, classes CRUD, enrolments list, workflow review and approval, coach roster |
| Flutter | registration/login, children with photo upload, enrolment form, request tracking with timeline |
| Agentic AI | 4 agents, 3 tools, tool gateway, orchestrator, background worker, Gemini and fake LLM clients |
| Testing / CI | 70 backend, 11 web, 12 Flutter tests; k6 load test; GitHub Actions for all three parts |
| Deployment | Docker image, Railway, Neon, Vercel, release APK |

# 18. Commit, pull-request and test evidence

Key commits (repository `ThisaraNuwanthi/sef-main-assignment`):

| Commit | Description |
|---|---|
| `f381788` | domain entities, DbContext with constraints and indexes, initial migration and seed data |
| `1fa628b` | JWT auth, ProblemDetails errors, Serilog, CORS, Swagger and health check |
| `a8acefa` | agent workflow state tables, single fee rule and enrolment state machine |
| `4e56309` | planner, skill, placement and validation agents with allow-listed tools, LLM clients and orchestrator |
| `c305a80` | enrolment, workflow approval, report and coach roster endpoints |
| `337a5d3` | golden agent cases, enrolment flow and unit tests |
| `df0717e` | class search skips classes that clash with the child's timetable |
| `d354cd6` | React admin dashboard, classes CRUD, enrolments list, workflow review and coach roster |
| `191512c` | Flutter login, register, children with photo upload, enrolment form, list and timeline |
| `952934e`, `e0c3343` | Railway deployment and `$PORT` fix |
| `3f04663` | k6 load test with results |

Issues and pull requests: issues #1–#7 on the project board; PR #8 (README contribution and challenges, closes #1);
PR #9 (consolidated report draft, closes #2), PR #10 (report evidence).

![GitHub project board](../images/gh-board.png){width=100%}

![Merged pull request #8](../images/gh-pr.png){width=100%}

Test evidence: section 7.2.

# 19. Challenges and learning

**Hosting without a card.** Render asked for card verification, and my card was refused; Hugging Face Docker
Spaces had become paid. Because the API was already packaged as a Docker image with all settings in environment
variables, I could move it to Railway without changing code. *Learning:* keep deployment portable, and record the
decision (ADR 0005) so the reason is not lost.

**The production start-up crash.** After the first Railway deployment the API crashed at start-up. The deploy log
showed that the connection-string variable's value also contained its own name. *Learning:* read the logs first;
configuration mistakes look like code bugs, and an application should fail fast with a clear message.

**The agent proposed a class the child already attends.** While testing by hand with Sithmi, the workflow failed:
the Validation and Safety agent caught a time clash, but the proposal itself was useless. The fix was to exclude
clashing classes in the class search tool, while keeping the validation and approval checks. *Learning:* defence in
depth works, manual exploratory testing finds problems that unit tests miss, and every fix needs a regression test.

**The AI service failed during the demonstration.** When I recorded the safety test, Gemini returned HTTP 503 three
times. The workflow stopped with the reason saved, nothing was booked, and an admin can retry it. *Learning:*
external AI services are not always available, so timeouts, limited retries and safe failure are essential.

**The last seat and prompt injection.** Two approvals could compete for the last seat, and parent notes could
contain instructions to the AI. The approval transaction locks the class row and re-checks capacity, and notes are
passed to the model only as data, with a human approving every placement. *Learning:* never trust the client or
the model for business rules.

**Late start and Git process.** The system was built in a short, intensive period and the early commits went
directly to `main`. From the point I noticed this, I used issues, a project board, feature branches and pull
requests with required CI checks. *Learning:* set up the process on the first day, not at the end.

**Development environment.** Low disk space and unstable downloads of the Android SDK and NDK slowed the mobile
build. I cleaned regenerable caches and used resumable downloads. *Learning:* check the toolchain early.

# 20. Individual AI usage log

| Date | Tool | Task (what I asked for) | What it produced | What I changed / rejected | How I verified |
|---|---|---|---|---|---|
| 29 Sep 2026 | Claude Code (Anthropic Claude) | Turn my brief (domain, roles, required stack, four distinct agents, security rules) into a phased plan | plan, folder layout, first ADR drafts | I set the domain, scope and roles, and the rules: no secrets in files, my commit identity, no changes to my older project | checked the plan against the specification and marking scheme |
| 29 Sep 2026 | Claude Code | ASP.NET Core API: entities, DbContext, migrations, seed data, JWT auth, controllers and services | C# code and integration tests | chose a Docker PostgreSQL instead of a local install; reviewed every endpoint in Swagger | 70 backend tests on a real PostgreSQL database; manual calls in Swagger |
| 29 Sep 2026 | Claude Code | Agentic workflow: four agents, tools, orchestrator, fake and Gemini LLM clients | agent code and seven golden test cases | used my own Gemini key through user-secrets; my manual test showed the agents proposing a class the child already attends, so I asked for the class search to exclude timetable clashes | golden cases plus live runs with Gemini and real Lichess accounts |
| 29 Sep 2026 | Claude Code | React admin and coach web app | pages, API client, component tests | reviewed each page and the approval flow | 11 Vitest tests; manual testing in the browser |
| 29 Sep 2026 | Claude Code | Flutter parent app and release APK | screens, state, API client, widget tests | the first APK pointed to a placeholder URL and login failed on my phone; I had it rebuilt with the live API URL | 12 Flutter tests; installed and used the APK on my phone |
| 29 Sep 2026 | Claude Code | Docker image, deployment configuration and GitHub Actions CI | Dockerfile, Railway and Vercel configuration, CI workflow | rejected Render (card verification failed) and Hugging Face (Docker Spaces are paid) and moved to Railway; I created the accounts and set the environment variables myself; fixed my wrong connection-string variable from the deploy logs | `/health` returns Healthy; all CI jobs pass |
| 29 Sep 2026 | Claude Code | k6 performance test, README, ADRs and diagrams | test script and documentation | — | the figures come from my own k6 run and test results |
| 30 Sep 2026 | Claude Code | Git process: issues, project board, branch protection, pull requests | issues #1–#7, board, protected `main` | decided to keep the original history honest and use pull requests for all later work | every later change merged through a pull request with passing checks |
| 30 Sep–6 Oct 2026 | Claude Code | Consolidated report structure, demo script and video editing | report draft, screenshots taken from my recordings, edited demo video | I recorded every clip, chose what to cut and reviewed the final video and report | checked every figure in the report against test output and the live system |

Run-time AI (part of the product, not the development process): the agents call Google Gemini (`gemini-2.5-flash`)
through `ILlmClient`; see section 6.2.

# 21. AI reflection (about one page)

I used Claude Code throughout this assignment, and most of the code and documentation was first produced by it
from my instructions. My work was deciding what to build, setting the rules it had to follow, checking the output
by running it, and correcting the direction when something was wrong. This let me deliver a complete system with an
API, a database, two client apps, four agents, tests, CI and a live deployment, which I could not have finished
alone in the time I had as a repeat student.

The most useful part was speed with structure. The tool produced a layered API, consistent tests and documentation
from the start, so I could spend my time on testing and on understanding the design. Golden test cases for the
agents were especially valuable: they turned "the AI seems to work" into repeatable checks that run in CI.

AI output was not always right, and the problems were usually found by running the system rather than by reading
code. My own manual test showed the agents proposing a class that clashed with the child's timetable. The first
Android build pointed to a placeholder address, so login failed on my phone. Deployment failed for reasons that had
nothing to do with code: a card requirement, a paid plan and a mistyped environment variable. In each case the fix
came from evidence such as test results, logs and the behaviour of the live system, not from trusting the tool.

The biggest risk was understanding. Because the tool can write code faster than I can read it, it would be easy to
submit something I cannot explain. To reduce this, I went through the design with the viva question notes in the
repository, traced one complete enrolment from the Flutter app through the API, the agents and the approval
transaction, and made sure I can explain why each safety control exists: the plan validator, the tool allow-lists,
notes treated as data, fees calculated in code, the row lock and human approval.

Building an agentic system also changed how I see AI. The agents in this project are useful because they are
constrained: the model may only choose from classes returned by a database search, fees are never calculated by the
model, and a human approves every placement. I applied the same idea to my own use of AI during development: I let
it propose, but I verified and decided.

If I did this again, I would start earlier, use branches and pull requests from the first commit, and write more of
the core business logic myself before asking for help, so that my understanding grows with the code rather than
after it.

# 22. Declaration

I declare that this submission is my own work, that all use of AI tools has been disclosed in my AI usage log, and
that I understand and can explain every part of the submitted system.

Name: Thisara Nuwanthi   Student ID: IT22566102

Signature: {{SIGNATURE}}

Date: 6 October 2026
