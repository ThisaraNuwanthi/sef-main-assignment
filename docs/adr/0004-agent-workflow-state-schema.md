# ADR 0004 — Storing agent workflow state

- **Status:** Accepted
- **Date:** 2026-09-29

## Context

Admins must be able to see, for every enrolment, what the agents planned, what each agent received and returned,
which tools were called (and whether they failed), how long everything took, which rules passed, and who made
the final decision. The workflow runs in the background, so its progress must survive between requests and be
visible while it is still running. It must also survive a server restart without hanging forever.

## Options considered

| Option | For | Against |
|---|---|---|
| Keep state in memory, return it at the end | Simple | Lost on restart; nothing to show while running; no audit trail |
| One `jsonb` blob per workflow | One table | Hard to query ("which tool fails most?"), rewrites the whole blob on every step |
| Event log (append-only events) | Complete history | More complex to read back; overkill here |
| **Normalised tables + `jsonb` for payloads** | Queryable structure, flexible payloads, saved step by step | More tables |

## Decision

Five tables (see `docs/er-diagram.md`):

- `AgentWorkflows` — one row per run: objective, `PlanJson` (jsonb), status, `FinalOutcome` (jsonb proposal),
  failure reason, start/end time. A retry or resubmission creates a **new** row, so failed runs stay as evidence.
- `AgentSteps` — one row per plan step: agent name, `InputJson`/`OutputJson` (jsonb), status, duration, retry
  count, last error. Saved as `Running` before the agent starts, then updated.
- `ToolCalls` — one row per tool use (including allow-list denials), with input/output jsonb, success, duration.
- `ValidationResults` — one row per deterministic rule, with severity (`Error` blocks, `Warning` informs).
- `ApprovalDecisions` — the admin, decision, note and time; together with `EnrolmentStatusHistory` this is the audit
  trail for the high-impact action.

Payloads are `jsonb` because each agent has a different typed contract; the structure around them is relational.

## Consequences

- The review page can show live progress (steps appear as they are saved) and the dashboard can compute agent
  statistics (average duration, success rate) with plain SQL.
- On startup `WorkflowWorker` re-queues `Queued` workflows and marks interrupted `Running` ones as `Failed`
  (admins can retry), so nothing hangs after a crash or redeploy.
- We deliberately store **no hidden chain-of-thought, passwords or tokens** — only inputs, outputs and short
  rationales that the prompt explicitly asks for.
- Payload size grows with every run; for a real deployment old workflow rows could be archived after a retention period.
