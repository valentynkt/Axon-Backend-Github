# SPARC Phase 1 – Conversation Persistence (Adjusted) — **Final Context Spec**

## 0) Scope & Non-Goals

**Scope (Phase 1)**

* Persist **Conversation** aggregate (and Messages) using EF Core + PostgreSQL
* **Explicit auditing** (CreatedAt/By, UpdatedAt/By) via backing fields; values set in infra
* **Optimistic concurrency** via PG `xmin`/rowversion
* **Domain events captured to Outbox** transactionally; background dispatcher publishes
* **Read models (CQRS)** updated asynchronously by projector hosted service
* **Repositories**:
  * **GenericRepository<TEntity, TId>** for basic CRUD/exists/find
  * **Narrow repositories** (e.g., `ConversationRepository`) inheriting from generic, adding compiled queries/domain methods
* **UoW façade**: orchestrates `SaveChanges`, outbox capture, optional transaction boundaries, **no IQueryable leaks**

**Non-Goals (Phase 1)**

* No global event sourcing for all aggregates. ES only where justified (see §9).
* No cross-module orchestration—only Chat module boundaries.
* No synchronous event publishing within `SaveChanges`.

---

## 1) Abstraction Hierarchy (R1)

**Typed IDs**

```csharp
public readonly record struct ConversationId(Guid Value)
{
    public static ConversationId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}
```

**Base Entity & AggregateRoot**

```csharp
public interface IIdentifiable<out TId> where TId : notnull { TId Id { get; } }

public abstract class BaseEntity<TId> : IIdentifiable<TId>, IEquatable<BaseEntity<TId>>
    where TId : notnull
{
    public TId Id { get; protected set; }

    public override bool Equals(object? obj) =>
        obj is BaseEntity<TId> other && EqualityComparer<TId>.Default.Equals(Id, other.Id) &&
        GetType() == other.GetType();

    public bool Equals(BaseEntity<TId>? other) => Equals((object?)other);
    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}

public interface IAggregateRoot
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}

public abstract class AggregateRoot<TId> : BaseEntity<TId>, IAggregateRoot where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    protected void Raise(IDomainEvent @event) => _domainEvents.Add(@event);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
```

**Auditable marker + explicit audit fields via backing fields**

```csharp
public interface IAuditable { }

public abstract class AuditableEntity<TId> : BaseEntity<TId>, IAuditable where TId : notnull
{
    private DateTime _createdAtUtc;
    private string _createdBy = default!;

    private DateTime _updatedAtUtc;
    private string _updatedBy = default!;

    public DateTime CreatedAtUtc => _createdAtUtc;
    public string CreatedBy => _createdBy;
    public DateTime UpdatedAtUtc => _updatedAtUtc;
    public string UpdatedBy => _updatedBy;

    internal void SetCreated(DateTime atUtc, string by) { _createdAtUtc = atUtc; _createdBy = by; }
    internal void SetUpdated(DateTime atUtc, string by) { _updatedAtUtc = atUtc; _updatedBy = by; }
}
```

---

## 2) EF Core Infrastructure & Auditing (R2)

**DbContext (Chat)**

```csharp
public sealed class ChatDbContext : DbContext
{
    public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options) { }

    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<ConversationReadModel> ConversationReads => Set<ConversationReadModel>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasPostgresExtension("pg_trgm");

        // typed id conversions
        b.Entity<Conversation>().Property(x => x.Id)
            .HasConversion(v => v.Value, v => new ConversationId(v))
            .ValueGeneratedNever();

        // optimistic concurrency via xmin
        b.Entity<Conversation>().Property<uint>("xmin").IsRowVersion();

        // auditing backing fields
        b.Entity<Conversation>().Property<DateTime>("_createdAtUtc").HasColumnName("created_at_utc");
        b.Entity<Conversation>().Property<string>("_createdBy").HasColumnName("created_by");
        b.Entity<Conversation>().Property<DateTime>("_updatedAtUtc").HasColumnName("updated_at_utc");
        b.Entity<Conversation>().Property<string>("_updatedBy").HasColumnName("updated_by");

        // read model FTS/search (example)
        b.Entity<ConversationReadModel>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id)
             .HasConversion(v => v.Value, v => new ConversationId(v))
             .ValueGeneratedNever();

            // audit backing fields for read model
            e.Property<DateTime>("_createdAtUtc").HasColumnName("created_at_utc");
            e.Property<string>("_createdBy").HasColumnName("created_by");
            e.Property<DateTime>("_updatedAtUtc").HasColumnName("updated_at_utc");
            e.Property<string>("_updatedBy").HasColumnName("updated_by");

            e.Property(x => x.Context).HasColumnType("jsonb");
            e.Property(x => x.Tags).HasColumnType("jsonb");
            e.Property(x => x.SearchVector).HasColumnType("tsvector")
             .HasComputedColumnSql(
                "to_tsvector('simple', coalesce(title,'') || ' ' || coalesce(context::text,''))",
                stored: true);
            e.HasIndex(x => x.SearchVector).HasMethod("gin");
            e.HasIndex(x => new { x.IsActive, x.LastMessageAt });
        });

        // outbox
        b.Entity<OutboxMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.OccurredAtUtc);
            e.Property(x => x.ProcessedAtUtc);
            e.HasIndex(x => x.ProcessedAtUtc);
        });
    }
}
```

**Auditing writer (SaveChangesInterceptor)**

```csharp
public sealed class AuditSaveChangesInterceptor(ICurrentUserService users) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        var ctx = eventData.Context;
        if (ctx is null) return base.SavingChanges(eventData, result);

        var now = DateTime.UtcNow;
        var user = users.GetCurrentUserIdOrSystem();

        foreach (var entry in ctx.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
                Set(entry, created: true, now, user);
            if (entry.State == EntityState.Modified)
                Set(entry, created: false, now, user);
        }
        return base.SavingChanges(eventData, result);
    }

    private static void Set(EntityEntry entry, bool created, DateTime at, string by)
    {
        if (created)
        {
            entry.Property("_createdAtUtc").CurrentValue = at;
            entry.Property("_createdBy").CurrentValue = by;
        }
        entry.Property("_updatedAtUtc").CurrentValue = at;
        entry.Property("_updatedBy").CurrentValue = by;
    }
}
```

> **Note:** No event publishing interceptor. Events are captured to **Outbox** in UoW and dispatched by a **background worker**.

---

## 3) Repositories (R3)

**Generic Repository (strict, no IQueryable return, async compiled queries inside)**

```csharp
public interface IGenericRepository<TEntity, TId>
    where TEntity : BaseEntity<TId>
    where TId : notnull
{
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default);
    Task<bool> ExistsAsync(TId id, CancellationToken ct = default);
    void Add(TEntity entity);
    void Update(TEntity entity);
    void Remove(TEntity entity);
}
```

**EF Implementation (base)**

```csharp
public abstract class EfRepositoryBase<TEntity, TId>(ChatDbContext db) : IGenericRepository<TEntity, TId>
    where TEntity : BaseEntity<TId>
    where TId : notnull
{
    public async Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default)
        => await db.Set<TEntity>().FindAsync([id], ct);

    public Task<bool> ExistsAsync(TId id, CancellationToken ct = default) =>
        db.Set<TEntity>().AnyAsync(e => e.Id!.Equals(id), ct);

    public void Add(TEntity entity) => db.Set<TEntity>().Add(entity);
    public void Update(TEntity entity) => db.Set<TEntity>().Update(entity);
    public void Remove(TEntity entity) => db.Set<TEntity>().Remove(entity);
}
```

**Narrow Repository (inherits from generic, adds domain methods & eager graphs)**

```csharp
public interface IConversationRepository : IGenericRepository<Conversation, ConversationId>
{
    Task<Conversation?> GetAggregateAsync(ConversationId id, CancellationToken ct = default);
    Task<IReadOnlyList<Conversation>> GetRecentAsync(int count, CancellationToken ct = default);
}

public sealed class ConversationRepository(ChatDbContext db)
    : EfRepositoryBase<Conversation, ConversationId>(db), IConversationRepository
{
    private static readonly Func<ChatDbContext, ConversationId, Task<Conversation?>> GetAggregate =
        EF.CompileAsyncQuery((ChatDbContext ctx, ConversationId id) =>
            ctx.Conversations
               .Include(c => c.Messages) // order handled in aggregate accessor
               .FirstOrDefault(c => c.Id == id));

    private static readonly Func<ChatDbContext, int, Task<List<Conversation>>> GetRecent =
        EF.CompileAsyncQuery((ChatDbContext ctx, int count) =>
            ctx.Conversations.OrderByDescending(c => EF.Property<DateTime>(c, "_updatedAtUtc"))
                             .Take(count).ToList());

    public Task<Conversation?> GetAggregateAsync(ConversationId id, CancellationToken ct = default)
        => GetAggregate(db, id);

    public async Task<IReadOnlyList<Conversation>> GetRecentAsync(int count, CancellationToken ct = default)
        => await GetRecent(db, count);
}
```

> **Query side:** Use `DbContext` directly or Dapper for read models. **Do not** expose `IQueryable` from repositories.

---

## 4) Unit of Work Façade (R4)

**Purpose:** Orchestrate **transaction boundaries**, **persist aggregates**, **capture domain events to Outbox**, commit; **no re-implementation of DbContext**.

```csharp
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);       // no transaction guarantee
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default);
}

public sealed class EfUnitOfWork(ChatDbContext db, IEventSerializer serializer) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await action(ct);

        // Capture domain events from tracked aggregates to Outbox
        var aggregates = db.ChangeTracker.Entries()
            .Where(e => e.State != EntityState.Detached && e.Entity is IAggregateRoot)
            .Select(e => (IAggregateRoot)e.Entity)
            .ToList();

        var now = DateTime.UtcNow;
        foreach (var aggregate in aggregates)
        {
            foreach (var ev in aggregate.DomainEvents)
            {
                var metadata = serializer.BuildMetadata(ev);
                db.Outbox.Add(OutboxMessage.Create(
                    type: ev.GetType().FullName!,
                    payload: serializer.Serialize(ev),
                    metadata: metadata,
                    occurredAtUtc: now));
            }
            aggregate.ClearDomainEvents();
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
```

**Command Handler Pattern**

```csharp
public sealed class AddMessageHandler(IConversationRepository repo, IUnitOfWork uow) 
    : IRequestHandler<AddMessage, Result>
{
    public async Task<Result> Handle(AddMessage cmd, CancellationToken ct)
    {
        await uow.ExecuteInTransactionAsync(async _ =>
        {
            var conv = await repo.GetAggregateAsync(cmd.ConversationId, ct)
                       ?? throw new NotFoundException(nameof(Conversation), cmd.ConversationId);

            conv.AddMessage(cmd.Role, cmd.Content, cmd.Metadata); // raises domain events
            repo.Update(conv);
            await Task.CompletedTask;
        }, ct);

        return Result.Success();
    }
}
```

---

## 5) Outbox & Dispatcher

**Schema**

```csharp
public sealed class OutboxMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Type { get; init; } = default!;
    public string Payload { get; init; } = default!;
    public string Metadata { get; init; } = default!;
    public DateTime OccurredAtUtc { get; init; }
    public DateTime? ProcessedAtUtc { get; set; }

    public static OutboxMessage Create(string type, string payload, string metadata, DateTime occurredAtUtc)
        => new() { Type = type, Payload = payload, Metadata = metadata, OccurredAtUtc = occurredAtUtc };
}
```

**Background dispatcher (HostedService)**

```csharp
public sealed class OutboxDispatcherService(ChatDbContext db, IServiceProvider sp, ILogger<OutboxDispatcherService> logger) 
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing outbox messages");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        
        var batch = await db.Outbox.FromSqlInterpolated($@"
            SELECT * FROM ""OutboxMessages""
            WHERE ""ProcessedAtUtc"" IS NULL
            ORDER BY ""OccurredAtUtc""
            FOR UPDATE SKIP LOCKED
            LIMIT {10}
        ").ToListAsync(ct);

        if (!batch.Any()) return;

        using var scope = sp.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var serializer = scope.ServiceProvider.GetRequiredService<IEventSerializer>();

        foreach (var message in batch)
        {
            try
            {
                var domainEvent = serializer.Deserialize(message.Type, message.Payload);
                await mediator.Publish(domainEvent, ct);
                message.ProcessedAtUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process outbox message {MessageId}", message.Id);
                // Could implement retry logic with exponential backoff here
            }
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
```

* Pull N unprocessed rows with `FOR UPDATE SKIP LOCKED`
* Deserialize → publish (MediatR or bus) → set `ProcessedAtUtc`
* Retry with exponential backoff on failures; log with Activity/ILogger

---

## 6) Read Models (CQRS) & Projection Host

**Read Model (example)**

```csharp
public sealed class ConversationReadModel : AuditableEntity<ConversationId>
{
    public string Title { get; private set; } = string.Empty;
    public int MessageCount { get; private set; }
    public int ToolExecutionCount { get; private set; }
    public DateTime LastMessageAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public bool IsActive { get; private set; }
    public string Context { get; private set; } = string.Empty; // jsonb
    public string Tags { get; private set; } = string.Empty;    // jsonb
    public string SearchVector { get; private set; } = string.Empty; // tsvector (computed)
}
```

**Projection**

* Subscribe to **outbox** events
* Idempotent updates via **UPSERT** (`ON CONFLICT (id) DO UPDATE`)
* Keep a **checkpoint** if you process streams elsewhere

---

## 7) Concurrency & Idempotency

* **Optimistic concurrency:** PG `xmin` configured as rowversion
* **Idempotency:** Commands carry **IdempotencyKey**; store processed keys per aggregate or module; reject duplicates
* **Expected versions (optional):** For aggregates prone to contention, include expected version in command → verify against `xmin`

---

## 8) Observability

* **Microsoft.Extensions.Logging** + **Activity (W3C)** + **App Insights** exporter
* Correlate: inject `CorrelationId`, `CausationId` into **Outbox.Metadata**
* Minimal logs on hot paths; structured logging on state transitions

---

## 9) Event Sourcing (R5) — "needed where it's needed"

**Policy**

* Use ES **only** if you require: temporal queries, audit by reconstruction, or high-contention collaborative workflows.
* For Phase 1 conversations: default **state store + outbox + projections**.
* If enabling ES for Conversations:
  * **Event entity** (append-only), **type registry**, **upcasters**, **expected version**
  * **Snapshot** every N events or size threshold; include **schema hash** to invalidate
  * Keep ES storage **separate table**; rehydrate aggregate in repository when ES is enabled

**Minimal ES interfaces (parked for when you flip it on)**

```csharp
public interface IEventStore
{
    Task AppendAsync<TId>(TId streamId, IEnumerable<IDomainEvent> events, int expectedVersion, CancellationToken ct);
    Task<IReadOnlyList<IDomainEvent>> LoadAsync<TId>(TId streamId, int fromVersion = 0, CancellationToken ct = default);
}
```

---

## 10) DI & Configuration (Module)

```csharp
public static class ChatModuleRegistration
{
    public static IServiceCollection AddChatModule(this IServiceCollection s, IConfiguration cfg)
    {
        s.AddScoped<AuditSaveChangesInterceptor>();
        
        s.AddDbContext<ChatDbContext>((sp, o) =>
            o.UseNpgsql(cfg.GetConnectionString("ChatDb"))
             .UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
             .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>()));

        s.AddScoped<IConversationRepository, ConversationRepository>();
        s.AddScoped<IUnitOfWork, EfUnitOfWork>();
        s.AddScoped<ICurrentUserService, HttpCurrentUserService>();
        s.AddSingleton<IEventSerializer, SystemTextJsonEventSerializer>();

        s.AddHostedService<OutboxDispatcherService>();
        s.AddHostedService<ReadModelProjectionService>();

        return s;
    }
}
```

---

## 11) MediatR Pipeline (App Layer)

Recommended order:

1. **ValidationBehavior**
2. **TimeoutBehavior** (per request budget)
3. **TransactionBehavior** (wraps `IUnitOfWork.ExecuteInTransactionAsync` when needed)
4. **CachingBehavior** (queries only)
5. **ObservabilityBehavior** (Activity + minimal logs)
6. **ExceptionMappingBehavior** (domain → problem details)

---

## 12) Testing Plan

* **Unit:** Aggregate invariants (no EF)
* **Integration:**
  * EF + Postgres via **Testcontainers**
  * UoW transaction → outbox capture
  * Dispatcher delivers → read model updates (end-to-end)
* **Contract:** Event payload schema snapshots; serializer round-trip
* **Load:** Compiled query hotspots; projection throughput

---

## 13) Success Criteria (Phase 1)

* [ ] Conversations persisted with typed IDs, explicit audit stamps set by interceptor
* [ ] `xmin` concurrency active; optimistic conflicts tested
* [ ] Domain events captured to **Outbox** within transaction; cleared on aggregates
* [ ] Background **OutboxDispatcher** publishes reliably (at-least-once)
* [ ] Read models updated asynchronously; queries use read DB/sets
* [ ] **Generic repo** exists but **narrow repos** implement domain-specific access with compiled queries
* [ ] UoW façade in place; **no IQueryable leaks**; no EF re-implementation
* [ ] ES interfaces ready; not forced globally

---

## 14) Migration Notes (PG)

* Enable extensions as needed: `CREATE EXTENSION IF NOT EXISTS pg_trgm;`
* Add GIN index for tsvector; partial indexes for hot filters `(is_active, last_message_at DESC)`
* Timestamps in UTC; ensure server `timezone = 'UTC'`

---

## 15) Risks & Mitigations

* **Risk:** Overusing generic repo → loss of EF strengths
  **Mitigation:** Keep it thin; move logic to **narrow repos** with compiled queries.

* **Risk:** Event dispatch in-tx → deadlocks/duplication
  **Mitigation:** Outbox with background dispatcher; idempotent handlers.

* **Risk:** Shadow-only audit obscurity
  **Mitigation:** **Explicit audit** fields (backing) + tests.

---

## 16) Idempotency & Metadata Contracts

**Idempotency Store**

```csharp
public sealed class IdempotencyRecord
{
    public string Key { get; init; } = default!; // e.g., GUID from client
    public DateTime CreatedAtUtc { get; init; }
    public string? ResultHash { get; init; } // optional for result caching
    public string AggregateType { get; init; } = default!;
    public string AggregateId { get; init; } = default!;
}

// Usage in pipeline: check key exists → short-circuit; else insert before execution in same tx
public interface ITransactionalRequest { }
public sealed record AddMessage : ITransactionalRequest, IRequest<Result> { /* ... */ }
```

**Outbox Metadata Contract**

```csharp
public sealed record OutboxMetadata(
    string CorrelationId,
    string? CausationId,
    string AggregateType,
    string AggregateId,
    string EventType,
    int? EventVersion,
    string UserId,
    DateTime OccurredAtUtc);

public interface IEventSerializer
{
    string Serialize(IDomainEvent @event);
    IDomainEvent Deserialize(string type, string payload);
    string BuildMetadata(IDomainEvent @event);
}
```

**TransactionBehavior Pipeline**

```csharp
public sealed class TransactionBehavior<TRequest, TResponse>(IUnitOfWork uow) 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ITransactionalRequest
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        TResponse response = default!;
        await uow.ExecuteInTransactionAsync(async _ =>
        {
            response = await next();
        }, ct);
        return response;
    }
}
```

---

## 17) Alignment with Existing Codebase

**Domain Events Integration**
* Leverages existing `IDomainEvent` interface from `src/Shared/Domain/IDomainEvent.cs`
* Builds on `DomainEvent` base record from `src/Shared/Domain/DomainEvent.cs`
* Extends existing `MessageAddedDomainEvent` pattern from Chat module

**Chat Module Boundaries**
* Maintains existing Chat module structure under `src/Modules/Chat/`
* Preserves Domain/Application/Infrastructure layering
* Integrates with existing ProcessMessage endpoint patterns

**Clean Architecture Compliance**
* Domain layer remains dependency-free with pure aggregates
* Infrastructure layer handles all EF and persistence concerns
* Application layer coordinates through MediatR handlers
* API layer unchanged, continues using FastEndpoints

---

# Phase 1 Specification Complete

This specification serves as the canonical source of truth for the conversation persistence implementation. All subsequent phases (Pseudocode, Architecture, Refinement, Completion) will be based on these requirements.

**Key Differentiators from Previous Version:**
- **Generic + Narrow Repository Pattern**: Inheritance-based approach maintaining EF strengths
- **Minimal UoW Façade**: Orchestrates transactions without re-implementing DbContext
- **Scoped Event Sourcing**: Available when needed, not forced globally
- **Explicit Audit Fields**: Backing field approach with interceptor population
- **Outbox + Background Dispatcher**: Reliable event publishing without in-transaction complexity

**Next Phase**: Proceed to Phase 2 (Pseudocode) for detailed algorithmic design based on this adjusted specification.