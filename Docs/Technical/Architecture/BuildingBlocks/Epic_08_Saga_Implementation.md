# Epic 08: Saga Pattern — Architecture & Implementation Guide (v2)

## Why this matters (short version)

We need a **reliable way to coordinate long-running, cross-service workflows** (funding → KYC → wallet provisioning → trade settlement → notifications, etc.). Sagas give us:

* **Atomicity by composition** (forward steps + compensations).
* **Idempotency + recovery** across crashes/restarts.
* **Clear observability** of every step and rollback.
* **Pluggable orchestration** (orchestrated, choreographed, or hybrid).

Below is a **state-of-the-art, production-grade blueprint** aligned with our CQRS/Result<T>/Outbox-Inbox stack and OpenTelemetry.

---

# 1) Scope & Outcomes

## Acceptance Criteria (refined)

* [ ] **Abstractions**: `ISaga<TState>`, `ISagaState`, `ISagaStep<TState>`, `ISagaCompensation<TState>`, `ISagaSerializer`, `ISagaClock`.
* [ ] **Execution Engine**: deterministic step runner, idempotent replays, retries, timeouts, compensation.
* [ ] **Persistence**: versioned state, optimistic concurrency, snapshots, encryption for sensitive fields.
* [ ] **Coordination**: Orchestrated engine + hooks for choreographed/hybrid via the event bus (Outbox/Inbox).
* [ ] **Error Model**: explicit retryable/non-retryable error taxonomy; poison handling.
* [ ] **Observability**: OTel tracing & metrics, structured logs, correlation IDs.
* [ ] **Ops**: pause/resume, manual compensate, dead-letter, requeue, TTL/archival.
* [ ] **Performance**: horizontal scaling, partitioning, sharding by `SagaType/CorrelationId`.

## Business Value

* Cuts failure blast radius; **automatic compensation** prevents stranded funds/states.
* **Faster recovery** (resume from last good step with at-least-once semantics).
* **Auditability** (who/what/when for each step and compensation).
* **Pluggable** across bounded contexts without tight coupling.

---

# 2) Architecture Overview

```
Client / Command
    └─► Orchestrator (SagaManager)
           ├─ SagaExecutionEngine
           │    ├─ StepRunner (Execute / Compensate)
           │    ├─ Retry & Timeout policy
           │    └─ Idempotency (step invocation keys)
           ├─ SagaStateRepository (EF/SQL, versioned JSON)
           ├─ Outbox (events) / Inbox (dedupe)
           └─ Scheduler (timeouts/heartbeats)
```

**Orchestrated Sagas**: centralized coordinator drives steps.
**Choreographed Sagas**: decentralized, event-driven steps listen to domain events.
**Hybrid**: orchestrator for critical sections + events for peripheral steps.

---

# 3) Domain Model

## 3.1 Contracts (minimal but expressive)

```csharp
// Abstractions/Sagas/ISagaState.cs
public interface ISagaState : ICloneable
{
    Guid SagaId { get; }
    string SagaType { get; }
    string CorrelationId { get; }
    SagaStatus Status { get; }              // Pending, Running, Compensating, Completed, Failed, TimedOut, Cancelled
    int Version { get; }                    // optimistic concurrency
    DateTime CreatedAt { get; }
    DateTime UpdatedAt { get; }
    DateTime? ExpiresAt { get; }

    string? CurrentStepId { get; }
    int StepsCompleted { get; }
    IReadOnlyList<string> ForwardHistory { get; }      // step ids in execution order
    IReadOnlyList<string> CompensationHistory { get; } // step ids compensated

    IReadOnlyDictionary<string, object> Bag { get; }   // safe, serializable scratchpad
}

// Abstractions/Sagas/ISaga.cs
public interface ISaga<TState> where TState : ISagaState
{
    string SagaType { get; }
    IReadOnlyList<ISagaStep<TState>> Steps { get; }        // forward steps
    TimeSpan? DefaultStepTimeout { get; }
    TimeSpan? SagaTtl { get; }
}

// Abstractions/Sagas/ISagaStep.cs
public interface ISagaStep<TState> where TState : ISagaState
{
    string StepId { get; }
    bool IsIdempotent { get; }             // external call must be idempotent
    bool IsCompensable { get; }            // must provide compensation if true
    TimeSpan? Timeout { get; }             // overrides default
    RetryPlan Retry { get; }               // retries & backoff
    Task<StepResult> ExecuteAsync(TState state, CancellationToken ct);
    Task<StepResult> CompensateAsync(TState state, CancellationToken ct);
}

public enum StepOutcome { Success, TransientFailure, PermanentFailure, NoOp }
public sealed record StepResult(StepOutcome Outcome, string? ErrorCode = null, string? ErrorMessage = null, object? Data = null);

public sealed record RetryPlan(int MaxAttempts = 3, TimeSpan? InitialDelay = null, TimeSpan? MaxDelay = null, bool Jitter = true);
```

**Notes**

* `Bag` stores transient, serializable details (e.g., external IDs) for later steps/compensation.
* A step returns `TransientFailure` to trigger retry (policies apply). `PermanentFailure` ends forward path and enters compensation.

## 3.2 Status Model

```
Pending → Running → Completed
     ↘          ↘
     Failed      Compensating → Compensated|Failed
     ↘
     TimedOut
```

* **Failed** (forward error): run compensation in reverse order.
* **TimedOut**: treat as failure with compensation; engine may retry if step policy allows.
* **Cancelled**: operator-triggered stop; can choose to compensate or hold.

---

# 4) Persistence & Idempotency

## 4.1 Repository & Schema

**Repository (interface)**

```csharp
public interface ISagaStateRepository
{
    Task<TState?> GetAsync<TState>(Guid sagaId, CancellationToken ct) where TState : class, ISagaState;
    Task UpsertAsync<TState>(TState state, CancellationToken ct) where TState : class, ISagaState; // optimistic concurrency
    Task<bool> TryUpdateAsync<TState>(TState state, int expectedVersion, CancellationToken ct) where TState : class, ISagaState;
    Task MarkExpiredAsync(Guid sagaId, DateTime expiresAt, CancellationToken ct);
    Task ArchiveAsync(Guid sagaId, CancellationToken ct);
}
```

**SQL (example)**

```sql
CREATE TABLE SagaStates (
  Id UNIQUEIDENTIFIER PRIMARY KEY,
  SagaType NVARCHAR(128) NOT NULL,
  CorrelationId NVARCHAR(255) NOT NULL,
  Status NVARCHAR(40) NOT NULL,
  Version INT NOT NULL,
  CurrentStepId NVARCHAR(128) NULL,
  StepsCompleted INT NOT NULL,
  ForwardHistory NVARCHAR(MAX) NOT NULL,        -- JSON string array
  CompensationHistory NVARCHAR(MAX) NOT NULL,   -- JSON string array
  Bag NVARCHAR(MAX) NULL,                       -- JSON map
  CreatedAt DATETIME2 NOT NULL,
  UpdatedAt DATETIME2 NOT NULL,
  ExpiresAt DATETIME2 NULL,
  RowVersion ROWVERSION                         -- or use EF Core concurrency token
);
CREATE INDEX IX_SagaStates_SagaType_Correlation ON SagaStates (SagaType, CorrelationId);
```

* **Optimistic concurrency** via `RowVersion` or `Version`.
* **Snapshots** optional: snapshot table keyed by `(SagaId, Version)` for rewind/debug.

## 4.2 Idempotency

* **Step Idempotency Key** = `${SagaId}:${StepId}:${Attempt}` (or just `${SagaId}:${StepId}` if external call is fully idempotent).
* Require **idempotent external APIs** (e.g., pass the key to MoonPay/Helius/Jupiter where supported).
* **Inbox/Outbox**: event deliveries de-duplicated by `(MessageId, Consumer)`.

---

# 5) Orchestration Engine

## 5.1 Manager & Engine

```csharp
public interface ISagaManager
{
    Task<Guid> StartAsync<TState>(ISaga<TState> saga, TState initial, CancellationToken ct) where TState : ISagaState;
    Task ResumeAsync(Guid sagaId, CancellationToken ct);
    Task CompensateAsync(Guid sagaId, CancellationToken ct);
    Task CancelAsync(Guid sagaId, bool compensate, CancellationToken ct);
}

public interface ISagaExecutionEngine
{
    Task RunForwardAsync<TState>(ISaga<TState> saga, TState state, CancellationToken ct) where TState : ISagaState;
    Task RunCompensationAsync<TState>(ISaga<TState> saga, TState state, CancellationToken ct) where TState : ISagaState;
}
```

### Forward Execution (deterministic)

1. Load state (check `Version`).
2. Pick next step by `StepsCompleted`.
3. Run **StepRunner** with retry/timeout.
4. Persist state (increment `Version`, append `ForwardHistory`).
5. Repeat until done or failure → compensation.

### Compensation

* Reverse iterate `ForwardHistory`.
* For each compensable step: execute `CompensateAsync` with its own retry/timeout.
* Record `CompensationHistory`, persist `Version`.
* Stop when fully compensated or we hit a **compensation failure** → raise alert + leave saga in `Failed` (compensating) with manual action required.

## 5.2 Timeouts & Scheduling

* **Per-step timeout** (uses `CancellationTokenSource` linked to request CT + timeout).
* **Heartbeat/Lease** rows: `UpdatedAt` acts as heartbeat; background **Reaper** resumes stuck sagas.
* **Scheduler**: durable queue (e.g., table + worker) for resuming at `NextAttemptAt`.

---

# 6) Choreography & Hybrid

* **Choreographed**: register event handlers that:

    * Validate **correlation** (`CorrelationId`, `CausationId`).
    * Load saga state, apply transitions, persist.
    * Publish next intent/event via Outbox.
* **Hybrid**: use orchestrator for critical steps (payments/trades), use events for UI notifications/integrations.

**Ordering**: rely on per-key partitioning (e.g., Kafka topic by `CorrelationId`) or logical sequence numbers in state.
**Delivery**: at-least-once with **Inbox de-dup**.

---

# 7) Error Handling & Retry Policy

### Error Taxonomy

* **Transient**: timeouts, 5xx, network errors → retry (exponential + jitter).
* **Permanent**: 4xx semantic errors, validation → **no retry**, go to compensation.
* **External Circuit Open**: short-circuit; set retry later (with backoff cap).

### Poison Protection

* Cap **MaxAttempts** (per step). Move to **dead-letter** with full context after cap; alert SRE.

---

# 8) Observability (OTel)

**Tracing**

* Activity name: `saga.{SagaType}` and `saga.step.{StepId}`.
* Tags:

    * `saga.id`, `saga.type`, `saga.correlation_id`, `saga.status`
    * `saga.step.id`, `saga.step.outcome`, `saga.step.attempt`
    * `error.code`, `error.message` (bounded length)

**Metrics**

* `axon.saga.started.total` (Counter) — tags: `saga.type`
* `axon.saga.completed.total` (Counter) — tags: `saga.type`
* `axon.saga.failed.total` (Counter) — tags: `saga.type`
* `axon.saga.compensated.total` (Counter) — tags: `saga.type`
* `axon.saga.step.duration.ms` (Histogram) — tags: `saga.type`, `step.id`
* `axon.saga.retry.attempts` (Counter) — tags: `saga.type`, `step.id`
* `axon.saga.inflight` (UpDownCounter) — tags: `saga.type`

**Logging**

* Structured, include `sagaId`, `correlationId`, `stepId`, `attempt`, `version`.
* **No PII**. Use our `ISensitiveDataMasker` for payloads.

---

# 9) Security & Compliance

* **Encrypt** sensitive fields in `Bag` (field-level encryption or envelope encryption).
* **Scrub** PII in telemetry via masker.
* **RBAC** for Admin actions (pause/resume/compensate).
* **Audit trail**: append-only log of forward/compensation steps with timestamps and operator actions.

---

# 10) File Structure (concrete)

```
src/BuildingBlocks/Application/
├── Abstractions/Sagas/
│   ├── ISaga.cs
│   ├── ISagaState.cs
│   ├── ISagaStep.cs
│   ├── ISagaManager.cs
│   ├── ISagaExecutionEngine.cs
│   ├── ISagaStateRepository.cs
│   ├── ISagaSerializer.cs
│   ├── ISagaClock.cs
│   └── SagaStatus.cs
├── Sagas/
│   ├── SagaManager.cs
│   ├── SagaExecutionEngine.cs
│   ├── StepRunner.cs
│   ├── Policies/
│   │   ├── RetryPlan.cs
│   │   └── TimeoutPolicy.cs
│   ├── Scheduling/
│   │   ├── SagaScheduler.cs
│   │   └── SagaReaper.cs
│   ├── Observability/SagaMetrics.cs
│   └── Orchestration/
│       ├── OrchestratedSagaHost.cs
│       └── ChoreographyHandlers.cs
└── Infrastructure/Sagas/
    ├── EfSagaStateRepository.cs
    ├── JsonSagaSerializer.cs
    ├── Encryption/EncryptedBagConverter.cs
    ├── Mappings/SagaStateEntity.cs
    └── Admin/SagaAdminService.cs
```

---

# 11) DI & Configuration

```csharp
services.AddScoped<ISagaStateRepository, EfSagaStateRepository>();
services.AddScoped<ISagaExecutionEngine, SagaExecutionEngine>();
services.AddScoped<ISagaManager, SagaManager>();
services.AddSingleton<ISagaSerializer, JsonSagaSerializer>();
services.AddSingleton<ISagaClock, SystemClock>();

// Background workers
services.AddHostedService<SagaReaper>();      // resumes stuck/expired
services.AddHostedService<SagaScheduler>();   // schedules retries/resumes

// Options (from config)
services.Configure<SagaOptions>(cfg => {
    cfg.DefaultStepTimeout = TimeSpan.FromSeconds(30);
    cfg.MaxParallelSagasPerType = 32;
    cfg.ArchiveAfter = TimeSpan.FromDays(30);
});
```

---

# 12) Example: Orchestrated Trading Saga (sketch)

Steps:

1. **ReserveFunds** (wallet),
2. **RouteTrade** (Jupiter),
3. **ExecuteSwap** (Helius),
4. **ConfirmSettlement** (on-chain check),
5. **NotifyUser**.

Compensations:

* `ExecuteSwap` ⇨ refund/reverse (best-effort)
* `RouteTrade` ⇨ no-op (idempotent)
* `ReserveFunds` ⇨ release hold

Each step uses an **idempotency key** `${SagaId}:${StepId}` passed downstream. Failures at steps 3–4 trigger compensation (reverse order).

---

# 13) Testing Strategy (tight)

* **Unit**: step logic, compensation, repository concurrency, retry math.
* **Integration**: end-to-end saga happy path + failure paths (transient & permanent) with Outbox/Inbox.
* **Chaos**: kill orchestrator mid-step; verify resume.
* **Performance**: parallel 1k sagas; measure p95 step duration & DB contention.
* **Data**: snapshot + restore; verify deterministic replays.

---

# 14) Risks & Mitigations

* **Compensation gaps** → enforce `IsCompensable` for any step with external side effects; PR checklist.
* **Idempotency drift** → formal contract with external providers; e2e tests verifying key replays.
* **Hot rows** → partition by `SagaType`; shard IDs; backoff on concurrency failures.
* **Poison loops** → cap retries; dead-letter + alert.
* **Observability noise** → cap tag cardinality; truncate error messages.

---

# 15) Definition of Done (final)

* Abstractions, engine, repository implemented with tests.
* Orchestrated saga working; sample Trading Saga runs through all paths.
* Choreography handlers integrated with event bus & Inbox/Outbox.
* OTel dashboards: success/failed/compensated counts, step durations.
* Admin APIs: get status, pause/resume, manual compensate/cancel.
* Docs: “How to author a saga” with templates and checklists.

---

If you want, I can follow up with:

* The **exact C# interfaces/classes** for `ISagaStateRepository`, `SagaExecutionEngine`, and `StepRunner`.
* A **reference TradingSaga** skeleton you can drop into the repo to validate the flow end-to-end.
