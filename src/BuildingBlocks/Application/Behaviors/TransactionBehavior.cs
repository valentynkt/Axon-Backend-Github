using System.Data;
using System.Diagnostics;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Core.Domain.Integration;
using BuildingBlocks.Core.Domain.Model;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Application.Outbox;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

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
    private readonly IOutboxService? _outboxService;  // Enhanced: Outbox service for reliable event storage
    private readonly IOutboxProcessor? _outboxProcessor;  // Background processor for event processing
    private readonly IOptions<TransactionOptions> _options;  // Configuration
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

    public TransactionBehavior(
        DbContext dbContext,
        IDomainEventDispatcher domainEventDispatcher,
        ILogger<TransactionBehavior<TRequest, TResponse>> logger,
        IOutboxService? outboxService = null,  // Outbox service (optional for backward compatibility)
        IOutboxProcessor? outboxProcessor = null,  // Background processor (optional)
        IOptions<TransactionOptions>? options = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _domainEventDispatcher = domainEventDispatcher ?? throw new ArgumentNullException(nameof(domainEventDispatcher));
        _outboxService = outboxService; // Can be null for backward compatibility
        _outboxProcessor = outboxProcessor; // Can be null
        _options = options ?? Options.Create(new TransactionOptions());
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
            return await next(cancellationToken);
        }

        // Extract metadata and trace context for enhanced transaction handling
        var (traceId, requestId, metadata) = TransactionBehavior<TRequest, TResponse>.ExtractRequestContext(request);
        var transactionId = Guid.NewGuid();

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestType"] = typeof(TRequest).Name,
            ["ResponseType"] = typeof(TResponse).Name,
            ["TransactionId"] = transactionId,
            ["TraceId"] = traceId ?? "none",
            ["RequestId"] = requestId ?? Guid.Empty
        });

        // Check if we're already in a transaction (nested transaction scenario)
        if (_dbContext.Database.CurrentTransaction != null)
        {
            _logger.LogDebug(
                "Already in transaction for {RequestType}, proceeding without new transaction",
                typeof(TRequest).Name);
            return await next(cancellationToken);
        }

        // Create activity for distributed tracing
        using var activity = Activity.Current?.Source.StartActivity("DatabaseTransaction");
        activity?.SetTag("transaction.id", transactionId);
        activity?.SetTag("transaction.type", "command");
        activity?.SetTag("request.type", typeof(TRequest).Name);
        activity?.SetTag("trace.id", traceId);

        // Add metadata tags to activity
        if (metadata?.Any() == true)
        {
            foreach (var (key, value) in metadata)
            {
                activity?.SetTag($"metadata.{key}", value?.ToString());
            }
        }

        // Determine isolation level based on metadata and configuration
        var isolationLevel = GetIsolationLevel(metadata);
        
        _logger.LogDebug(
            "Starting transaction {TransactionId} for {RequestType} with isolation level {IsolationLevel} (Trace: {TraceId})",
            transactionId, typeof(TRequest).Name, isolationLevel, traceId);

        var stopwatch = Stopwatch.StartNew();
        using var transaction = await _dbContext.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
        
        try
        {
            // Execute the command handler
            var response = await next(cancellationToken);

            // Check if the response indicates failure
            if (response.IsFailure)
            {
                stopwatch.Stop();
                _logger.LogWarning(
                    "Command {RequestType} failed, rolling back transaction {TransactionId}: {Error} (elapsed: {ElapsedMs}ms)",
                    typeof(TRequest).Name, transactionId, response.Error.Message, stopwatch.ElapsedMilliseconds);
                
                activity?.SetStatus(ActivityStatusCode.Error, response.Error.Message);
                activity?.SetTag("transaction.duration_ms", stopwatch.ElapsedMilliseconds);
                
                await transaction.RollbackAsync(cancellationToken);
                return response;
            }

            // Collect domain events from tracked aggregates before commit
            var domainEvents = CollectDomainEvents();
            
            // Store events in outbox if outbox service is available, otherwise use legacy dispatch
            if (_outboxService != null && _options.Value.EnableOutboxProcessing && domainEvents.Count != 0)
            {
                var storeResult = await _outboxService.StoreEventsAsync(
                    domainEvents,
                    transactionId,
                    traceId,
                    requestId,
                    metadata,
                    cancellationToken);

                if (storeResult.IsFailure)
                {
                    _logger.LogError(
                        "Failed to store {EventCount} events in outbox for transaction {TransactionId}: {Error}",
                        domainEvents.Count, transactionId, storeResult.Error.Message);
                    
                    activity?.SetStatus(ActivityStatusCode.Error, storeResult.Error.Message);
                    await transaction.RollbackAsync(cancellationToken);
                    return CreateFailureResponse<TResponse>(storeResult.Error);
                }

                _logger.LogDebug(
                    "Stored {EventCount} domain events in outbox for transaction {TransactionId}",
                    storeResult.Value, transactionId);
                
                activity?.SetTag("outbox.events_stored", storeResult.Value);
            }
            
            // Commit the transaction
            _logger.LogDebug("Committing transaction {TransactionId} for {RequestType}", transactionId, typeof(TRequest).Name);
            await transaction.CommitAsync(cancellationToken);
            stopwatch.Stop();
            
            // Enhanced success logging with performance metrics
            _logger.LogInformation(
                "Transaction {TransactionId} committed successfully for {RequestType} in {ElapsedMs}ms (Trace: {TraceId}, Events: {EventCount})",
                transactionId, typeof(TRequest).Name, stopwatch.ElapsedMilliseconds, traceId, domainEvents.Count);
            
            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("transaction.duration_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetTag("transaction.events_count", domainEvents.Count);
            activity?.SetTag("transaction.committed", true);

            // Post-commit processing
            await HandlePostCommitProcessing(domainEvents, transactionId, cancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex,
                "Exception occurred during transaction {TransactionId} for {RequestType} after {ElapsedMs}ms, rolling back",
                transactionId, typeof(TRequest).Name, stopwatch.ElapsedMilliseconds);

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("transaction.duration_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetTag("transaction.failed", true);
            activity?.AddException(ex);

            try
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogDebug("Successfully rolled back transaction {TransactionId}", transactionId);
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(rollbackEx,
                    "Failed to rollback transaction {TransactionId} for {RequestType}",
                    transactionId, typeof(TRequest).Name);
            }

            // Convert exception to Result failure response
            var error = Error.Internal(
                $"Transaction failed for {typeof(TRequest).Name}: {ex.Message}",
                "TRANSACTION_FAILED",
                ex);
                
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
            .Entries<IAggregateRoot<>>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        if (aggregates.Count == 0)
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
            .Entries<IAggregateRoot<>>()
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
    /// Extract request context information for enhanced transaction handling.
    /// Attempts to extract trace ID, request ID, and metadata from the request.
    /// </summary>
    private static (string? TraceId, Guid? RequestId, IReadOnlyDictionary<string, object>? Metadata) ExtractRequestContext(TRequest request)
    {
        string? traceId = null;
        Guid? requestId = null;
        IReadOnlyDictionary<string, object>? metadata = null;

        // Try to get trace context from Activity
        if (Activity.Current != null)
        {
            traceId = Activity.Current.TraceId.ToString();
        }

        // Try to extract from request if it has properties (using reflection)
        var requestType = typeof(TRequest);
        
        // Look for common property names
        var traceIdProperty = requestType.GetProperty("TraceId");
        if (traceIdProperty?.GetValue(request) is string extractedTraceId)
        {
            traceId = extractedTraceId;
        }

        var requestIdProperty = requestType.GetProperty("RequestId");
        if (requestIdProperty?.GetValue(request) is Guid extractedRequestId)
        {
            requestId = extractedRequestId;
        }

        var metadataProperty = requestType.GetProperty("Metadata");
        if (metadataProperty?.GetValue(request) is IReadOnlyDictionary<string, object> extractedMetadata)
        {
            metadata = extractedMetadata;
        }

        return (traceId, requestId, metadata);
    }

    /// <summary>
    /// Determine transaction isolation level based on request metadata and configuration.
    /// </summary>
    private IsolationLevel GetIsolationLevel(IReadOnlyDictionary<string, object>? metadata)
    {
        if (!_options.Value.UseMetadataAwareIsolation || metadata == null)
        {
            return _options.Value.DefaultIsolationLevel;
        }

        // Check metadata for explicit isolation level
        if (metadata.TryGetValue("IsolationLevel", out var levelValue) &&
            Enum.TryParse<IsolationLevel>(levelValue.ToString(), out var isolationLevel))
        {
            _logger.LogDebug("Using explicit isolation level {IsolationLevel} from metadata", isolationLevel);
            return isolationLevel;
        }

        // Check for high-consistency requirements
        if (metadata.ContainsKey("RequireSerializable") || 
            metadata.ContainsKey("HighConsistency"))
        {
            _logger.LogDebug("Using Serializable isolation level due to high consistency requirement");
            return IsolationLevel.Serializable;
        }

        // Check for tenant-specific settings
        if (metadata.TryGetValue("TenantId", out var tenantIdValue))
        {
            var tenantId = tenantIdValue?.ToString();
            if (tenantId == "high-consistency-tenant")
            {
                _logger.LogDebug("Using RepeatableRead isolation level for high-consistency tenant {TenantId}", tenantId);
                return IsolationLevel.RepeatableRead;
            }
        }

        // Check for financial or critical operations
        if (metadata.ContainsKey("FinancialOperation") || 
            metadata.ContainsKey("CriticalOperation"))
        {
            _logger.LogDebug("Using RepeatableRead isolation level for critical operation");
            return IsolationLevel.RepeatableRead;
        }

        return _options.Value.DefaultIsolationLevel;
    }

    /// <summary>
    /// Handle post-commit processing including event dispatch and outbox triggering.
    /// </summary>
    private async Task HandlePostCommitProcessing(
        List<IDomainEvent> domainEvents,
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        // If outbox service is available and enabled, trigger outbox processing
        if (_outboxService != null && _options.Value.EnableOutboxProcessing && _options.Value.TriggerImmediateProcessing)
        {
            TriggerOutboxProcessing(transactionId);
        }
        // Fallback to legacy domain event dispatch if no outbox service
        else if (domainEvents.Count != 0)
        {
            await DispatchDomainEventsAsync(domainEvents, cancellationToken);
        }
    }

    /// <summary>
    /// Trigger outbox processing after successful transaction commit.
    /// Fire-and-forget pattern to avoid blocking the main request.
    /// </summary>
    private void TriggerOutboxProcessing(Guid? transactionId = null)
    {
        if (_outboxProcessor is null)
        {
            _logger.LogDebug("No outbox processor available for {RequestType}", typeof(TRequest).Name);
            return;
        }
            
        _logger.LogDebug("Triggering outbox processing for {RequestType} (Transaction: {TransactionId})", 
            typeof(TRequest).Name, transactionId);
        
        // Fire-and-forget pattern - don't await to avoid blocking main request
        _ = Task.Run(async () =>
        {
            try
            {
                // Optional delay for transaction visibility (configurable)
                var processingDelay = _options.Value.OutboxProcessingDelay;
                if (processingDelay > TimeSpan.Zero)
                {
                    await Task.Delay(processingDelay);
                }
                
                // Process specific transaction if provided, otherwise process all pending
                if (transactionId.HasValue)
                {
                    await _outboxProcessor.ProcessPendingAsync(transactionId.Value);
                }
                else
                {
                    await _outboxProcessor.ProcessPendingAsync();
                }
                
                _logger.LogDebug("Outbox processing triggered successfully for {RequestType} (Transaction: {TransactionId})",
                    typeof(TRequest).Name, transactionId);
            }
            catch (Exception ex)
            {
                using (_logger.BeginScope(new Dictionary<string, object>
                {
                    ["Operation"] = "OutboxProcessingTrigger",
                    ["RequestType"] = typeof(TRequest).Name,
                    ["TransactionId"] = transactionId ?? Guid.Empty
                }))
                {
                    _logger.LogError(ex, 
                        "Outbox processing trigger failed after commit for {RequestType} (Transaction: {TransactionId})",
                        typeof(TRequest).Name, transactionId);
                }
            }
        });
    }

    /// <summary>
    /// Create a failure response with the appropriate type.
    /// Uses reflection to create the correct Result&lt;T&gt; failure response.
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