using System.Data;
using System.Diagnostics;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Application.Outbox;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Core.Abstractions.CQRS; // for ICommand<TResponse>
using System.Reflection;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// DB transaction wrapper for commands with domain-event integration and optional outbox.
/// - Commands only (compile-time constrained)
/// - Begins EF transaction, executes handler, stores events to outbox (if enabled), commits
/// - Post-commit: trigger outbox processor OR legacy dispatch
/// - Always clears domain events after commit to avoid re-dispatch
/// - Static ActivitySource for consistent tracing
/// </summary>
public sealed class CommandTransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, ICommand<TResponse>
    where TResponse : IResult
{
    private static readonly ActivitySource ActivitySource = new("Axon.Application.Transactions");

    private readonly DbContext _dbContext;
    private readonly IDomainEventDispatcher _domainEventDispatcher;
    private readonly IOutboxService? _outboxService;
    private readonly IOutboxProcessor? _outboxProcessor;
    private readonly IOptions<TransactionOptions> _options;
    private readonly ILogger<CommandTransactionBehavior<TRequest, TResponse>> _logger;

    public CommandTransactionBehavior(
        DbContext dbContext,
        IDomainEventDispatcher domainEventDispatcher,
        ILogger<CommandTransactionBehavior<TRequest, TResponse>> logger,
        IOutboxService? outboxService = null,
        IOutboxProcessor? outboxProcessor = null,
        IOptions<TransactionOptions>? options = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _domainEventDispatcher = domainEventDispatcher ?? throw new ArgumentNullException(nameof(domainEventDispatcher));
        _outboxService = outboxService;
        _outboxProcessor = outboxProcessor;
        _options = options ?? Options.Create(new TransactionOptions());
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Already inside a transaction? Respect it and don’t nest.
        if (_dbContext.Database.CurrentTransaction is not null)
        {
            _logger.LogDebug("Existing transaction detected for {RequestType}; skipping new transaction", typeof(TRequest).Name);
            return await next(); // MediatR delegate has no token parameter
        }

        var txId = Guid.NewGuid();
        var (traceId, requestId, metadata) = ExtractRequestContext(request);
        var isolation = SelectIsolation(metadata);

        using var activity = ActivitySource.StartActivity("db.transaction", ActivityKind.Internal);
        activity?.SetTag("transaction.id", txId);
        activity?.SetTag("transaction.isolation", isolation.ToString());
        activity?.SetTag("request.type", typeof(TRequest).Name);
        if (!string.IsNullOrWhiteSpace(traceId)) activity?.SetTag("trace.id", traceId);

        if (metadata is { Count: > 0 })
            foreach (var (k, v) in metadata) activity?.SetTag($"request.metadata.{k}", v?.ToString());

        var sw = Stopwatch.StartNew();

        await using var tx = await _dbContext.Database.BeginTransactionAsync(isolation, cancellationToken);

        try
        {
            var response = await next();

            if (response.IsFailure)
            {
                sw.Stop();
                _logger.LogWarning("Command {Request} failed; rolling back tx {TxId}: {Error}",
                    typeof(TRequest).Name, txId, response.Error?.Message);
                activity?.SetStatus(ActivityStatusCode.Error, response.Error?.Message);
                activity?.SetTag("transaction.duration_ms", sw.ElapsedMilliseconds);
                await tx.RollbackAsync(cancellationToken);
                return response;
            }

            // Collect domain events BEFORE commit (outbox write participates in same tx)
            var domainEvents = CollectDomainEventsSafe();

            if (_outboxService is not null && _options.Value.EnableOutboxProcessing && domainEvents.Count > 0)
            {
                var storeResult = await _outboxService.StoreEventsAsync(
                    domainEvents, txId, traceId, requestId, metadata, cancellationToken);

                if (storeResult.IsFailure)
                {
                    sw.Stop();
                    _logger.LogError("Outbox store failed for tx {TxId}: {Error}", txId, storeResult.Error.Message);
                    activity?.SetStatus(ActivityStatusCode.Error, storeResult.Error.Message);
                    await tx.RollbackAsync(cancellationToken);
                    return FailureResult(storeResult.Error);
                }

                activity?.SetTag("outbox.events_stored", storeResult.Value);
            }
            else if (domainEvents.Count > 0)
            {
                // Legacy in-memory dispatch AFTER commit, but we collect now
                activity?.SetTag("domain.events_collected", domainEvents.Count);
            }

            // Commit writes + outbox in same transaction
            await tx.CommitAsync(cancellationToken);
            sw.Stop();

            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("transaction.duration_ms", sw.ElapsedMilliseconds);
            activity?.SetTag("transaction.committed", true);

            _logger.LogInformation("Tx {TxId} committed for {Request} in {Elapsed}ms (events: {Events})",
                txId, typeof(TRequest).Name, sw.ElapsedMilliseconds, domainEvents.Count);

            // IMPORTANT: clear domain events after commit in ALL paths
            ClearDomainEventsSafe();

            // Post-commit processing (fire-and-forget; don’t tie to request cancellation)
            await PostCommitAsync(domainEvents, txId);

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("transaction.duration_ms", sw.ElapsedMilliseconds);
            activity?.SetTag("transaction.failed", true);
            activity?.AddException(ex);

            _logger.LogError(ex, "Exception in tx {TxId} for {Request} after {Elapsed}ms; rolling back",
                txId, typeof(TRequest).Name, sw.ElapsedMilliseconds);

            try { await tx.RollbackAsync(CancellationToken.None); }
            catch (Exception rbEx) { _logger.LogError(rbEx, "Rollback failed for tx {TxId}", txId); }

            return FailureResult(Error.Internal($"Transaction failed for {typeof(TRequest).Name}: {ex.Message}", "TRANSACTION_FAILED", ex));
        }
    }

    private IsolationLevel SelectIsolation(IReadOnlyDictionary<string, object>? metadata)
    {
        if (!_options.Value.UseMetadataAwareIsolation || metadata is null) return _options.Value.DefaultIsolationLevel;

        if (metadata.TryGetValue("IsolationLevel", out var explicitLevel) &&
            Enum.TryParse<IsolationLevel>(explicitLevel?.ToString(), out var level))
            return level;

        if (metadata.ContainsKey("RequireSerializable") || metadata.ContainsKey("HighConsistency"))
            return IsolationLevel.Serializable;

        if (metadata.ContainsKey("FinancialOperation") || metadata.ContainsKey("CriticalOperation"))
            return IsolationLevel.RepeatableRead;

        if (metadata.TryGetValue("TenantId", out var tenant) && string.Equals(tenant?.ToString(), "high-consistency-tenant", StringComparison.Ordinal))
            return IsolationLevel.RepeatableRead;

        return _options.Value.DefaultIsolationLevel;
    }

    private static (string? TraceId, Guid? RequestId, IReadOnlyDictionary<string, object>? Metadata)
        ExtractRequestContext(TRequest request)
    {
        string? traceId = Activity.Current?.TraceId.ToString();
        Guid? requestId = null;
        IReadOnlyDictionary<string, object>? metadata = null;

        var t = typeof(TRequest);

        var traceProp = t.GetProperty("TraceId", BindingFlags.Public | BindingFlags.Instance);
        if (traceProp?.GetValue(request) is string reqTrace && !string.IsNullOrWhiteSpace(reqTrace))
            traceId = reqTrace;

        var ridProp = t.GetProperty("RequestId", BindingFlags.Public | BindingFlags.Instance);
        if (ridProp?.GetValue(request) is Guid rid && rid != Guid.Empty)
            requestId = rid;

        var mdProp = t.GetProperty("Metadata", BindingFlags.Public | BindingFlags.Instance);
        if (mdProp?.GetValue(request) is IReadOnlyDictionary<string, object> md)
            metadata = md;

        return (traceId, requestId, metadata);
    }

    // ---- Domain events collection/clearing with robust shape handling ----

    private List<IDomainEvent> CollectDomainEventsSafe()
    {
        var list = new List<IDomainEvent>();

        foreach (var entry in _dbContext.ChangeTracker.Entries())
        {
            var entity = entry.Entity;
            if (entity is null) continue;

            // Preferred: aggregates implement a known interface exposing DomainEvents
            if (entity is IAggregateRootWithEvents typed && typed.DomainEvents is { Count: > 0 })
            {
                list.AddRange(typed.DomainEvents);
                continue;
            }

            // Fallback: reflection to read "DomainEvents" property
            var deProp = entity.GetType().GetProperty("DomainEvents", BindingFlags.Public | BindingFlags.Instance);
            if (deProp?.GetValue(entity) is IEnumerable<IDomainEvent> events)
            {
                list.AddRange(events);
            }
        }

        return list;
    }

    private void ClearDomainEventsSafe()
    {
        foreach (var entry in _dbContext.ChangeTracker.Entries())
        {
            var entity = entry.Entity;
            if (entity is null) continue;

            if (entity is IAggregateRootWithEvents typed)
            {
                typed.ClearDomainEvents();
                continue;
            }

            var clear = entity.GetType().GetMethod("ClearDomainEvents", BindingFlags.Public | BindingFlags.Instance);
            if (clear is not null)
            {
                try { clear.Invoke(entity, null); } catch { /* ignore */ }
            }
        }
    }

    // ---- Post-commit paths ----

    private async Task PostCommitAsync(List<IDomainEvent> domainEvents, Guid txId)
    {
        // Prefer outbox processor if available/enabled
        if (_outboxService is not null && _options.Value.EnableOutboxProcessing && _outboxProcessor is not null)
        {
            // fire-and-forget; do not bind to request token
            _ = Task.Run(async () =>
            {
                try
                {
                    var delay = _options.Value.OutboxProcessingDelay;
                    if (delay > TimeSpan.Zero) await Task.Delay(delay);
                    if (_options.Value.TriggerImmediateProcessing)
                    {
                        await _outboxProcessor.ProcessPendingAsync(txId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Outbox processing trigger failed for tx {TxId}", txId);
                }
            }, CancellationToken.None);

            return;
        }

        // Legacy direct dispatch AFTER commit (best-effort)
        if (domainEvents.Count == 0) return;

        _ = Task.Run(async () =>
        {
            try
            {
                await _domainEventDispatcher.DispatchAsync(domainEvents, CancellationToken.None);
                _logger.LogDebug("Dispatched {Count} domain events (legacy path) for tx {TxId}", domainEvents.Count, txId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Domain event dispatch failed post-commit for tx {TxId}", txId);
            }
        }, CancellationToken.None);
    }

    // ---- Result helpers ----

    private static TResponse FailureResult(Error error)
    {
        var t = typeof(TResponse);
        if (t == typeof(Result)) return (TResponse)(object)Result.Failure(error);

        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var failure = t.GetMethod("Failure", BindingFlags.Public | BindingFlags.Static, new[] { typeof(Error) });
            if (failure is not null)
                return (TResponse)failure.Invoke(null, new object[] { error })!;
        }

        throw new InvalidOperationException($"TransactionBehavior requires TResponse : Result or Result<T>. Found {t.Name}");
    }
}

/// <summary>
/// Optional unified interface for aggregates exposing domain events.
/// Prefer your actual domain base type here to avoid reflection fallback.
/// </summary>
public interface IAggregateRootWithEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}