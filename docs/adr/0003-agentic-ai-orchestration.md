# ADR 0003 — Agentic AI orchestration

- **Status:** Accepted
- **Date:** 2026-09-29

## Context

Placing a child needs several different kinds of work: understanding the request, judging skill from outside
data (Lichess), searching real class data, pricing, and checking business rules. The assignment requires an
agentic workflow with at least a plan, multiple agents, tools, state, and human approval for high-impact actions.
Hard requirements for us: the result must be **safe** (never overbook, never invent a class, never let a parent's
text steer the system), **traceable** (every step visible to an admin), **testable offline**, and **free to run**.

## Options considered

| Option | For | Against |
|---|---|---|
| Python service with LangGraph/CrewAI | Rich agent libraries | Second language/runtime/deployment; clients must only call the ASP.NET Core API; harder to keep business rules in one place |
| Semantic Kernel (C#) | Microsoft SDK, planners | Extra abstraction to learn and explain; its automatic planners/function-calling give the model more freedom than we want |
| **Custom orchestration in C# inside the API** (allowed by the spec) | Same codebase and DI as the rest; full control over plan validation, tool allow-lists, retries, timeouts and persistence; easy to test | We write the orchestration ourselves (~300 lines) |

LLM provider: **Google Gemini** (free tier, JSON output mode) behind an `ILlmClient` interface, with a
**`FakeLlmClient`** chosen by configuration for tests, CI and offline demos.

## Decision

A custom orchestrator (`Agents/Orchestration/WorkflowOrchestrator.cs`) with **four distinct agents**, each with
its own class, typed input/output records (`AgentContracts.cs`) and tool allow-list:

| Agent | LLM | Tools |
|---|---|---|
| PlannerAgent | yes | none |
| SkillAssessmentAgent | yes (only when Lichess data exists) | LichessProfile |
| PlacementAgent | yes | ClassSearch, FeeCalculator |
| ValidationSafetyAgent | **no** — deterministic rules | none (read-only DB) |

Safety design:

1. **The LLM proposes, code decides.** Plans must match the fixed allowed steps in order; the placement may only
   pick a class id returned by the ClassSearch tool; fees come only from `FeeService`; every LLM answer is parsed
   and checked against its contract before use (bad JSON / bad values → retry).
2. **Least privilege.** Agents never hold tool objects; they call `ToolGateway`, which checks the agent's allow-list
   and records every call (including denied ones).
3. **Untrusted text is data.** Parent notes are passed inside a JSON-encoded `<data>` block, never in the
   objective/instructions; `PromptInjectionDetector` flags instruction-like text for the admin.
4. **Safe failure.** Each step has a timeout and max 2 retries; anything unrecoverable ends as `Failed` with the
   reason recorded — never a half-finished placement.
5. **Human in the loop.** The workflow stops at `AwaitingApproval`. Only an Admin's approval, inside one database
   transaction that re-checks capacity with a row lock, books the place and creates the fee.
6. **Asynchronous.** `POST /api/enrolments` only queues the workflow (a `Channel<int>` read by a
   `BackgroundService`), so parents get an instant response.

## Consequences

- Golden-case tests run the real orchestrator against real PostgreSQL with the fake LLM (happy path, prompt
  injection, invented class id, Lichess failure, non-admin approval, capacity race).
- Because the LLM has little freedom, it cannot cause harm — but it also cannot invent a better plan. That is the
  intended trade-off for a system that handles bookings and money.
- One worker processes workflows sequentially, which is simple and stays under the Gemini free-tier rate limit;
  more throughput would need parallel workers.
- Only short rationales are stored, never hidden chain-of-thought, passwords or tokens.
