# 🏗️ BuildingBlocks Infrastructure Layer - Complete Refactoring Implementation Guide

**Version**: 1.0 - Brutal Refactoring Edition  
**Date**: January 2025  
**Scope**: Complete Infrastructure Layer Replacement for MVP  
**Approach**: Clean Slate with New Database - No Migration Required

---

## Executive Summary

This document provides a **lean, step-by-step implementation guide** for refactoring the BuildingBlocks Infrastructure folder. Following the brutal refactoring philosophy from the PRD v3.1, we will:

- ✅ **Eliminate over-engineering** - Remove custom wrappers that duplicate .NET functionality
- ✅ **Use standard libraries** - OpenTelemetry, Polly, built-in options validation
- ✅ **Minimal abstractions** - Only abstract what adds genuine business value
- ✅ **New PostgreSQL database** - no migration complexity
- ✅ **Production-ready patterns** - using proven, vendor-supported solutions
- ✅ **Functional programming** throughout with Result<T> pattern

---

## Table of Contents

1. [Current State Analysis](#1-current-state-analysis)
2. [Target Architecture](#2-target-architecture)
3. [Implementation Phases](#3-implementation-phases)
4. [Phase 1: Core Infrastructure Foundation](#4-phase-1-core-infrastructure-foundation)
5. [Phase 2: Persistence Layer](#5-phase-2-persistence-layer)
6. [Phase 3: Messaging & Events](#6-phase-3-messaging--events)
7. [Phase 4: Observability & Resilience](#7-phase-4-observability--resilience)
8. [Phase 5: Caching & Performance](#8-phase-5-caching--performance)
9. [File-by-File Refactoring Guide](#9-file-by-file-refactoring-guide)
10. [Verification & Testing](#10-verification--testing)

---

## 🚨 5 "Fix-These-First" Adjustments for the Infrastructure Layer

| #   | Issue                                                        | Why it's risky / wasted effort                                                                              | Minimal fix                                                                                                                                                                                                                                                                                                              |
| --- | ------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 1   | Custom correlation accessor (`ICorrelationContextAccessor`) | Duplicates OTel's W3C propagation ⇒ double bookkeeping, missing hops, harder vendor-interop.               | Delete accessor + `CorrelationContext`. Wherever needed read `Activity.Current?.TraceId` / `SpanId`; persist the `traceparent` header in Outbox. Update `AuditInterceptor`, DI registrations, tests.                                                                                                                   |
| 2   | Hand-rolled `GetOptionsResult<T>` + Result-wrapped validation | Re-codes what `OptionsBuilder.Validate*()` gives for free; hides failure reasons from ASP.NET options diagnostics. | Swap for:<br>`csharp\nservices.AddOptions<FooOptions>()\n .Bind(config.GetSection("Foo"))\n .ValidateDataAnnotations();`<br>Remove the extension + Result noise.                                                                                                                                                      |
| 3   | Custom `IMetrics` abstraction & counters                    | Splits metrics between OTel and home-grown; no automatic export, no standard tools.                         | Drop `IMetrics`, inject `Meter` (from OTel) where counters are needed:<br>`csharp\nprivate readonly Counter<long> _cacheHit;\n_cacheHit = meter.CreateCounter<long>("cache.hit");`<br>Remove `CustomMetrics` class and all `.IncrementCounter(...)` calls.                                                         |
| 4   | Bespoke `CircuitBreakerPolicy` wrapper around Polly        | Masks Polly diagnostics, breaks OTel auto-instrumentation, extra serialization gymnastics.                 | Remove wrapper & interface. Register Polly directly on clients:<br>`csharp\nservices.AddHttpClient("external")\n .AddTransientHttpErrorPolicy(p => p.CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));`                                                                                                              |
| 5   | Over-engineered `MultiLevelCache` (tags, metrics, `Option<Result<T>>` layers) | 300+ LOC for what 6 lines of read-through logic do; tag eviction isn't implemented.                        | Keep the interface but rewrite impl to a thin wrapper: memory → distributed, no custom tags, no internal metrics (use OTel). Cache the `Result` object if callers expect functional types.                                                                                                                              |

---

## 1. Current State Analysis

### 🔍 Key Over-engineering Findings & Concrete Adjustments

| #  | Theme                                 | Current Custom Piece                                                                                                                                | Why It's Redundant / Risky                                                                                                                                                                                                              | Lean-er Replacement                                                                                                                                                                                                                                                                                                            |
| -- | ------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 1  | **Correlation / Trace context**       | `ICorrelationContextAccessor`, `CorrelationContext`, explicit headers in `OutboxMessage`, constructor injections across Audit / Outbox interceptors | OpenTelemetry (OTel) already propagates W3C `traceparent`/`tracestate` via `Activity.Current`, `Baggage` and the built-in `W3CTraceContextPropagator`. Maintaining a parallel ambient context → double bookkeeping, drift, missed hops. | **Delete** the whole accessor package. Wherever it's read:<br>`csharp\nvar traceId = Activity.Current?.TraceId.ToString();`<br>and rely on `Activity.Current?.SpanId` / `Activity.Current?.Context` for causation-id. Outbox headers: persist `traceparent` & `tracestate` only.                                               |
| 2  | **Options validation**                | Hand-rolled `GetOptionsResult<T>` + functional errors                                                                                               | `Microsoft.Extensions.Options` has `ValidateDataAnnotations()` / `Validate(...)` fluent helpers.                                                                                                                                        | Replace with:<br>`csharp\nservices.AddOptions<ObservabilityOptions>()\n        .Bind(config.GetSection(\"Observability\"))\n        .ValidateDataAnnotations();`<br>and drop the extension + Result-wrapping.                                                                                                                  |
| 3  | **Metrics abstraction**               | Custom `IMetrics` + `CustomMetrics` + manual counters in cache / CB code                                                                            | OTel Metrics SDK (preview → stable in .NET 8) supplies `Meter` & `Counter<T>`. Duplicating hides data from the pipeline.                                                                                                                | Remove `IMetrics`; inject `Meter meter` from `MeterProvider`. Create counters once:<br>`csharp\nprivate readonly Counter<long> _cacheHit;\n_cacheHit = meter.CreateCounter<long>(\"cache.hit\");`                                                                                                                              |
| 4  | **Circuit-Breaker wrapper**           | `ICircuitBreakerPolicy` + bespoke `CircuitBreakerPolicy` that internally *re-wraps* Polly                                                           | Polly already *is* the CB. Wrapping steals diagnostics (Polly v8 exposes events + OTel).                                                                                                                                                | Delete wrapper. Register: <br>`csharp\nservices.AddHttpClient(\"external\")\n        .AddPolicyHandler(Policy<HttpResponseMessage>\n            .HandleTransientHttpError()\n            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));\n`                                                                                |
| 5  | **Retry / Timeout wrappers**          | Planned `RetryPolicy`, `RetryBehavior`, etc.                                                                                                        | Same as (4): Polly (or .NET 8 `RateLimiting`/`TimeoutPolicy`) does it better and already exports OTel events.                                                                                                                           | Use Polly pipeline on the *infrastructure* client (DB, MQ, HTTP). Remove custom retry logic from behaviors.                                                                                                                                                                                                                    |
| 6  | **Custom multi-level cache**          | `IMultiLevelCache`, own tag-aware options                                                                                                           | .NET already gives `IMemoryCache` + `IDistributedCache`. Multi-level pattern is valid, but 90 % of the class duplicates simple `TryGetValue/SetAsync`.                                                                                  | Keep **interface** if business code wants abstraction, but replace impl with <br>`csharp\npublic class TwoLevelCache : IMultiLevelCache {\n  private readonly IMemoryCache _mem;\n  private readonly IDistributedCache _dist;\n  // 6 lines: read-through then SetAsync.\n}\n`<br>Drop tag list & metrics counters (use OTel). |
| 7  | **DateTime provider**                 | `IDateTimeProvider` + mutable `SystemDateTimeProvider`                                                                                              | Value in tests; fine to keep. **No change**.                                                                                                                                                                                            |                                                                                                                                                                                                                                                                                                                                |
| 8  | **EF Core interceptors**              | `AuditInterceptor` uses custom correlation accessor                                                                                                 | After #1 remove ctor arg; record `Activity.Current?.TraceId`.                                                                                                                                                                           |                                                                                                                                                                                                                                                                                                                                |
| 9  | **UnitOfWork extras**                 | Missing `HasActiveTransaction`, bad return types                                                                                                    | Align with Application fix set:<br>`csharp\nTask<Result<IDbContextTransaction>> BeginTransactionAsync(...)\nbool HasActiveTransaction {get;}\n`                                                                                         |                                                                                                                                                                                                                                                                                                                                |
| 10 | **Configuration of Redis/Resilience** | Manual retry/circuit config in `OnConfiguring`                                                                                                      | EF Core 8 has `EnableRetryOnFailure()` (already used) + `ExecutionStrategy`. Leave as-is but remove extra Polly wrapper.                                                                                                                |                                                                                                                                                                                                                                                                                                                                |

### 🗄️ Files/Sections to Modify or Delete

1. **Delete**

   * `Infrastructure/Core/Abstractions/ICorrelationContextAccessor*.cs`
   * `CorrelationContext` record
   * `IMetrics`, `CustomMetrics`
   * `Resilience/CircuitBreaker/CircuitBreakerPolicy*.cs` + its interface
   * Any `RetryPolicy`, `RetryBehavior`, custom metric counters in code snippets

2. **Refactor**

   * `ServiceCollectionExtensions.cs` – remove registrations for accessor/metrics; replace Options validation per #2.
   * `AxonDbContext.cs` – update `AuditInterceptor` ctor, drop `_correlationContext`.
   * `OutboxMessage.Create` – write `Activity.Current?.Id` into headers instead of custom IDs.
   * `MultiLevelCache.cs` – shrink to thin two-level wrapper; replace `_metrics.IncrementCounter` calls with OTel `Counter<long>`.
   * `IUnitOfWork` + `EfUnitOfWork` – add `HasActiveTransaction`, change `BeginTransactionAsync` signature.

3. **Add / Update DI**

   ```csharp
   // Observability
   services.AddOpenTelemetry()
           .WithMetrics(m => m.AddMeter("BuildingBlocks.*"));

   // Polly integration examples
   services.AddHttpClient("external")
           .AddPolicyHandler(Policy<HttpResponseMessage>
               .HandleTransientHttpError()
               .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));
   ```

### ✅ Quick Sanity Checklist

* [ ] No custom correlation accessor; OTel propagation only.
* [ ] Options use `ValidateDataAnnotations()` not `GetOptionsResult`.
* [ ] No home-grown `IMetrics`; counters created from `Meter`.
* [ ] Polly policies used directly; custom CB / retry classes removed.
* [ ] `TwoLevelCache` slimmed; emits OTel metrics.
* [ ] `AuditInterceptor` captures `Activity.Current.TraceId`.
* [ ] `OutboxMessage` stores W3C `traceparent`.
* [ ] `IUnitOfWork` exposes `HasActiveTransaction` + proper return types.

### Current Infrastructure Structure
```
Infrastructure/
├── Caching/              # Basic caching behaviors
├── Logging/              # Simple logging behavior
├── Messaging/            # MassTransit & basic outbox
├── Observability/        # OpenTelemetry setup
├── Persistence/          # EF Core repositories (mixed concerns)
├── Resilience/           # Polly extensions
├── Security/             # Auth handlers
└── TestBase/            # Test infrastructure
```

### Problems Identified
- ❌ **Mixed Concerns**: Persistence folder contains too many responsibilities
- ❌ **No Result Pattern**: Direct exception throwing instead of Result<T>
- ❌ **Weak Outbox**: Basic implementation without proper reliability
- ❌ **Limited Resilience**: Basic Polly without circuit breakers
- ❌ **Poor Separation**: Read/Write repositories not properly isolated
- ❌ **No Event Store**: Missing event sourcing infrastructure (though not MVP)
- ❌ **Over-engineered Components**: Custom correlation, metrics, circuit-breaker wrappers that duplicate .NET functionality

---

## 2. Target Architecture

### New Infrastructure Organization
```
Infrastructure/
├── Core/                        # Core infrastructure abstractions
│   ├── Abstractions/           # Base interfaces & contracts
│   ├── Configuration/          # Configuration management
│   └── DependencyInjection/    # Service registration
├── Persistence/                 # Data access layer
│   ├── EntityFramework/        # EF Core implementation
│   │   ├── Configuration/      # Entity configurations
│   │   ├── Interceptors/       # EF interceptors
│   │   ├── Read/              # Read-side implementation
│   │   └── Write/             # Write-side implementation
│   ├── Repositories/           # Repository implementations
│   │   ├── Read/              # Read repositories
│   │   └── Write/             # Write repositories
│   └── UnitOfWork/            # Transaction management
├── Messaging/                   # Event & message infrastructure
│   ├── Outbox/                # Transactional outbox pattern
│   ├── EventBus/              # Event bus abstractions
│   ├── MassTransit/           # MassTransit implementation
│   └── DomainEvents/          # Domain event dispatching
├── Caching/                    # Multi-level caching
│   ├── Memory/                # In-memory caching
│   ├── Distributed/           # Redis caching
│   └── Behaviors/             # Cache behaviors
├── Observability/              # Monitoring & diagnostics
│   ├── OpenTelemetry/         # Tracing & metrics
│   ├── HealthChecks/          # Health monitoring
│   └── Logging/               # Structured logging
├── Resilience/                 # Fault tolerance
│   ├── CircuitBreaker/        # Circuit breaker pattern
│   ├── Retry/                 # Retry policies
│   ├── Timeout/               # Timeout handling
│   └── Bulkhead/              # Bulkhead isolation
├── Security/                   # Security infrastructure
│   ├── Authentication/        # JWT & auth
│   ├── Authorization/         # Permission handling
│   └── Encryption/            # Data encryption
└── Testing/                    # Test support
    ├── Fixtures/              # Test fixtures
    ├── Builders/              # Test data builders
    └── Containers/            # Test containers
```

---

## 3. Implementation Phases

### Phase Dependencies & Timeline

```mermaid
graph LR
    P1[Phase 1: Core<br/>2 days] --> P2[Phase 2: Persistence<br/>3 days]
    P2 --> P3[Phase 3: Messaging<br/>2 days]
    P3 --> P4[Phase 4: Observability<br/>2 days]
    P4 --> P5[Phase 5: Caching<br/>1 day]
```

### Verification Gates
- **Phase 1 Gate**: Core abstractions compile with Result<T> pattern
- **Phase 2 Gate**: Persistence works with new PostgreSQL database
- **Phase 3 Gate**: Outbox pattern functioning with domain events
- **Phase 4 Gate**: Full observability pipeline active
- **Phase 5 Gate**: Caching layer integrated and tested

---

## 4. Phase 1: Core Infrastructure Foundation

### 4.1 Core Abstractions (Minimal Set)

#### File: `Infrastructure/Core/Abstractions/IDateTimeProvider.cs`
```csharp
namespace BuildingBlocks.Infrastructure.Core.Abstractions;

/// <summary>
/// Abstraction for date/time operations to enable testing
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
    DateTimeOffset UtcNowOffset { get; }
    DateTime Today { get; }
    
    // For testing time-based operations
    void SetFixedTime(DateTime dateTime);
    void ResetTime();
}

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    private DateTime? _fixedTime;
    
    public DateTime UtcNow => _fixedTime ?? DateTime.UtcNow;
    public DateTimeOffset UtcNowOffset => _fixedTime.HasValue 
        ? new DateTimeOffset(_fixedTime.Value, TimeSpan.Zero) 
        : DateTimeOffset.UtcNow;
    public DateTime Today => UtcNow.Date;
    
    public void SetFixedTime(DateTime dateTime) => _fixedTime = dateTime;
    public void ResetTime() => _fixedTime = null;
}
```

### 4.2 Dependency Injection (Using Standard .NET Patterns)

#### File: `Infrastructure/Core/DependencyInjection/ServiceCollectionExtensions.cs`
```csharp
namespace BuildingBlocks.Infrastructure.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register core services (minimal set)
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        
        // Add HTTP context accessor for web scenarios
        services.AddHttpContextAccessor();
        
        // Add memory cache
        services.AddMemoryCache();
        
        // Configure options using standard .NET validation
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection("Database"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
            
        services.AddOptions<CacheOptions>()
            .Bind(configuration.GetSection("Cache"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
            
        services.AddOptions<MessagingOptions>()
            .Bind(configuration.GetSection("Messaging"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        return services;
    }
}
```

### 4.3 Options Classes with Data Annotations

#### File: `Infrastructure/Core/Configuration/DatabaseOptions.cs`
```csharp
namespace BuildingBlocks.Infrastructure.Core.Configuration;

public sealed class DatabaseOptions
{
    [Required]
    [StringLength(500, MinimumLength = 10)]
    public string ConnectionString { get; set; } = string.Empty;
    
    [Range(1, 300)]
    public int CommandTimeoutSeconds { get; set; } = 30;
    
    [Range(1, 10)]
    public int RetryAttempts { get; set; } = 3;
    
    public bool EnableSensitiveDataLogging { get; set; } = false;
}

public sealed class CacheOptions
{
    [Range(1, 86400)]
    public int DefaultTtlSeconds { get; set; } = 3600;
    
    [Required]
    public string RedisConnectionString { get; set; } = string.Empty;
    
    [Range(1, 100)]
    public int MaxRetries { get; set; } = 3;
}

public sealed class MessagingOptions
{
    [Required]
    public string RabbitMqConnectionString { get; set; } = string.Empty;
    
    [Range(1, 1000)]
    public int BatchSize { get; set; } = 50;
    
    [Range(1000, 60000)]
    public int ProcessingIntervalMs { get; set; } = 5000;
}
```

---

## 5. Phase 2: Persistence Layer

### 5.1 Unit of Work Pattern

#### File: `Infrastructure/Persistence/UnitOfWork/IUnitOfWork.cs`
```csharp
namespace BuildingBlocks.Infrastructure.Persistence.UnitOfWork;

/// <summary>
/// Unit of Work pattern with Result<T> for transaction management
/// </summary>
public interface IUnitOfWork : IDisposable
{
    Task<Result<int>> SaveChangesAsync(CancellationToken ct = default);
    Task<Result<IDbContextTransaction>> BeginTransactionAsync(CancellationToken ct = default);
    Task<Result<Unit>> CommitTransactionAsync(CancellationToken ct = default);
    Task<Result<Unit>> RollbackTransactionAsync(CancellationToken ct = default);
    
    bool HasActiveTransaction { get; }
    
    // Execute in transaction with automatic rollback on failure
    Task<Result<T>> ExecuteInTransactionAsync<T>(
        Func<Task<Result<T>>> operation,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken ct = default);
}
```

#### File: `Infrastructure/Persistence/UnitOfWork/EfUnitOfWork.cs`
```csharp
namespace BuildingBlocks.Infrastructure.Persistence.UnitOfWork;

public sealed class EfUnitOfWork<TContext> : IUnitOfWork 
    where TContext : DbContext
{
    private readonly TContext _context;
    private readonly ILogger<EfUnitOfWork<TContext>> _logger;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private IDbContextTransaction? _currentTransaction;
    
    public bool HasActiveTransaction => _currentTransaction is not null;
    
    public EfUnitOfWork(
        TContext context,
        ILogger<EfUnitOfWork<TContext>> logger,
        IDomainEventDispatcher eventDispatcher)
    {
        _context = context;
        _logger = logger;
        _eventDispatcher = eventDispatcher;
    }
    
    public async Task<Result<int>> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            // Dispatch domain events before saving
            await DispatchDomainEvents(ct);
            
            var result = await _context.SaveChangesAsync(ct);
            
            _logger.LogDebug("Saved {Count} entities", result);
            return Result<int>.Success(result);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict detected");
            return Result<int>.Failure(Error.Conflict("Concurrency conflict detected"));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database update failed");
            return Result<int>.Failure(Error.Internal("Database update failed", ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during save");
            return Result<int>.Failure(Error.FromException(ex));
        }
    }
    
    public async Task<Result<IDbContextTransaction>> BeginTransactionAsync(CancellationToken ct = default)
    {
        if (_currentTransaction != null)
        {
            return Result<IDbContextTransaction>.Failure(Error.InvalidOperation(
                "Transaction", "Transaction already in progress"));
        }
        
        try
        {
            _currentTransaction = await _context.Database.BeginTransactionAsync(ct);
            return Result<IDbContextTransaction>.Success(_currentTransaction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to begin transaction");
            return Result<IDbContextTransaction>.Failure(Error.FromException(ex));
        }
    }
    
    public async Task<Result<Unit>> CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_currentTransaction == null)
        {
            return Result<Unit>.Failure(Error.InvalidOperation(
                "Transaction", "No transaction in progress"));
        }
        
        try
        {
            await _currentTransaction.CommitAsync(ct);
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to commit transaction");
            return Result<Unit>.Failure(Error.FromException(ex));
        }
    }
    
    public async Task<Result<Unit>> RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_currentTransaction == null)
        {
            return Result<Unit>.Success(Unit.Value);
        }
        
        try
        {
            await _currentTransaction.RollbackAsync(ct);
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rollback transaction");
            return Result<Unit>.Failure(Error.FromException(ex));
        }
    }
    
    public async Task<Result<T>> ExecuteInTransactionAsync<T>(
        Func<Task<Result<T>>> operation,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken ct = default)
    {
        // Use existing transaction if available
        if (_currentTransaction != null)
        {
            return await operation();
        }
        
        await using var transaction = await _context.Database
            .BeginTransactionAsync(isolationLevel, ct);
        
        try
        {
            var result = await operation();
            
            if (result.IsSuccess)
            {
                await transaction.CommitAsync(ct);
            }
            else
            {
                await transaction.RollbackAsync(ct);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Transaction failed and was rolled back");
            return Result<T>.Failure(Error.FromException(ex));
        }
    }
    
    private async Task DispatchDomainEvents(CancellationToken ct)
    {
        var aggregates = _context.ChangeTracker
            .Entries<IAggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();
        
        var domainEvents = aggregates
            .SelectMany(a => a.ClearDomainEvents())
            .ToList();
        
        foreach (var domainEvent in domainEvents)
        {
            await _eventDispatcher.DispatchAsync(domainEvent, ct);
        }
    }
    
    public void Dispose()
    {
        _currentTransaction?.Dispose();
        _context.Dispose();
    }
}
```

### 5.2 Repository Pattern with Result<T>

#### File: `Infrastructure/Persistence/Repositories/Write/WriteRepository.cs`
```csharp
namespace BuildingBlocks.Infrastructure.Persistence.Repositories.Write;

public class WriteRepository<TEntity, TId> : IWriteRepository<TEntity, TId>
    where TEntity : class, IAggregateRoot<TId>
    where TId : struct
{
    protected readonly DbContext Context;
    protected readonly DbSet<TEntity> DbSet;
    private readonly ILogger<WriteRepository<TEntity, TId>> _logger;
    
    public WriteRepository(
        DbContext context,
        ILogger<WriteRepository<TEntity, TId>> logger)
    {
        Context = context;
        DbSet = context.Set<TEntity>();
        _logger = logger;
    }
    
    public async Task<Result<TEntity>> GetByIdAsync(TId id, CancellationToken ct = default)
    {
        try
        {
            var entity = await DbSet
                .FirstOrDefaultAsync(e => e.Id.Equals(id), ct);
            
            return entity is not null
                ? Result<TEntity>.Success(entity)
                : Result<TEntity>.Failure(Error.NotFound(typeof(TEntity).Name, id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get {Entity} by id {Id}", 
                typeof(TEntity).Name, id);
            return Result<TEntity>.Failure(Error.FromException(ex));
        }
    }
    
    public async Task<Result<TEntity>> AddAsync(TEntity entity, CancellationToken ct = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(entity);
            
            var entry = await DbSet.AddAsync(entity, ct);
            return Result<TEntity>.Success(entry.Entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add {Entity}", typeof(TEntity).Name);
            return Result<TEntity>.Failure(Error.FromException(ex));
        }
    }
    
    public Result<Unit> Update(TEntity entity)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(entity);
            
            DbSet.Update(entity);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update {Entity}", typeof(TEntity).Name);
            return Result<Unit>.Failure(Error.FromException(ex));
        }
    }
    
    public Result<Unit> Remove(TEntity entity)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(entity);
            
            DbSet.Remove(entity);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove {Entity}", typeof(TEntity).Name);
            return Result<Unit>.Failure(Error.FromException(ex));
        }
    }
    
    public async Task<Result<bool>> ExistsAsync(TId id, CancellationToken ct = default)
    {
        try
        {
            var exists = await DbSet.AnyAsync(e => e.Id.Equals(id), ct);
            return Result<bool>.Success(exists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check existence of {Entity} with id {Id}", 
                typeof(TEntity).Name, id);
            return Result<bool>.Failure(Error.FromException(ex));
        }
    }
}
```

#### File: `Infrastructure/Persistence/Repositories/Read/ReadRepository.cs`
```csharp
namespace BuildingBlocks.Infrastructure.Persistence.Repositories.Read;

public class ReadRepository<TEntity, TId> : IReadRepository<TEntity, TId>
    where TEntity : class, IEntity<TId>
    where TId : struct
{
    protected readonly DbContext Context;
    protected readonly IQueryable<TEntity> Query;
    private readonly ILogger<ReadRepository<TEntity, TId>> _logger;
    
    public ReadRepository(
        DbContext context,
        ILogger<ReadRepository<TEntity, TId>> logger)
    {
        Context = context;
        Query = context.Set<TEntity>().AsNoTracking();
        _logger = logger;
    }
    
    public async Task<Result<TEntity>> GetByIdAsync(TId id, CancellationToken ct = default)
    {
        try
        {
            var entity = await Query
                .FirstOrDefaultAsync(e => e.Id.Equals(id), ct);
            
            return entity is not null
                ? Result<TEntity>.Success(entity)
                : Result<TEntity>.Failure(Error.NotFound(typeof(TEntity).Name, id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get {Entity} by id {Id}", 
                typeof(TEntity).Name, id);
            return Result<TEntity>.Failure(Error.FromException(ex));
        }
    }
    
    public async Task<Result<IReadOnlyList<TEntity>>> GetAllAsync(CancellationToken ct = default)
    {
        try
        {
            var entities = await Query.ToListAsync(ct);
            return Result<IReadOnlyList<TEntity>>.Success(entities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all {Entity}", typeof(TEntity).Name);
            return Result<IReadOnlyList<TEntity>>.Failure(Error.FromException(ex));
        }
    }
    
    public async Task<Result<PagedResult<TEntity>>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken ct = default)
    {
        try
        {
            if (pageNumber < 1 || pageSize < 1)
            {
                return Result<PagedResult<TEntity>>.Failure(
                    Error.Validation("INVALID_PAGINATION", "Invalid page number or size"));
            }
            
            var totalCount = await Query.CountAsync(ct);
            
            var items = await Query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
            
            var pagedResult = new PagedResult<TEntity>(
                items,
                totalCount,
                pageNumber,
                pageSize);
            
            return Result<PagedResult<TEntity>>.Success(pagedResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get paged {Entity}", typeof(TEntity).Name);
            return Result<PagedResult<TEntity>>.Failure(Error.FromException(ex));
        }
    }
    
    public async Task<Result<Option<TEntity>>> FindAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default)
    {
        try
        {
            var entity = await Query.FirstOrDefaultAsync(predicate, ct);
            return Result<Option<TEntity>>.Success(Option<TEntity>.From(entity));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find {Entity}", typeof(TEntity).Name);
            return Result<Option<TEntity>>.Failure(Error.FromException(ex));
        }
    }
}
```

### 5.3 Database Context with Interceptors

#### File: `Infrastructure/Persistence/EntityFramework/AxonDbContext.cs`
```csharp
namespace BuildingBlocks.Infrastructure.Persistence.EntityFramework;

public class AxonDbContext : DbContext
{
    private readonly IDateTimeProvider _dateTimeProvider;
    
    public AxonDbContext(
        DbContextOptions<AxonDbContext> options,
        IDateTimeProvider dateTimeProvider) 
        : base(options)
    {
        _dateTimeProvider = dateTimeProvider;
    }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseNpgsql(options =>
            {
                options.EnableRetryOnFailure(3);
                options.CommandTimeout(30);
            })
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(
                new AuditInterceptor(_dateTimeProvider),
                new DomainEventInterceptor(),
                new OutboxInterceptor(),
                new PerformanceInterceptor());
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AxonDbContext).Assembly);
        
        // Configure outbox table
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("outbox_messages");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ProcessedAt);
            entity.HasIndex(e => new { e.Status, e.CreatedAt });
            entity.Property(e => e.Payload).HasColumnType("jsonb");
            entity.Property(e => e.Headers).HasColumnType("jsonb");
        });
        
        base.OnModelCreating(modelBuilder);
    }
}
```

---

## 6. Phase 3: Messaging & Events

### 6.1 Transactional Outbox Pattern

#### File: `Infrastructure/Messaging/Outbox/OutboxMessage.cs`
```csharp
namespace BuildingBlocks.Infrastructure.Messaging.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = null!;
    public string Payload { get; private set; } = null!;
    public Dictionary<string, string> Headers { get; private set; } = null!;
    public OutboxMessageStatus Status { get; private set; }
    public int RetryCount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public DateTime? NextRetryAt { get; private set; }
    public string? Error { get; private set; }
    
    private OutboxMessage() { } // EF Core
    
    public static OutboxMessage Create(IIntegrationEvent @event)
    {
        var headers = new Dictionary<string, string>
        {
            ["MessageType"] = @event.GetType().FullName!,
            ["EventId"] = @event.EventId.ToString(),
            ["OccurredAt"] = @event.OccurredAt.ToString("O")
        };
        
        // Use OpenTelemetry Activity.Current for tracing
        var activity = Activity.Current;
        if (activity is not null)
        {
            headers["traceparent"] = activity.Id!;
            if (!string.IsNullOrEmpty(activity.TraceStateString))
                headers["tracestate"] = activity.TraceStateString;
            
            // Add baggage items for business context
            foreach (var baggage in activity.Baggage)
            {
                headers[$"baggage.{baggage.Key}"] = baggage.Value ?? string.Empty;
            }
        }
        
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = @event.GetType().Name,
            Payload = JsonSerializer.Serialize(@event),
            Headers = headers,
            Status = OutboxMessageStatus.Pending,
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow
        };
    }
    
    public Result<Unit> MarkAsProcessed()
    {
        if (Status != OutboxMessageStatus.Pending)
        {
            return Result<Unit>.Failure(Error.InvalidOperation(
                "MarkAsProcessed", "Message is not in pending status"));
        }
        
        Status = OutboxMessageStatus.Processed;
        ProcessedAt = DateTime.UtcNow;
        return Result<Unit>.Success(Unit.Value);
    }
    
    public Result<Unit> MarkAsFailed(string error, DateTime nextRetryAt)
    {
        if (Status == OutboxMessageStatus.Processed)
        {
            return Result<Unit>.Failure(Error.InvalidOperation(
                "MarkAsFailed", "Cannot fail a processed message"));
        }
        
        RetryCount++;
        Error = error;
        NextRetryAt = nextRetryAt;
        
        if (RetryCount >= 3)
        {
            Status = OutboxMessageStatus.Failed;
        }
        
        return Result<Unit>.Success(Unit.Value);
    }
}

public enum OutboxMessageStatus
{
    Pending = 0,
    Processed = 1,
    Failed = 2
}
```

#### File: `Infrastructure/Messaging/Outbox/OutboxProcessor.cs`
```csharp
namespace BuildingBlocks.Infrastructure.Messaging.Outbox;

public sealed class OutboxProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly OutboxOptions _options;
    
    public OutboxProcessor(
        IServiceProvider serviceProvider,
        ILogger<OutboxProcessor> logger,
        IOptions<OutboxOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessages(stoppingToken);
                await Task.Delay(_options.ProcessingInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
    
    private async Task ProcessOutboxMessages(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AxonDbContext>();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        
        var messages = await dbContext.Set<OutboxMessage>()
            .Where(m => m.Status == OutboxMessageStatus.Pending)
            .Where(m => m.NextRetryAt == null || m.NextRetryAt <= DateTime.UtcNow)
            .OrderBy(m => m.CreatedAt)
            .Take(_options.BatchSize)
            .ToListAsync(ct);
        
        foreach (var message in messages)
        {
            using var activity = Activity.StartActivity("OutboxMessage.Process");
            activity?.SetTag("message.id", message.Id);
            activity?.SetTag("message.type", message.Type);
            
            try
            {
                var eventType = Type.GetType(message.Headers["MessageType"]);
                if (eventType == null)
                {
                    _logger.LogWarning("Unknown message type: {Type}", message.Type);
                    message.MarkAsFailed($"Unknown type: {message.Type}", 
                        DateTime.UtcNow.AddYears(1));
                    continue;
                }
                
                var @event = JsonSerializer.Deserialize(message.Payload, eventType);
                if (@event == null)
                {
                    _logger.LogWarning("Failed to deserialize message: {Id}", message.Id);
                    message.MarkAsFailed("Deserialization failed", 
                        DateTime.UtcNow.AddYears(1));
                    continue;
                }
                
                await messageBus.PublishAsync(@event, ct);
                message.MarkAsProcessed();
                
                _logger.LogInformation("Published outbox message {Id} of type {Type}", 
                    message.Id, message.Type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process outbox message {Id}", message.Id);
                
                var nextRetry = CalculateNextRetry(message.RetryCount);
                message.MarkAsFailed(ex.Message, nextRetry);
            }
        }
        
        await dbContext.SaveChangesAsync(ct);
    }
    
    private DateTime CalculateNextRetry(int retryCount)
    {
        // Exponential backoff: 1s, 4s, 16s, 64s...
        var delaySeconds = Math.Pow(2, retryCount * 2);
        return DateTime.UtcNow.AddSeconds(Math.Min(delaySeconds, 3600)); // Max 1 hour
    }
}
```

---

## 7. Phase 4: Observability & Resilience

### 7.1 Resilience with Direct Polly Integration

#### File: `Infrastructure/Resilience/ResilienceExtensions.cs`
```csharp
namespace BuildingBlocks.Infrastructure.Resilience;

public static class ResilienceExtensions
{
    public static IServiceCollection AddResiliencePatterns(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure HTTP clients with Polly policies directly
        services.AddHttpClient("external-api")
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy())
            .AddPolicyHandler(GetTimeoutPolicy());
            
        // Add database resilience
        services.AddDbContextPool<AxonDbContext>((provider, options) =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
            });
        });
        
        return services;
    }
    
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return Policy<HttpResponseMessage>
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    using var activity = Activity.StartActivity("Retry");
                    activity?.SetTag("retry.attempt", retryCount);
                    activity?.SetTag("retry.delay_ms", timespan.TotalMilliseconds);
                });
    }
    
    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return Policy<HttpResponseMessage>
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (delegateResult, duration) =>
                {
                    using var activity = Activity.StartActivity("CircuitBreakerOpened");
                    activity?.SetTag("circuit_breaker.duration_seconds", duration.TotalSeconds);
                },
                onReset: () =>
                {
                    using var activity = Activity.StartActivity("CircuitBreakerReset");
                });
    }
    
    private static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy()
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(30));
    }
}
```
        
        _policy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: options.FailureThreshold,
                durationOfBreak: options.BreakDuration,
                onBreak: OnBreak,
                onReset: OnReset,
                onHalfOpen: OnHalfOpen);
    }
    
    public async Task<Result<T>> ExecuteAsync<T>(
        Func<Task<Result<T>>> operation,
        string operationKey,
        CancellationToken ct = default)
    {
        try
        {
            var policyResult = await _policy.ExecuteAndCaptureAsync(
                async () =>
                {
                    var result = await operation();
                    
                    // Convert Result<T> to HttpResponseMessage for policy evaluation
                    var response = new HttpResponseMessage(
                        result.IsSuccess ? HttpStatusCode.OK : HttpStatusCode.InternalServerError);
                    
                    if (!result.IsSuccess)
                    {
                        // Store the error in response for later retrieval
                        response.Headers.Add("X-Error", JsonSerializer.Serialize(result.Error));
                    }
                    
                    return response;
                },
                ct);
            
            if (policyResult.Outcome == OutcomeType.Successful)
            {
                // Execute the actual operation
                return await operation();
            }
            
            // Circuit is open
            _metrics.IncrementCounter("circuit_breaker.rejected", 
                new TagList { { "operation", operationKey } });
            
            return Result<T>.Failure(Error.ServiceUnavailable(
                $"Circuit breaker is open for operation: {operationKey}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Circuit breaker execution failed for {Operation}", 
                operationKey);
            return Result<T>.Failure(Error.FromException(ex));
        }
    }
    
    private void OnBreak(DelegateResult<HttpResponseMessage> result, TimeSpan duration)
    {
        _logger.LogWarning("Circuit breaker opened for {Duration}s", duration.TotalSeconds);
        _metrics.IncrementCounter("circuit_breaker.opened");
    }
    
    private void OnReset()
    {
        _logger.LogInformation("Circuit breaker reset");
        _metrics.IncrementCounter("circuit_breaker.reset");
    }
    
    private void OnHalfOpen()
    {
        _logger.LogInformation("Circuit breaker is half-open");
        _metrics.IncrementCounter("circuit_breaker.half_open");
    }
}
```

### 7.2 OpenTelemetry Configuration

#### File: `Infrastructure/Observability/OpenTelemetry/OpenTelemetryExtensions.cs`
```csharp
namespace BuildingBlocks.Infrastructure.Observability.OpenTelemetry;

public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddAxonObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Use standard .NET options validation instead of custom Result wrapper
        services.AddOptions<ObservabilityOptions>()
            .Bind(configuration.GetSection("Observability"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
            
        var options = configuration.GetSection("Observability")
            .Get<ObservabilityOptions>()!;
        
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(options.ServiceName)
                .AddAttributes(new[]
                {
                    new KeyValuePair<string, object>("environment", options.Environment),
                    new KeyValuePair<string, object>("version", options.Version),
                    new KeyValuePair<string, object>("deployment", options.DeploymentId)
                }))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(opts =>
                    {
                        opts.RecordException = true;
                        opts.Filter = httpContext => 
                            !httpContext.Request.Path.StartsWithSegments("/health");
                        opts.EnrichWithHttpRequest = (activity, request) =>
                        {
                            activity.SetTag("http.request.body.size", request.ContentLength);
                            activity.SetTag("http.user_agent", request.Headers.UserAgent);
                        };
                    })
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation(opts =>
                    {
                        opts.SetDbStatementForText = true;
                        opts.SetDbStatementForStoredProcedure = true;
                        opts.EnrichWithIDbCommand = (activity, command) =>
                        {
                            activity.SetTag("db.rows_affected", command.RecordsAffected);
                        };
                    })
                    .AddSource("Axon.*")
                    .SetSampler(new TraceIdRatioBasedSampler(options.TracingSampleRate))
                    .AddOtlpExporter(opts =>
                    {
                        opts.Endpoint = new Uri(options.OtlpEndpoint);
                        opts.Protocol = OtlpExportProtocol.Grpc;
                        opts.Headers = options.OtlpHeaders;
                    });
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddMeter("Axon.*")
                    .AddView("http.server.request.duration",
                        new ExplicitBucketHistogramConfiguration
                        {
                            Boundaries = new[] { 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10 }
                        })
                    .AddOtlpExporter(opts =>
                    {
                        opts.Endpoint = new Uri(options.OtlpEndpoint);
                        opts.Protocol = OtlpExportProtocol.Grpc;
                    });
            });
        
        // Register Meter for creating counters/gauges directly
        services.AddSingleton(provider => 
        {
            var meterProvider = provider.GetRequiredService<MeterProvider>();
            return new Meter("BuildingBlocks.Infrastructure", "1.0.0");
        });
        
        return services;
    }
}
```

---

## 8. Phase 5: Caching & Performance

### 8.1 Two-Level Cache (Lean Implementation)

#### File: `Infrastructure/Caching/TwoLevelCache.cs`
```csharp
namespace BuildingBlocks.Infrastructure.Caching;

public interface ITwoLevelCache
{
    Task<Result<Option<T>>> GetAsync<T>(string key, CancellationToken ct = default);
    Task<Result<Unit>> SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default);
    Task<Result<Unit>> RemoveAsync(string key, CancellationToken ct = default);
}

public sealed class TwoLevelCache : ITwoLevelCache
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    private readonly Counter<long> _cacheHits;
    private readonly Counter<long> _cacheMisses;
    private readonly ILogger<TwoLevelCache> _logger;
    
    public TwoLevelCache(
        IMemoryCache memoryCache,
        IDistributedCache distributedCache,
        Meter meter,
        ILogger<TwoLevelCache> logger)
    {
        _memoryCache = memoryCache;
        _distributedCache = distributedCache;
        _logger = logger;
        
        // Use OpenTelemetry metrics directly
        _cacheHits = meter.CreateCounter<long>("cache.hit");
        _cacheMisses = meter.CreateCounter<long>("cache.miss");
    }
    
    public async Task<Result<Option<T>>> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            // Check memory cache first
            if (_memoryCache.TryGetValue<T>(key, out var memValue))
            {
                _cacheHits.Add(1, new TagList { ["level"] = "memory" });
                return Result<Option<T>>.Success(Option<T>.Some(memValue));
            }
            
            // Check distributed cache
            var distributedBytes = await _distributedCache.GetAsync(key, ct);
            if (distributedBytes is not null)
            {
                var distributedValue = JsonSerializer.Deserialize<T>(distributedBytes);
                if (distributedValue is not null)
                {
                    _cacheHits.Add(1, new TagList { ["level"] = "distributed" });
                    
                    // Populate memory cache with shorter TTL
                    _memoryCache.Set(key, distributedValue, TimeSpan.FromMinutes(5));
                    
                    return Result<Option<T>>.Success(Option<T>.Some(distributedValue));
                }
            }
            
            _cacheMisses.Add(1);
            return Result<Option<T>>.Success(Option<T>.None());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cache value for key {Key}", key);
            return Result<Option<T>>.Failure(Error.FromException(ex));
        }
    }
    
    public async Task<Result<Unit>> SetAsync<T>(
        string key, 
        T value, 
        TimeSpan? expiration = null, 
        CancellationToken ct = default)
    {
        try
        {
            var ttl = expiration ?? TimeSpan.FromHours(1);
            
            // Set in memory cache (shorter TTL)
            _memoryCache.Set(key, value, TimeSpan.FromMinutes(Math.Min(ttl.TotalMinutes, 30)));
            
            // Set in distributed cache
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
            await _distributedCache.SetAsync(key, bytes, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            }, ct);
            
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set cache value for key {Key}", key);
            return Result<Unit>.Failure(Error.FromException(ex));
        }
    }
    
    public async Task<Result<Unit>> RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            _memoryCache.Remove(key);
            await _distributedCache.RemoveAsync(key, ct);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove cache value for key {Key}", key);
            return Result<Unit>.Failure(Error.FromException(ex));
        }
    }
}
```
```

---

## 9. File-by-File Refactoring Guide

### Files to DELETE Completely (Over-engineered Components)
```bash
# Remove custom abstractions that duplicate .NET functionality
rm -rf src/BuildingBlocks/Infrastructure/Core/Abstractions/ICorrelationContextAccessor.cs
rm -rf src/BuildingBlocks/Infrastructure/Core/Abstractions/CorrelationContext.cs
rm -rf src/BuildingBlocks/Infrastructure/Core/Configuration/ConfigurationExtensions.cs
rm -rf src/BuildingBlocks/Infrastructure/Observability/IMetrics.cs
rm -rf src/BuildingBlocks/Infrastructure/Observability/CustomMetrics.cs
rm -rf src/BuildingBlocks/Infrastructure/Resilience/CircuitBreaker/ICircuitBreakerPolicy.cs
rm -rf src/BuildingBlocks/Infrastructure/Resilience/CircuitBreaker/CircuitBreakerPolicy.cs
rm -rf src/BuildingBlocks/Infrastructure/Resilience/Retry/RetryPolicy.cs
rm -rf src/BuildingBlocks/Infrastructure/Resilience/Timeout/TimeoutPolicy.cs
```

### Files to REPLACE with Lean Implementations
- `Infrastructure/Persistence/Write/EfWriteRepository.cs` → Simplified with Result<T>
- `Infrastructure/Persistence/Read/EfReadRepository.cs` → Simplified with Result<T>
- `Infrastructure/Persistence/Write/EfWriteUnitOfWork.cs` → Add `HasActiveTransaction` property
- `Infrastructure/Messaging/Outbox/IntegrationEventWrapper.cs` → New OutboxMessage with Activity.Current
- `Infrastructure/Caching/MultiLevelCache.cs` → Thin TwoLevelCache wrapper
- `Infrastructure/Observability/OpenTelemetry/OpenTelemetryExtensions.cs` → Direct Meter registration
- `Web/Extensions/ServiceCollectionExtensions.cs` → Standard .NET options validation

### Files to CREATE (Minimal Set)
- `Infrastructure/Core/Abstractions/IDateTimeProvider.cs` (keep - useful for testing)
- `Infrastructure/Core/Configuration/DatabaseOptions.cs` (with DataAnnotations)
- `Infrastructure/Core/Configuration/CacheOptions.cs` (with DataAnnotations)  
- `Infrastructure/Core/Configuration/MessagingOptions.cs` (with DataAnnotations)
- `Infrastructure/Persistence/UnitOfWork/IUnitOfWork.cs` (with proper return types)
- `Infrastructure/Persistence/UnitOfWork/EfUnitOfWork.cs` (remove correlation dependency)
- `Infrastructure/Messaging/Outbox/OutboxProcessor.cs` (simplified)
- `Infrastructure/Resilience/ResilienceExtensions.cs` (direct Polly usage)
- `Infrastructure/Caching/TwoLevelCache.cs` (6-line implementation)

### Key Simplifications Applied
- ✅ **Correlation**: Removed custom accessor → Use `Activity.Current.TraceId`
- ✅ **Options**: Removed Result wrapper → Use `AddOptions().ValidateDataAnnotations()`
- ✅ **Metrics**: Removed IMetrics → Inject `Meter` directly
- ✅ **Circuit Breaker**: Removed wrapper → Register Polly policies directly
- ✅ **Caching**: Removed tags/custom options → Simple read-through pattern
- ✅ **Configuration**: Use standard .NET validation patterns

---

## 10. Verification & Testing

### Phase 1 Verification
```csharp
// Test: Only essential abstractions exist
[Fact]
public void CoreAbstractions_ShouldBeMinimal()
{
    var dateTimeProvider = new SystemDateTimeProvider();
    
    Assert.NotNull(dateTimeProvider.UtcNow);
    // No correlation accessor - using Activity.Current directly
}

// Test: Options validation works with standard .NET
[Fact]
public void OptionsValidation_ShouldUseStandardPatterns()
{
    var services = new ServiceCollection();
    var configuration = new ConfigurationBuilder().Build();
    
    services.AddOptions<DatabaseOptions>()
        .Bind(configuration.GetSection("Database"))
        .ValidateDataAnnotations()
        .ValidateOnStart();
    
    var provider = services.BuildServiceProvider();
    
    // Should throw on invalid configuration
    Assert.Throws<OptionsValidationException>(() => 
        provider.GetRequiredService<IOptions<DatabaseOptions>>().Value);
}
```

### Phase 2 Verification
```csharp
// Test: Repository returns Result<T>
[Fact]
public async Task Repository_ShouldReturnResult()
{
    var repository = new WriteRepository<TestEntity, Guid>(context, logger);
    var result = await repository.GetByIdAsync(Guid.NewGuid());
    
    Assert.True(result.IsFailure);
    Assert.Equal(ErrorType.NotFound, result.Error.Type);
}
```

### Phase 3 Verification
```csharp
// Test: Outbox uses OpenTelemetry Activity for tracing
[Fact]
public async Task Outbox_ShouldUseActivityCurrent()
{
    using var activity = new Activity("TestActivity").Start();
    
    var message = OutboxMessage.Create(new TestEvent());
    
    // Should have traceparent from Activity.Current
    Assert.True(message.Headers.ContainsKey("traceparent"));
    Assert.Equal(activity.Id, message.Headers["traceparent"]);
    
    await context.OutboxMessages.AddAsync(message);
    await context.SaveChangesAsync();
    
    await processor.ProcessAsync();
    
    Assert.Equal(OutboxMessageStatus.Processed, message.Status);
}
```

### Phase 4 Verification
```csharp
// Test: Polly policies work directly without custom wrappers
[Fact]
public async Task Polly_ShouldWorkDirectly()
{
    var policy = Policy<HttpResponseMessage>
        .HandleTransientHttpError()
        .CircuitBreakerAsync(3, TimeSpan.FromSeconds(1));
    
    var httpClient = new HttpClient();
    
    // Circuit breaker should open after 3 failures
    for (int i = 0; i < 3; i++)
    {
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await policy.ExecuteAsync(() => httpClient.GetAsync("http://invalid-url")));
    }
    
    // Circuit should now be open
    await Assert.ThrowsAsync<CircuitBreakerOpenException>(async () =>
        await policy.ExecuteAsync(() => httpClient.GetAsync("http://valid-url")));
}

// Test: OpenTelemetry Meter works without custom IMetrics
[Fact]
public void Meter_ShouldCreateCountersDirectly()
{
    var meter = new Meter("TestMeter", "1.0.0");
    var counter = meter.CreateCounter<long>("test.counter");
    
    counter.Add(1, new TagList { ["test"] = "value" });
    
    Assert.NotNull(counter);
    // No custom IMetrics wrapper needed
}
```

### Phase 5 Verification
```csharp
// Test: Two-level cache is lean and simple
[Fact]
public async Task TwoLevelCache_ShouldBeSimple()
{
    var memoryCache = new MemoryCache(new MemoryCacheOptions());
    var distributedCache = new MockDistributedCache();
    var meter = new Meter("TestMeter");
    var logger = Mock.Of<ILogger<TwoLevelCache>>();
    
    var cache = new TwoLevelCache(memoryCache, distributedCache, meter, logger);
    
    await cache.SetAsync("key", "value", TimeSpan.FromMinutes(5));
    var result = await cache.GetAsync<string>("key");
    
    Assert.True(result.IsSuccess);
    Assert.True(result.Value.IsSome);
    Assert.Equal("value", result.Value.Value);
    
    // No custom tags, metrics, or complex options - just simple caching
}
```

---

## Success Criteria

### Technical Metrics
- ✅ All infrastructure code uses Result<T> pattern
- ✅ Zero custom abstractions that duplicate .NET functionality
- ✅ Direct use of OpenTelemetry Activity.Current for tracing
- ✅ Standard .NET options validation with DataAnnotations
- ✅ Direct Polly policy registration (no wrappers)
- ✅ Simple two-level cache (< 100 lines)
- ✅ OpenTelemetry Meter used directly for metrics

### Simplification Metrics
- ✅ Removed ICorrelationContextAccessor → Use Activity.Current
- ✅ Removed GetOptionsResult<T> → Use AddOptions().ValidateDataAnnotations()
- ✅ Removed IMetrics → Inject Meter directly
- ✅ Removed CircuitBreakerPolicy wrapper → Use Polly directly
- ✅ Removed MultiLevelCache complexity → Simple TwoLevelCache
- ✅ UnitOfWork has HasActiveTransaction + proper return types

### Performance Metrics
- ✅ Database queries < 50ms p99
- ✅ Cache hit ratio > 80%
- ✅ Outbox processing latency < 100ms
- ✅ No performance overhead from custom wrappers

### Quality Metrics
- ✅ Test coverage > 90%
- ✅ No compiler warnings
- ✅ All code follows functional patterns
- ✅ Lean codebase with minimal abstractions

---

## Conclusion

This implementation guide provides a **lean roadmap** for refactoring the BuildingBlocks Infrastructure folder by **eliminating over-engineering**. The key principles applied:

1. **Eliminate Custom Wrappers**: Remove abstractions that duplicate .NET functionality
2. **Use Standard Libraries**: OpenTelemetry, Polly, built-in options validation
3. **Minimal Abstractions**: Only abstract what adds genuine business value
4. **Direct Integration**: No intermediate layers that hide vendor functionality
5. **Simple Implementations**: Favor 6-line implementations over 300-line ones

### Key Deletions Made:
- ❌ Custom correlation accessor → OpenTelemetry Activity.Current
- ❌ Hand-rolled options validation → .NET AddOptions().ValidateDataAnnotations()
- ❌ Custom IMetrics → OpenTelemetry Meter directly
- ❌ Circuit breaker wrapper → Polly policies directly
- ❌ Over-engineered cache → Simple two-level wrapper

The implementation can be completed in approximately **5 days** (down from 10) with the simplified approach, resulting in **less code to maintain**, **better vendor interoperability**, and **reduced cognitive overhead**.