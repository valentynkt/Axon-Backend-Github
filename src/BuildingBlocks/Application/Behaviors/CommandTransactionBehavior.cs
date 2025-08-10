using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Application.Events.Collecting;
using BuildingBlocks.Application.Events.Dispatching;
using BuildingBlocks.Application.Events.Notifications;
using IsolationLevel = System.Data.IsolationLevel;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// Thin, KISS/YAGNI transaction wrapper for commands.
/// MassTransit-first:
/// - Save changes inside a single EF transaction
/// - Collect domain events
/// - Map & publish integration events *before commit* (so MT EF Outbox can capture them)
/// - Commit
/// - Publish in-process (MediatR) notifications after commit
/// No custom outbox tables, schedulers, or retries here — Infra (MassTransit EF Outbox) handles reliability.
/// </summary>
public sealed class CommandTransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull, ICommand<TResponse>
    where TResponse : IResult
{
    private readonly DbContext _dbContext;
    private readonly IDomainEventCollector _domainEventCollector;
    private readonly IIntegrationEventDispatcher _integrationDispatcher;
    private readonly IPostCommitDomainEventPublisher _postCommitPublisher;
    private readonly ILogger<CommandTransactionBehavior<TRequest, TResponse>> _logger;

    public CommandTransactionBehavior(
        DbContext dbContext,
        IDomainEventCollector domainEventCollector,
        IIntegrationEventDispatcher integrationDispatcher,
        IPostCommitDomainEventPublisher postCommitPublisher,
        ILogger<CommandTransactionBehavior<TRequest, TResponse>> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _domainEventCollector = domainEventCollector ?? throw new ArgumentNullException(nameof(domainEventCollector));
        _integrationDispatcher = integrationDispatcher ?? throw new ArgumentNullException(nameof(integrationDispatcher));
        _postCommitPublisher = postCommitPublisher ?? throw new ArgumentNullException(nameof(postCommitPublisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // If someone already started a transaction, don't nest — just run next().
        if (_dbContext.Database.CurrentTransaction is not null)
        {
            _logger.LogDebug("Existing transaction detected for {Command}. Skipping behavior transaction.", typeof(TRequest).Name);
            return await next();
        }

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

            // Persist changes (no-op if nothing to save)
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Collect and clear domain events AFTER SaveChanges, BEFORE commit
            var domainEvents = _domainEventCollector.Collect(_dbContext, clear: true);

            // Cross-boundary: map & publish integration events BEFORE COMMIT
            // MassTransit EF Outbox will capture these publishes inside the same transaction.
            if (domainEvents.Count > 0)
            {
                await _integrationDispatcher.SendAsync(domainEvents, cancellationToken: cancellationToken);
                _logger.LogDebug("Published {Count} integration event(s) for {Command} before commit.",
                    domainEvents.Count, typeof(TRequest).Name);
            }

            // Commit business data + outbox entries atomically
            await tx.CommitAsync(cancellationToken);
            _logger.LogInformation("Transaction committed for {Command}.", typeof(TRequest).Name);

            // In-process orchestration (Lane A) AFTER COMMIT
            if (domainEvents.Count > 0)
            {
                await _postCommitPublisher.PublishAsync(domainEvents, cancellationToken);
                _logger.LogDebug("Published {Count} post-commit domain notification(s) for {Command}.",
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
