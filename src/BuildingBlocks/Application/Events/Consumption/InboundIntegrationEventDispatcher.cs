using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BuildingBlocks.Core.Abstractions.Events;
using BuildingBlocks.Application.Events.Enveloping;
using BuildingBlocks.Application.Events.Serialization;
using BuildingBlocks.Application.Events.Consumption.Inbox;
using BuildingBlocks.Application.Events.Consumption.Errors;
using BuildingBlocks.Application.Events.Consumption.Retry;
using BuildingBlocks.Application.Events.Consumption.DeadLetter;
using BuildingBlocks.Application.Outbox.Monitoring;

namespace BuildingBlocks.Application.Events.Consumption;

/// <summary>
/// Default implementation of inbound integration event dispatcher.
/// Handles type resolution, deserialization, idempotency checking, handler routing,
/// retry scheduling, and dead-letter handling based on configurable policies.
/// Maintains application purity with no transport dependencies.
/// </summary>
public sealed class InboundIntegrationEventDispatcher : IInboundIntegrationEventDispatcher
{
    private readonly IEventSerializer _serializer;
    private readonly IIntegrationEventHandlerRegistry _handlerRegistry;
    private readonly IInboxStore _inboxStore;
    private readonly IEnvelopeContextAccessor _contextAccessor;
    private readonly IInboundErrorClassifier _errorClassifier;
    private readonly IInboxRetryPolicy _retryPolicy;
    private readonly IInboxDeadLetterStore _deadLetterStore;
    private readonly IOutboxMetrics _metrics;
    private readonly InboxOptions _options;
    private readonly ILogger<InboundIntegrationEventDispatcher> _logger;

    // Cache for reverse type name lookup to avoid reflection on every call
    private readonly Lazy<IReadOnlyDictionary<string, Type>> _typeNameCache;

    public InboundIntegrationEventDispatcher(
        IEventSerializer serializer,
        IIntegrationEventHandlerRegistry handlerRegistry,
        IInboxStore inboxStore,
        IEnvelopeContextAccessor contextAccessor,
        IInboundErrorClassifier errorClassifier,
        IInboxRetryPolicy retryPolicy,
        IInboxDeadLetterStore deadLetterStore,
        IOutboxMetrics metrics,
        IOptions<InboxOptions> options,
        ILogger<InboundIntegrationEventDispatcher> logger)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _handlerRegistry = handlerRegistry ?? throw new ArgumentNullException(nameof(handlerRegistry));
        _inboxStore = inboxStore ?? throw new ArgumentNullException(nameof(inboxStore));
        _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
        _errorClassifier = errorClassifier ?? throw new ArgumentNullException(nameof(errorClassifier));
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        _deadLetterStore = deadLetterStore ?? throw new ArgumentNullException(nameof(deadLetterStore));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Validate options on startup
        var validationError = _options.Validate();
        if (validationError != null)
        {
            throw new ArgumentException($"Invalid InboxOptions: {validationError}", nameof(options));
        }

        _typeNameCache = new Lazy<IReadOnlyDictionary<string, Type>>(BuildTypeNameCache);
    }

    public async Task<InboundDispatchResult> DispatchAsync(
        IntegrationEventEnvelope envelope, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        _metrics.RecordInboundReceived();

        try
        {
            // Extract idempotency key from headers
            var idempotencyKey = ExtractIdempotencyKey(envelope);
            if (string.IsNullOrEmpty(idempotencyKey))
            {
                _logger.LogWarning(
                    "Integration event envelope {EnvelopeId} is missing IdempotencyKey header. Processing without idempotency guarantees.",
                    envelope.EnvelopeId);
                idempotencyKey = envelope.EnvelopeId.ToString();
            }

            // Check idempotency through inbox store
            var inboxResult = await _inboxStore.TryBeginAsync(
                idempotencyKey, 
                envelope.OccurredAtUtc, 
                cancellationToken);

            if (inboxResult == InboxStartResult.AlreadyCompleted)
            {
                _metrics.RecordInboundDuplicate();
                _logger.LogDebug(
                    "Integration event with idempotency key {IdempotencyKey} has already been processed. Skipping.",
                    idempotencyKey);
                return InboundDispatchResult.AlreadyProcessed;
            }

            if (inboxResult == InboxStartResult.InFlight)
            {
                _metrics.RecordInboundDuplicate();
                _logger.LogDebug(
                    "Integration event with idempotency key {IdempotencyKey} is currently being processed. Skipping.",
                    idempotencyKey);
                return InboundDispatchResult.AlreadyProcessed;
            }

            // Resolve event type from type name
            var eventType = ResolveEventType(envelope.Type);
            if (eventType == null)
            {
                await _inboxStore.FailAsync(idempotencyKey, $"Unknown event type: {envelope.Type}", cancellationToken);
                _metrics.RecordInboundHandlerFailed();
                _logger.LogWarning(
                    "Could not resolve event type {EventType} for envelope {EnvelopeId}",
                    envelope.Type, envelope.EnvelopeId);
                return InboundDispatchResult.UnknownType;
            }

            // Deserialize event payload
            IIntegrationEvent integrationEvent;
            try
            {
                var serializedData = _serializer.Serialize(envelope.Data);
                var deserializedEvent = _serializer.Deserialize(serializedData, eventType);
                integrationEvent = (IIntegrationEvent)deserializedEvent!;
            }
            catch (Exception ex)
            {
                await _inboxStore.FailAsync(idempotencyKey, $"Deserialization failed: {ex.Message}", cancellationToken);
                _metrics.RecordInboundHandlerFailed();
                _logger.LogError(ex,
                    "Failed to deserialize event payload for type {EventType} in envelope {EnvelopeId}",
                    envelope.Type, envelope.EnvelopeId);
                return InboundDispatchResult.BadPayload;
            }

            // Get handlers for this event type
            var handlers = _handlerRegistry.GetHandlers(eventType).ToList();
            if (handlers.Count == 0)
            {
                await _inboxStore.CompleteAsync(idempotencyKey, cancellationToken);
                _metrics.RecordInboundHandled();
                _logger.LogDebug(
                    "No handlers registered for event type {EventType}. Treating as success.",
                    envelope.Type);
                return InboundDispatchResult.NoHandlers;
            }

            // Execute handlers within envelope context
            var context = CreateEnvelopeContext(envelope);
            using var contextScope = _contextAccessor.Push(context);

            try
            {
                // Execute handlers sequentially to maintain ordering
                foreach (var handler in handlers)
                {
                    await handler.HandleAsync(integrationEvent, cancellationToken);
                }

                await _inboxStore.CompleteAsync(idempotencyKey, cancellationToken);
                _metrics.RecordInboundHandled();
                
                _logger.LogDebug(
                    "Successfully processed integration event {EventType} with {HandlerCount} handlers for envelope {EnvelopeId}",
                    envelope.Type, handlers.Count, envelope.EnvelopeId);
                
                return InboundDispatchResult.Success;
            }
            catch (Exception ex)
            {
                return await HandleFailureAsync(
                    idempotencyKey, envelope, eventType, ex, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _metrics.RecordInboundHandlerFailed();
            _logger.LogError(ex,
                "Unexpected error during inbound event dispatch for envelope {EnvelopeId}",
                envelope.EnvelopeId);
            return InboundDispatchResult.HandlerFailed;
        }
    }

    private static string? ExtractIdempotencyKey(IntegrationEventEnvelope envelope)
    {
        return envelope.Headers.TryGetValue(IntegrationEventHeaders.IdempotencyKey, out var value)
            ? value?.ToString()
            : null;
    }

    private Type? ResolveEventType(string typeName)
    {
        return _typeNameCache.Value.TryGetValue(typeName, out var type) ? type : null;
    }

    private static IntegrationEnvelopeContext CreateEnvelopeContext(IntegrationEventEnvelope envelope)
    {
        var metadata = new Dictionary<string, object>();
        
        // Copy relevant headers to context metadata
        foreach (var header in envelope.Headers)
        {
            metadata[header.Key] = header.Value!;
        }

        return new IntegrationEnvelopeContext(
            TraceId: envelope.Headers.TryGetValue(IntegrationEventHeaders.TraceId, out var traceId) 
                ? traceId?.ToString() 
                : null,
            RequestId: envelope.Headers.TryGetValue(IntegrationEventHeaders.CorrelationId, out var correlationId) 
                && Guid.TryParse(correlationId?.ToString(), out var requestGuid) 
                ? requestGuid 
                : null,
            TenantId: envelope.Headers.TryGetValue(IntegrationEventHeaders.TenantId, out var tenantId)
                ? tenantId?.ToString()
                : null,
            Metadata: metadata,
            OutboxEntryId: envelope.Headers.TryGetValue(IntegrationEventHeaders.OutboxEntryId, out var outboxId)
                && Guid.TryParse(outboxId?.ToString(), out var outboxGuid)
                ? outboxGuid
                : null,
            TransactionId: envelope.Headers.TryGetValue(IntegrationEventHeaders.TransactionId, out var transactionId)
                && Guid.TryParse(transactionId?.ToString(), out var transactionGuid)
                ? transactionGuid
                : null);
    }

    /// <summary>
    /// Builds a reverse lookup cache from stable type names to CLR types.
    /// Scans all loaded assemblies for types implementing IIntegrationEvent.
    /// </summary>
    private static Dictionary<string, Type> BuildTypeNameCache()
    {
        var cache = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        var integrationEventType = typeof(IIntegrationEvent);

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (!integrationEventType.IsAssignableFrom(type) || type.IsInterface || type.IsAbstract)
                        continue;

                    // Check for IntegrationEventNameAttribute
                    var attribute = type.GetCustomAttribute<IntegrationEventNameAttribute>();
                    var typeName = attribute?.Name ?? type.FullName ?? type.Name;

                    // Handle potential duplicates by preferring attributed types
                    if (cache.TryGetValue(typeName, out var existingType))
                    {
                        var existingHasAttribute = existingType.GetCustomAttribute<IntegrationEventNameAttribute>() != null;
                        var currentHasAttribute = attribute != null;

                        // Prefer attributed types over non-attributed
                        if (currentHasAttribute && !existingHasAttribute)
                        {
                            cache[typeName] = type;
                        }
                        // If both or neither have attributes, log warning about conflict
                        else if (currentHasAttribute == existingHasAttribute)
                        {
                            // Note: In production, you might want to throw or use a more sophisticated logging approach
                            Console.WriteLine($"Warning: Duplicate type name '{typeName}' found for types {existingType.FullName} and {type.FullName}");
                        }
                    }
                    else
                    {
                        cache[typeName] = type;
                    }
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // Skip assemblies that can't be loaded
                continue;
            }
        }

        return cache;
    }

    /// <summary>
    /// Handles handler failures with retry and dead-letter logic based on error classification.
    /// </summary>
    private async Task<InboundDispatchResult> HandleFailureAsync(
        string idempotencyKey,
        IntegrationEventEnvelope envelope,
        Type eventType,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Classify the error to determine handling strategy
        var errorKind = _errorClassifier.Classify(exception, envelope, eventType);
        
        _logger.LogDebug(
            "Handler failure classified as {ErrorKind} for event {EventType} in envelope {EnvelopeId}: {ErrorMessage}",
            errorKind, envelope.Type, envelope.EnvelopeId, exception.Message);

        // Handle unclassified errors based on configuration
        if (errorKind == InboundErrorKind.Unclassified)
        {
            errorKind = _options.UnclassifiedIsPermanent 
                ? InboundErrorKind.Permanent 
                : InboundErrorKind.Transient;

            _logger.LogDebug(
                "Unclassified error treated as {EffectiveErrorKind} based on configuration for envelope {EnvelopeId}",
                errorKind, envelope.EnvelopeId);
        }

        // Handle permanent failures immediately
        if (errorKind == InboundErrorKind.Permanent)
        {
            return await HandlePermanentFailureAsync(
                idempotencyKey, envelope, eventType, exception, cancellationToken);
        }

        // Handle transient failures with retry logic
        return await HandleTransientFailureAsync(
            idempotencyKey, envelope, eventType, exception, cancellationToken);
    }

    /// <summary>
    /// Handles permanent failures by immediately moving to dead letter.
    /// </summary>
    private async Task<InboundDispatchResult> HandlePermanentFailureAsync(
        string idempotencyKey,
        IntegrationEventEnvelope envelope,
        Type eventType,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var attemptCount = GetDeliveryAttempt(envelope);
        
        await MoveToDeadLetterAsync(
            idempotencyKey, envelope, eventType.Name, exception, attemptCount, cancellationToken);

        await _inboxStore.FailAsync(idempotencyKey, $"Permanent failure: {exception.Message}", cancellationToken);
        _metrics.RecordInboundPermanentFailure();

        _logger.LogWarning(
            "Permanent failure for event {EventType} in envelope {EnvelopeId} moved to dead letter: {ErrorMessage}",
            envelope.Type, envelope.EnvelopeId, exception.Message);

        return InboundDispatchResult.HandlerFailed;
    }

    /// <summary>
    /// Handles transient failures with retry policy evaluation.
    /// </summary>
    private async Task<InboundDispatchResult> HandleTransientFailureAsync(
        string idempotencyKey,
        IntegrationEventEnvelope envelope,
        Type eventType,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var currentAttempt = GetDeliveryAttempt(envelope);
        var nextAttempt = currentAttempt + 1;

        // Evaluate retry policy
        var retryDecision = _retryPolicy.Compute(
            nextAttempt, envelope.OccurredAtUtc, DateTime.UtcNow, _options);

        if (retryDecision.MovedToDeadLetter)
        {
            // Exceeded retry limit - move to dead letter
            await MoveToDeadLetterAsync(
                idempotencyKey, envelope, eventType.Name, exception, currentAttempt, cancellationToken);

            await _inboxStore.FailAsync(idempotencyKey, $"Retry limit exceeded: {exception.Message}", cancellationToken);
            _metrics.RecordInboundMovedToDeadLetter();

            _logger.LogWarning(
                "Retry limit exceeded for event {EventType} in envelope {EnvelopeId} after {AttemptCount} attempts. Moved to dead letter: {ErrorMessage}",
                envelope.Type, envelope.EnvelopeId, currentAttempt, exception.Message);

            return InboundDispatchResult.HandlerFailed;
        }

        if (retryDecision.ShouldRetry)
        {
            // Schedule retry
            await _inboxStore.FailAsync(idempotencyKey, 
                $"Transient failure (attempt {currentAttempt}): {exception.Message}", cancellationToken);
            
            _metrics.RecordInboundRetryScheduled(retryDecision.Delay);

            _logger.LogInformation(
                "Transient failure for event {EventType} in envelope {EnvelopeId} scheduled for retry {NextAttempt}/{MaxAttempts} after {Delay}: {ErrorMessage}",
                envelope.Type, envelope.EnvelopeId, nextAttempt, _options.MaxAttempts, retryDecision.Delay, exception.Message);

            return InboundDispatchResult.HandlerFailed;
        }

        // Should not retry (edge case)
        await _inboxStore.FailAsync(idempotencyKey, $"No retry decision: {exception.Message}", cancellationToken);
        _metrics.RecordInboundHandlerFailed();

        _logger.LogError(
            "No retry decision for event {EventType} in envelope {EnvelopeId}: {ErrorMessage}",
            envelope.Type, envelope.EnvelopeId, exception.Message);

        return InboundDispatchResult.HandlerFailed;
    }

    /// <summary>
    /// Moves a failed message to the dead letter store with comprehensive error details.
    /// </summary>
    private async Task MoveToDeadLetterAsync(
        string idempotencyKey,
        IntegrationEventEnvelope envelope,
        string eventTypeName,
        Exception exception,
        int attemptCount,
        CancellationToken cancellationToken)
    {
        var deadLetterEntry = InboxDeadLetterEntry.FromFailure(
            idempotencyKey: idempotencyKey,
            eventTypeName: eventTypeName,
            envelope: envelope,
            exception: exception,
            attemptCount: attemptCount,
            firstFailedAt: envelope.OccurredAtUtc, // Approximate - could track actual first failure
            deadLetteredAt: DateTime.UtcNow
        );

        try
        {
            await _deadLetterStore.AddAsync(deadLetterEntry, cancellationToken);
        }
        catch (Exception deadLetterEx)
        {
            // Log dead letter store failure but don't fail the operation
            _logger.LogError(deadLetterEx,
                "Failed to store dead letter entry for envelope {EnvelopeId}. Original error: {OriginalError}",
                envelope.EnvelopeId, exception.Message);
        }
    }

    /// <summary>
    /// Extracts the delivery attempt number from envelope headers.
    /// </summary>
    private static int GetDeliveryAttempt(IntegrationEventEnvelope envelope)
    {
        if (envelope.Headers.TryGetValue(IntegrationEventHeaders.DeliveryAttempt, out var attemptValue) &&
            int.TryParse(attemptValue?.ToString(), out var attempt) &&
            attempt > 0)
        {
            return attempt;
        }

        // Default to 1 if header is missing or invalid
        return 1;
    }
}