using System.Diagnostics;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Application.Events.Collecting;
using BuildingBlocks.Application.Events.Dispatching;
using BuildingBlocks.Application.Events.Enveloping;
using BuildingBlocks.Application.Events.Notifications;
using IsolationLevel = System.Data.IsolationLevel;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Thin, KISS/YAGNI transaction wrapper for commands.
/// MassTransit-first:
/// - Save changes inside a single EF transaction
/// - Collect domain events
/// - Publish integration events *before commit* (so MT EF Outbox can capture them)
/// - Commit
/// - Publish in-process (MediatR) notifications after commit
/// No custom outbox tables, schedulers, or retries here — Infra (MassTransit EF Outbox) handles reliability.
/// 
/// SAVE PATTERN GUIDANCE:
/// 
/// SINGLE-SAVE Pattern (Used Here):
/// 1. Execute business logic → 2. Collect events → 3. Publish to outbox → 4. SaveChanges() → 5. Commit → 6. Post-commit notifications
/// - PROS: Atomic, simple, MassTransit EF Outbox handles reliability, single transaction
/// - CONS: Integration events published even if SaveChanges fails (but handled by outbox retry)
/// - USE WHEN: Standard CQRS with reliable outbox infrastructure (recommended)
/// 
/// DOUBLE-SAVE Pattern (Alternative):
/// 1. Execute business logic → 2. SaveChanges() → 3. Collect events → 4. Publish to outbox → 5. SaveChanges() → 6. Commit
/// - PROS: Integration events only published after business data is persisted
/// - CONS: More complex, two save operations, potential performance impact
/// - USE WHEN: You need guarantee that business data persists before any integration events
/// 
/// This behavior implements SINGLE-SAVE because:
/// - MassTransit EF Outbox provides reliability guarantees
/// - Simpler transaction semantics
/// - Better performance (one SaveChanges call)
/// - Integration event failures don't affect business data integrity
/// </summary>
public sealed class CommandTransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, ICommand<TResponse>
    where TResponse : IResult
{
    private readonly DbContext _dbContext;
    private readonly IDomainEventCollector _domainEventCollector;
    private readonly IIntegrationEventDispatcher _integrationDispatcher;
    private readonly IPostCommitDomainEventPublisher _postCommitPublisher;
    private readonly IEnvelopeContextAccessor _envelopeContextAccessor;
    private readonly ILogger<CommandTransactionBehavior<TRequest, TResponse>> _logger;

    public CommandTransactionBehavior(
        DbContext dbContext,
        IDomainEventCollector domainEventCollector,
        IIntegrationEventDispatcher integrationDispatcher,
        IPostCommitDomainEventPublisher postCommitPublisher,
        IEnvelopeContextAccessor envelopeContextAccessor,
        ILogger<CommandTransactionBehavior<TRequest, TResponse>> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _domainEventCollector = domainEventCollector ?? throw new ArgumentNullException(nameof(domainEventCollector));
        _integrationDispatcher = integrationDispatcher ?? throw new ArgumentNullException(nameof(integrationDispatcher));
        _postCommitPublisher = postCommitPublisher ?? throw new ArgumentNullException(nameof(postCommitPublisher));
        _envelopeContextAccessor = envelopeContextAccessor ?? throw new ArgumentNullException(nameof(envelopeContextAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Push correlation context for distributed tracing
        var envelopeContext = new IntegrationEnvelopeContext(
            TraceId: Activity.Current?.Id,
            RequestId: Guid.NewGuid(), // Could be extracted from request metadata if available
            TenantId: null, // Could be extracted from current user context if available
            Metadata: null
        );

        using var contextScope = _envelopeContextAccessor.Push(envelopeContext);

        var ownsTransaction = _dbContext.Database.CurrentTransaction is null;

        if (!ownsTransaction)
        {
            _logger.LogDebug("Existing transaction detected for {Command}. Participating without creating new transaction.", typeof(TRequest).Name);
            
            // Execute command handler
            var response = await next();

            if (response.IsFailure)
            {
                _logger.LogWarning("Command {Command} failed under ambient transaction. Error: {Error}",
                    typeof(TRequest).Name, response.Error?.Message);
                return response;
            }

            // Even under ambient transaction, we need to handle eventing
            var domainEvents = _domainEventCollector.Collect(_dbContext, clear: true);

            // Publish integration events to outbox (will be captured by MassTransit EF Outbox in ambient transaction)
            if (domainEvents.Count > 0)
            {
                await _integrationDispatcher.SendAsync(domainEvents, cancellationToken: cancellationToken);
                _logger.LogInformation("Published integration events for {Command} under ambient transaction (from {DomainEventCount} domain events).",
                    typeof(TRequest).Name, domainEvents.Count);
            }

            // Participate in ambient transaction with SaveChanges
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Skip post-commit notifications since we don't own the transaction
            _logger.LogDebug("Skipping post-commit notifications for {Command} (ambient transaction owned elsewhere).", typeof(TRequest).Name);
            
            return response;
        }

        // We own the transaction - full transaction management
        await using var tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            // Execute command handler
            var response = await next();

            if (response.IsFailure)
            {
                _logger.LogWarning("Command {Command} failed. Rolling back transaction. Error: {Error}",
                    typeof(TRequest).Name, response.Error?.Message);
                await tx.RollbackAsync(cancellationToken);
                return response;
            }

            // Collect and clear domain events BEFORE SaveChanges, BEFORE commit
            var domainEvents = _domainEventCollector.Collect(_dbContext, clear: true);

            // Publish integration events BEFORE SaveChanges
            // MassTransit EF Outbox will capture these publishes inside the same transaction.
            if (domainEvents.Count > 0)
            {
                await _integrationDispatcher.SendAsync(domainEvents, cancellationToken: cancellationToken);
                _logger.LogInformation("Published integration events for {Command} before SaveChanges (from {DomainEventCount} domain events).",
                    typeof(TRequest).Name, domainEvents.Count);
            }

            // Persist changes (business data + outbox entries atomically)
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Commit business data + outbox entries atomically
            await tx.CommitAsync(cancellationToken);
            _logger.LogInformation("Transaction committed for {Command}.", typeof(TRequest).Name);

            // In-process orchestration (Lane A) AFTER COMMIT - only when we own the transaction
            if (domainEvents.Count > 0)
            {
                await _postCommitPublisher.PublishAsync(domainEvents, cancellationToken);
                _logger.LogInformation("Published {DomainEventCount} post-commit domain notifications for {Command}.",
                    domainEvents.Count, typeof(TRequest).Name);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transaction failed for {Command}. Rolling back.", typeof(TRequest).Name);
            try { await tx.RollbackAsync(CancellationToken.None); } catch { /* best effort */ }
            return FailureResult(Error.Internal($"Transaction failed for {typeof(TRequest).Name}: {ex.Message}", "TX_FAILED", ex));
        }
    }

    private static TResponse FailureResult(Error error)
    {
        var t = typeof(TResponse);
        if (t == typeof(Result)) return (TResponse)(object)Result.Failure(error);

        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var method = t.GetMethod("Failure", new[] { typeof(Error) });
            if (method is not null)
                return (TResponse)method.Invoke(null, new object[] { error })!;
        }

        throw new InvalidOperationException(
            $"CommandTransactionBehavior requires TResponse : Result or Result<T>. Found {t.Name}");
    }
}
