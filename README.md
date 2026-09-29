# SKCA Enrol — Chess Academy Enrolment & Class Placement

SE3090 Software Engineering Frameworks — Assignment 1 · Thisara Nuwanthi

An enrolment system for a chess academy. Parents request a class for their child from a mobile app; a team of
AI agents assesses the child's level (using real Lichess ratings), finds suitable classes with free seats,
proposes a placement with the correct fee and checks it against every business rule. An admin reviews the
proposal on the web and approves it in one safe database transaction.

## Live system

| Part | URL |
|---|---|
| Web app (Admin, Coach) | https://web-taupe-two-2l4c58eqb5.vercel.app |
| API — Swagger UI | https://sef-main-assignment-production.up.railway.app/swagger |
| API — health check | https://sef-main-assignment-production.up.railway.app/health |
| Parent app (Android) | build with the command in [Mobile](#mobile-flutter) — APK attached to the submission |
| Source code | https://github.com/ThisaraNuwanthi/sef-main-assignment |

### Test accounts (password for all: `Demo@12345`)

| Role | Email | Use it on |
|---|---|---|
| Admin | `admin@skca.lk` | web |
| Coach | `coach.nimal@skca.lk`, `coach.sanduni@skca.lk` | web |
| Parent | `parent.kumari@skca.lk` (2 children, one already placed → sibling discount) | mobile |
| Parent | `parent.ruwan@skca.lk`, `parent.dilani@skca.lk` | mobile |

> The API runs on a free plan: if nobody has used it for a while, the first request can take a few seconds.

## 1. Business problem

Placing a new child in the right class is done by hand today: staff guess the level, check class times and
capacity manually and calculate fees themselves. This leads to wrong-level placements, overfull classes, clashing
timetables and fee mistakes (e.g. forgetting the sibling discount). SKCA Enrol makes the placement consistent,
explainable and auditable, while a human still makes the final decision.

## 2. Roles

| Role | Client | Can |
|---|---|---|
| **Parent** | Flutter mobile app | register, manage own children (with photo), submit/edit/cancel enrolment requests, track status, history, class and fee |
| **Admin** | React web app | manage classes, see all requests, review each agent workflow, approve / reject / request revision, retry failed workflows, dashboard |
| **Coach** | React web app | read-only view of own classes and rosters |

## 3. Features

- JWT login with BCrypt passwords, role-based **and** ownership-based authorisation.
- Classes: full CRUD with search, level/day filters, sorting, pagination, live seat counts.
- Children with validated photo upload (JPEG/PNG, ≤ 2 MB, magic-byte check, served only to the owner).
- Enrolment requests with preferred days/times and notes; full status timeline.
- **Agentic placement workflow** (4 agents, tools, validation, human approval) running in the background.
- Transactional approval that re-checks capacity under a row lock (no overbooking, even with two admins).
- Fee rule with 10 % sibling discount in one service; one fee record per enrolment per month.
- Admin dashboard: counts by status, class fill-rate chart, monthly fee totals, agent statistics.
- ProblemDetails errors, Serilog request logs, Swagger with JWT, health check.

## 4. Technology choices

| Layer | Choice | Why |
|---|---|---|
| API | ASP.NET Core 8 Web API (C#) | required stack; LTS; built-in DI, auth, validation, ProblemDetails |
| Data | EF Core 8 + PostgreSQL (Npgsql) | required; migrations; `jsonb` for agent payloads; `xmin` concurrency |
| Web | React 19 + TypeScript + Vite, React Router, TanStack Query, Recharts | [ADR 0001](docs/adr/0001-react-state-management.md) |
| Mobile | Flutter + Provider, go_router, flutter_secure_storage, image_picker | [ADR 0002](docs/adr/0002-flutter-state-management.md) |
| AI | Custom C# orchestration, Google Gemini (JSON mode) + offline fake client | [ADR 0003](docs/adr/0003-agentic-ai-orchestration.md) |
| Hosting | Railway (API, Docker), Neon (PostgreSQL), Vercel (web) | [ADR 0005](docs/adr/0005-deployment-platform.md) |
| Tests | xUnit + WebApplicationFactory + Testcontainers, Vitest + RTL, flutter_test, k6 | |

## 5. Architecture

```
React (Vercel) ─┐                    ┌──────────── ASP.NET Core API (Railway) ─────────────┐
                ├── HTTPS + JWT ──▶  │ Controllers → DTOs → Services → EF Core → PostgreSQL │──▶ Neon
Flutter (APK) ──┘                    │        └─▶ WorkflowQueue → Worker → Orchestrator     │──▶ Gemini
                                     │                     → 4 agents → tools               │──▶ Lichess
                                     └───────────────────────────────────────────────────────┘
```

Full Mermaid diagrams (system, agent sequence, status life cycle, deployment): **[docs/architecture.md](docs/architecture.md)**.

## 6. Agentic AI architecture

| Agent | Type | Tools (allow-list) | Output |
|---|---|---|---|
| **PlannerAgent** | LLM | — | plan from the fixed steps `AssessSkill → FindCandidateClasses → ProposePlacement → Validate → RequestApproval` (anything else is rejected) |
| **SkillAssessmentAgent** | LLM + tool | `LichessProfile` | level, confidence, rationale; age-based default (Low confidence) if there is no or a failing Lichess account |
| **PlacementAgent** | LLM + tools | `ClassSearch`, `FeeCalculator` | one class id **from the search results only**, reason, fee from `FeeService` |
| **ValidationSafetyAgent** | deterministic | — | 10 rules: output schemas, candidate membership, active class, capacity, time clash, level fit, fee recomputation, prompt injection |

Safety mechanisms: plan validation, per-agent tool allow-list via `ToolGateway`, LLM output contract checks,
parent notes passed as JSON-encoded `<data>` (never as instructions), prompt-injection flagging, 45 s timeout and
2 retries per step, safe failure with a recorded reason, and **human approval** before anything is booked.
Every step, tool call, validation result and decision is stored and shown on the admin review page.
Details: [ADR 0003](docs/adr/0003-agentic-ai-orchestration.md), [ADR 0004](docs/adr/0004-agent-workflow-state-schema.md).

## 7. Database design

11 tables, check/unique constraints, indexes and `jsonb` payloads — see **[docs/er-diagram.md](docs/er-diagram.md)**.

## 8. Repository structure

```
backend/
  SkcaEnrol.sln
  Dockerfile, railway.json
  src/SkcaEnrol.Api/        Controllers, Dtos, Services, Domain, Data (DbContext, migrations, seed),
                            Auth, Common (errors, paging), Agents (agents, tools, LLM clients, orchestration),
                            Integrations (Lichess)
  tests/SkcaEnrol.Tests/    Unit + Integration (golden agent cases)
web/                        React admin/coach app (Vercel)
mobile/                     Flutter parent app (Android)
perf/                       k6 load test + results
docs/                       ADRs, ER diagram, architecture diagrams, requirements checklist, AI usage log
.github/workflows/ci.yml    CI for all three parts
docker-compose.yml          local PostgreSQL
VIVA_QUESTIONS.md           viva preparation notes
```

## 9. Running locally

**Prerequisites:** .NET 8 SDK, Node 22, Flutter 3.41+, Docker (for PostgreSQL and backend integration tests).

**Startup order:** database → API → web / mobile.

### Database
```bash
docker compose up -d db          # PostgreSQL 16 on localhost:5433
```

### API
```bash
cd backend/src/SkcaEnrol.Api
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5433;Database=skca_enrol;Username=skca;Password=skca_dev_pw"
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)"
dotnet user-secrets set "Seed:DemoPassword" "Demo@12345"
# optional — real AI instead of the offline fake:
dotnet user-secrets set "Llm:Provider" "Gemini"
dotnet user-secrets set "GEMINI_API_KEY" "<your key>"
dotnet run                        # http://localhost:5056/swagger — migrates and seeds on first start
```

### Web
```bash
cd web
cp .env.example .env.local        # VITE_API_BASE_URL=http://localhost:5056
npm install && npm run dev        # http://localhost:5173
```

### Mobile
```bash
cd mobile
flutter pub get
flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5056   # Android emulator → your computer
```

### Environment variables

| Variable | Used by | Purpose |
|---|---|---|
| `ConnectionStrings__Default` | API | PostgreSQL connection (Npgsql format) |
| `Jwt__Key` | API | JWT signing key, ≥ 32 characters |
| `Llm__Provider` | API | `Fake` (default) or `Gemini` |
| `GEMINI_API_KEY` | API | Gemini key (only when provider is Gemini) |
| `Cors__AllowedOrigins__0` | API | URL of the web app |
| `Seed__Enabled`, `Seed__DemoPassword` | API | demo data for an empty database |
| `PORT` | API | port set by the host (Railway) |
| `VITE_API_BASE_URL` | web (build time) | API URL |
| `API_BASE_URL` | mobile (`--dart-define`) | API URL |
| `TEST_DB_CONNECTION` | backend tests | use this PostgreSQL instead of Testcontainers |

Templates with placeholders only: [`.env.example`](.env.example), `backend/src/SkcaEnrol.Api/appsettings.Development.example.json`, `web/.env.example`.

## 10. API documentation

Interactive Swagger UI (with an **Authorize** button for the JWT): `/swagger` on the live or local API.
Main endpoints:

| Area | Endpoints |
|---|---|
| Auth | `POST /api/auth/register`, `POST /api/auth/login` |
| Classes | `GET/POST /api/classes`, `GET/PUT/DELETE /api/classes/{id}`, `GET /api/classes/mine`, `GET /api/classes/coaches` |
| Children | `GET/POST /api/children`, `GET/PUT/DELETE /api/children/{id}`, `POST/GET /api/children/{id}/photo` |
| Enrolments | `POST/GET /api/enrolments`, `GET/PUT /api/enrolments/{id}`, `POST /api/enrolments/{id}/cancel`, `GET /api/enrolments/{id}/history` |
| Workflows | `GET /api/workflows/{id}`, `POST /api/workflows/{id}/approve \| reject \| revise \| retry` |
| Reports | `GET /api/reports/enrolment-summary` |
| Health | `GET /health` |

## 11. Tests

| Part | Command | Count |
|---|---|---|
| Backend (unit, golden agent cases, integration on real PostgreSQL) | `cd backend && dotnet test` | 70 |
| Web | `cd web && npm test` | 11 |
| Mobile | `cd mobile && flutter test` | 12 |
| Performance | `k6 run perf/load-test.js` — see [perf/README.md](perf/README.md) | 4 thresholds |

Backend integration tests start PostgreSQL with **Testcontainers** (needs Docker), or use `TEST_DB_CONNECTION`
(CI uses a postgres service container). With Colima on macOS also set
`DOCKER_HOST=unix://$HOME/.colima/default/docker.sock` and `TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE=/var/run/docker.sock`.

Golden agent cases: happy path → `PendingAdminApproval` with the right plan and delegation · prompt injection
flagged and not auto-approved · invented class id rejected and retried, then safe failure · Lichess failure →
age-based fallback · approval refused for non-admins · capacity exceeded at approval → 409 and rollback ·
timetable clash never offered.

Performance (local, 30 virtual users): `GET /api/classes` p95 **24 ms**, 0 % errors, agent workflow p95 **1.4 s**
with the offline model (≈ 10 s per workflow with Gemini).

## 12. Deployment

| Part | Platform | How |
|---|---|---|
| API | Railway | New project from the GitHub repo, root directory `backend` (uses `Dockerfile` + `railway.json`), set the environment variables above, generate a domain. Auto-deploys on every push to `main`. |
| Database | Neon | Create a project, copy the **.NET** connection string into `ConnectionStrings__Default`. Migrations and seed run automatically when the API starts. |
| Web | Vercel | Import the repo, root directory `web`, set `VITE_API_BASE_URL`. Then put the Vercel URL in the API's `Cors__AllowedOrigins__0`. |
| Mobile | APK | `cd mobile && flutter build apk --release --dart-define=API_BASE_URL=https://sef-main-assignment-production.up.railway.app` → `build/app/outputs/flutter-apk/app-release.apk` |
| CI | GitHub Actions | `.github/workflows/ci.yml` — backend build + tests (postgres service), web lint/test/build, Flutter analyze/test |

Why these platforms (and why not Render / Hugging Face): [ADR 0005](docs/adr/0005-deployment-platform.md).

## 13. Security considerations

- Passwords hashed with **BCrypt**; login errors don't reveal whether an email exists.
- **JWT** (HMAC-SHA256, ≥ 256-bit key from the environment), validated issuer/audience/lifetime; roles in the token.
- Public sign-up can only create **Parent** accounts.
- **Ownership checks** on every child/enrolment access, with IDs taken from the token, never from the request body.
- Server-side validation on every DTO; database check/unique constraints as a second line of defence.
- **ProblemDetails** errors: no stack traces or SQL reach clients; unexpected errors are logged server-side.
- CORS allows only the web app's origin. Swagger is public but every data endpoint needs a JWT.
- Uploads: type + size + magic-byte checks, server-generated file names, path-traversal guard, served only through an authorised endpoint.
- **AI safety:** tool allow-lists, LLM output validation, class id restricted to search results, fees computed in code,
  parent notes treated as data, prompt-injection flagging, human approval, no chain-of-thought or secrets stored.
- **Secrets** only in user-secrets (local) and platform environment variables; the repository contains placeholders only.
- Concurrency: row lock on approval (`SELECT … FOR UPDATE`) and `xmin` optimistic concurrency on enrolments.
- Mobile: JWT in Android Keystore-backed secure storage; release builds allow HTTPS only.
- Known limitations: the web app stores the JWT in `localStorage` (XSS risk, see ADR 0001); uploaded photos are not
  persistent on the free host (ADR 0005); no refresh tokens or rate limiting.

## 14. AI usage declaration

<!-- Complete this section honestly before submission. Keep the detailed log in docs/ai-usage-log.md. -->

**To be completed by the student.** Describe which AI tools were used during development, for which parts, how
the output was reviewed, changed and tested, and which parts were written or substantially rewritten by hand.
See [docs/ai-usage-log.md](docs/ai-usage-log.md).

Note: the *product itself* uses AI at runtime (Google Gemini via the agent workflow), described in section 6.

## 15. More documents

- [docs/requirements-checklist.md](docs/requirements-checklist.md) — every requirement mapped to where it is implemented
- [docs/adr/](docs/adr) — architecture decision records 0001–0005
- [VIVA_QUESTIONS.md](VIVA_QUESTIONS.md) — viva preparation
