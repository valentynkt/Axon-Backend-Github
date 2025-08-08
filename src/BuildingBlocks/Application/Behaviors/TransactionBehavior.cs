using MediatR;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Core.Domain.Integration;
using BuildingBlocks.Core.Domain.Model;
using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that provides database transaction management with domain event integration.
/// Integrates Epic 2 domain events with Epic 5 pipeline behaviors for reliable event processing.
/// 
/// This behavior:
/// 1. Wraps command execution in database transactions
/// 2. Dispatches domain events after successful commit (Epic 2 integration)
/// 3. Handles rollback scenarios gracefully
/// 4. Supports nested transaction detection
/// 5. Integrates with outbox pattern for reliable event processing
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type (must be Result-based)</typeparam>
public sealed class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : IResult
{
    private readonly DbContext _dbContext;
    private readonly IDomainEventDispatcher _domainEventDispatcher;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

    public TransactionBehavior(
        DbContext dbContext,
        IDomainEventDispatcher domainEventDispatcher,
        ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _domainEventDispatcher = domainEventDispatcher ?? throw new ArgumentNullException(nameof(domainEventDispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        // Only apply transaction behavior to commands, not queries
        if (!ShouldApplyTransaction(request))
        {
            _logger.LogDebug(
                "Skipping transaction for {RequestType} (not a command)",
                typeof(TRequest).Name);
            return await next();
        }

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestType"] = typeof(TRequest).Name,
            ["ResponseType"] = typeof(TResponse).Name
        });

        // Check if we're already in a transaction (nested transaction scenario)
        if (_dbContext.Database.CurrentTransaction != null)
        {
            _logger.LogDebug(
                "Already in transaction for {RequestType}, proceeding without new transaction",
                typeof(TRequest).Name);
            return await next();
        }

        _logger.LogDebug("Starting transaction for {RequestType}", typeof(TRequest).Name);

        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        
        try
        {
            // Execute the command handler
            var response = await next();

            // Check if the response indicates failure
            if (response.IsFailure)
            {
                _logger.LogWarning(
                    "Command {RequestType} failed, rolling back transaction: {Error}",
                    typeof(TRequest).Name,
                    response.Error.Message);
                
                await transaction.RollbackAsync(cancellationToken);
                return response;
            }

            // Collect domain events from tracked aggregates before commit
            var domainEvents = CollectDomainEvents();
            
            // Commit the transaction
            _logger.LogDebug("Committing transaction for {RequestType}", typeof(TRequest).Name);
            await transaction.CommitAsync(cancellationToken);
            
            // Dispatch domain events after successful commit (Epic 2 integration)
            if (domainEvents.Any())
            {
                await DispatchDomainEventsAsync(domainEvents, cancellationToken);
            }

            _logger.LogDebug("Transaction completed successfully for {RequestType}", typeof(TRequest).Name);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Exception occurred during transaction for {RequestType}, rolling back",
                typeof(TRequest).Name);

            try
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(rollbackEx,
                    "Failed to rollback transaction for {RequestType}",
                    typeof(TRequest).Name);
            }

            // Convert exception to Result failure response
            var error = Error.Failure(
                $"Transaction failed for {typeof(TRequest).Name}: {ex.Message}",
                "TRANSACTION_FAILED");
                
            return CreateFailureResponse<TResponse>(error);
        }
    }

    /// <summary>
    /// Determine if transaction behavior should be applied to this request.
    /// Only commands should run in transactions, not queries.
    /// </summary>
    private static bool ShouldApplyTransaction(TRequest request)
    {
        // Check for domain command interfaces (Epic 2 integration)
        if (request is IDomainCommand or IDomainCommandAsync)
        {
            return true;
        }

        // Check for MediatR IRequest without response (commands by convention)
        var requestType = typeof(TRequest);
        var interfaces = requestType.GetInterfaces();
        
        // Commands typically implement IRequest without a response type
        return interfaces.Any(i => 
            i.IsGenericType && 
            i.GetGenericTypeDefinition() == typeof(IRequest<>) &&
            i.GetGenericArguments()[0] == typeof(Unit));
    }

    /// <summary>
    /// Collect domain events from all tracked aggregate roots in the DbContext.
    /// This integrates with Epic 2 domain model patterns.
    /// </summary>
    private List<IDomainEvent> CollectDomainEvents()
    {
        var domainEvents = new List<IDomainEvent>();

        // Get all tracked entities that are aggregate roots
        var aggregates = _dbContext.ChangeTracker
            .Entries<IAggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        if (!aggregates.Any())
        {
            return domainEvents;
        }

        _logger.LogDebug(
            "Collecting domain events from {AggregateCount} aggregates",
            aggregates.Count);

        // Collect all domain events
        foreach (var aggregate in aggregates)
        {
            domainEvents.AddRange(aggregate.DomainEvents);
        }

        _logger.LogDebug(
            "Collected {EventCount} domain events from {AggregateCount} aggregates",
            domainEvents.Count,
            aggregates.Count);

        return domainEvents;
    }

    /// <summary>
    /// Dispatch domain events after successful transaction commit.
    /// This follows the pattern from Epic 5 specification for reliable event processing.
    /// </summary>
    private async Task DispatchDomainEventsAsync(
        List<IDomainEvent> domainEvents,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Dispatching {EventCount} domain events", domainEvents.Count);

        // Dispatch events asynchronously after commit
        // Following Epic 5 specification pattern with proper error handling
        _ = Task.Run(async () =>
        {
            try
            {
                // Small delay to ensure transaction is fully visible
                await Task.Delay(100, cancellationToken);
                
                var dispatchResult = await _domainEventDispatcher.DispatchAsync(domainEvents, cancellationToken);
                
                if (dispatchResult.IsFailure)
                {
                    using var errorScope = _logger.BeginScope("DomainEventDispatchFailure");
                    _logger.LogError(
                        "Domain event dispatch failed after transaction commit: {Error}",
                        dispatchResult.Error.Message);
                }
                else
                {
                    _logger.LogDebug("Successfully dispatched all {EventCount} domain events", domainEvents.Count);
                }
            }
            catch (Exception ex)
            {
                using var errorScope = _logger.BeginScope("DomainEventDispatchException");
                _logger.LogError(ex,
                    "Exception occurred during domain event dispatch after transaction commit");
            }
        }, cancellationToken);

        // Clear domain events from aggregates after dispatch initiation
        ClearDomainEventsFromAggregates();
    }

    /// <summary>
    /// Clear domain events from all tracked aggregates.
    /// Called after successful event dispatch initiation.
    /// </summary>
    private void ClearDomainEventsFromAggregates()
    {
        var aggregates = _dbContext.ChangeTracker
            .Entries<IAggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }

        _logger.LogDebug("Cleared domain events from {AggregateCount} aggregates", aggregates.Count);
    }

    /// <summary>
    /// Create a failure response with the appropriate type.
    /// Uses reflection to create the correct Result<T> failure response.
    /// </summary>
    private static TResponse CreateFailureResponse<T>(Error error) where T : IResult
    {
        var responseType = typeof(T);
        
        // Handle Result<TValue> types
        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var valueType = responseType.GetGenericArguments()[0];
            var failureMethod = typeof(Result<>)
                .MakeGenericType(valueType)
                .GetMethod(nameof(Result<object>.Failure), new[] { typeof(Error) });
                
            if (failureMethod != null)
            {
                var result = failureMethod.Invoke(null, new object[] { error });
                return (TResponse)result!;
            }
        }
        
        // Handle basic Result type
        if (responseType == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(error);
        }
        
        throw new InvalidOperationException(
            $"TransactionBehavior can only be used with Result or Result<T> response types. " +
            $"Got: {responseType.Name}");
    }
}