# Performance test (k6)

`load-test.js` runs two scenarios **at the same time**:

| Scenario | Load | What it measures |
|---|---|---|
| `browse_classes` | 20 virtual users for 30 s, `GET /api/classes` (filtered, sorted, paged) | read latency and error rate under load |
| `submit_enrolments` | 10 parents each `POST /api/enrolments`, then poll until the agents finish | success rate and **end-to-end agent workflow latency** |

`setup()` registers 10 fresh parents with one child each, so the test never depends on seed data.

## Run

```bash
brew install k6                          # or see k6.io/docs/get-started/installation
# API running locally with the offline LLM so the test is repeatable and free:
Llm__Provider=Fake dotnet run --project backend/src/SkcaEnrol.Api
k6 run --summary-export perf/results/summary.json perf/load-test.js
```

Use a local/dev database: the test creates accounts and enrolments.
With `Llm__Provider=Gemini` the agent latency is dominated by the model (≈ 9–11 s per workflow in
the live deployment) and 10 simultaneous workflows would exceed the Gemini free-tier rate limit.

## Results (29 Sep 2026, MacBook Air M-series, API + PostgreSQL 16 in Docker, fake LLM)

| Metric | Result | Threshold |
|---|---|---|
| Total requests | 2,849 (88 req/s) | — |
| Failed requests | **0 %** | < 1 % ✅ |
| All requests | avg 15.5 ms · p95 27 ms · max 430 ms | — |
| `GET /api/classes` p95 | **24.4 ms** | < 500 ms ✅ |
| Agent workflow latency (submit → PendingAdminApproval) | avg 1.01 s · p95 1.42 s · max 1.42 s | p95 < 30 s ✅ |
| Enrolments reaching admin review | **100 %** (10/10) | > 95 % ✅ |

### What the numbers mean

- Reads stay fast because the class list is a single SQL query (seat counts are a sub-count in the
  projection, and `Level + DayOfWeek` / `AssignedClassId + Status` are indexed).
- The background worker runs workflows **one at a time**, so 10 simultaneous submissions queue up:
  the first finishes in ~0.9 s, the last in ~1.4 s. With the real LLM each workflow takes ~10 s, so a
  burst of 10 would take ~100 s to clear. That is acceptable for an academy (a few requests a day)
  and keeps us under the Gemini rate limit; the fix if needed is running several workers
  (`Channel` readers) in parallel.
- `POST /api/enrolments` itself returns in milliseconds (201 + workflow id) because the agents run
  in the background — the parent never waits for the LLM.
