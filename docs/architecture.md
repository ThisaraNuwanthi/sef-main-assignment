# Architecture diagrams

## 1. System architecture

```mermaid
flowchart LR
    subgraph Clients
        WEB["React web app<br/>(Admin, Coach)<br/>Vercel"]
        APP["Flutter app<br/>(Parent)<br/>Android APK"]
    end

    subgraph API["ASP.NET Core Web API (Docker, Railway)"]
        direction TB
        CTRL["Controllers<br/>+ DTO validation<br/>+ JWT / roles"]
        SVC["Services<br/>Class · Child · Enrolment<br/>Workflow · Fee · Report"]
        subgraph AI["Agentic AI subsystem"]
            Q["WorkflowQueue<br/>(Channel)"]
            W["WorkflowWorker<br/>(BackgroundService)"]
            ORCH["WorkflowOrchestrator"]
            AG["4 agents + ToolGateway"]
        end
        EF["EF Core AppDbContext"]
        CTRL --> SVC --> EF
        SVC -- "enqueue id" --> Q --> W --> ORCH --> AG
        AG --> EF
    end

    DB[("PostgreSQL<br/>Neon")]
    GEM["Google Gemini<br/>(JSON mode)"]
    LI["Lichess public API"]

    WEB -- "HTTPS + JWT" --> CTRL
    APP -- "HTTPS + JWT" --> CTRL
    EF --> DB
    AG -- "ILlmClient" --> GEM
    AG -- "LichessProfile tool" --> LI
```

The clients only ever talk to the API. The API is layered: **Controllers → DTOs → Services → EF Core**, all wired by dependency
injection. Errors from any layer become ProblemDetails in one `GlobalExceptionHandler`.

## 2. Agent workflow (one enrolment)

```mermaid
sequenceDiagram
    autonumber
    actor Parent
    participant API as Enrolments API
    participant Q as WorkflowQueue
    participant O as Orchestrator
    participant P as PlannerAgent (LLM)
    participant S as SkillAssessmentAgent (LLM + Lichess)
    participant PL as PlacementAgent (LLM + ClassSearch + FeeCalculator)
    participant V as ValidationSafetyAgent (rules, no LLM)
    actor Admin

    Parent->>API: POST /api/enrolments
    API->>API: save Enrolment (Submitted) + AgentWorkflow (Queued)
    API->>Q: enqueue workflow id
    API-->>Parent: 201 {enrolmentId, workflowId}
    Q->>O: RunAsync(id) in background
    O->>P: objective
    P-->>O: plan (validated: fixed steps, fixed order)
    O->>S: child age + Lichess username
    S-->>O: level, confidence, rationale (or age default)
    O->>PL: FindCandidateClasses (DB search, skips timetable clashes)
    PL-->>O: candidates
    O->>PL: ProposePlacement (notes as DATA)
    PL-->>O: classId ∈ candidates, reason, fee from FeeService
    O->>V: all outputs + parent notes
    V-->>O: 10 rule results (capacity, clash, level, fee, injection…)
    alt a hard rule failed or a step failed after 2 retries
        O->>O: Workflow = Failed (reason saved), Enrolment = Failed
    else all hard rules passed
        O->>O: Workflow = AwaitingApproval, Enrolment = PendingAdminApproval
    end
    Admin->>API: POST /api/workflows/{id}/approve
    API->>API: ONE transaction: lock class row, re-check capacity/clash,<br/>assign class, create FeeRecord, history, ApprovalDecision
    API-->>Admin: 200 (or 409 if the class filled up → rollback)
```

Every step and tool call is saved as it happens (`AgentSteps`, `ToolCalls`), so the Admin review page shows the
full trail, including failed attempts and timings.

## 3. Enrolment status life cycle

```mermaid
stateDiagram-v2
    [*] --> Submitted : parent submits
    Submitted --> AgentProcessing : worker starts
    Submitted --> Cancelled : parent cancels
    AgentProcessing --> PendingAdminApproval : proposal ready
    AgentProcessing --> Failed : safe failure
    PendingAdminApproval --> Approved : admin approves (transaction)
    PendingAdminApproval --> Rejected : admin rejects (note)
    PendingAdminApproval --> RevisionRequested : admin asks for changes (note)
    PendingAdminApproval --> Cancelled
    RevisionRequested --> Submitted : parent edits + resubmits (new workflow)
    RevisionRequested --> Cancelled
    Failed --> Submitted : admin retries (new workflow)
    Failed --> Cancelled
    Approved --> Cancelled : frees the seat
    Rejected --> [*]
    Cancelled --> [*]
```

Enforced in code by `EnrolmentStateMachine` (any other move → 409), and every move writes an `EnrolmentStatusHistory` row.

## 4. Deployment

```mermaid
flowchart LR
    DEV["Developer<br/>git push main"] --> GH["GitHub<br/>ThisaraNuwanthi/sef-main-assignment"]
    GH -- "GitHub Actions CI:<br/>backend tests (Postgres service),<br/>web lint/test/build,<br/>Flutter analyze/test" --> GH
    GH -- "auto-deploy<br/>backend/Dockerfile" --> RW["Railway<br/>API container"]
    GH -- "auto-deploy web/" --> VC["Vercel<br/>static React build"]
    RW --> NEON[("Neon PostgreSQL<br/>Singapore")]
    DEV -- "flutter build apk --release<br/>--dart-define=API_BASE_URL" --> APK["Android APK"]
```
