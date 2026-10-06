# AI usage log

Thisara Nuwanthi (IT22566102). The same log appears in section 20 of the report.

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
