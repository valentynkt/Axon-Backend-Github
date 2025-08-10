using BuildingBlocks.Application.Events.Enveloping;

namespace BuildingBlocks.Application.Events.Consumption;

/// <summary>
/// Transport-neutral dispatcher for inbound integration events.
/// Handles deserialization, idempotency checking, and routing to registered handlers.
/// </summary>
public interface IInboundIntegrationEventDispatcher
{
    /// <summary>
    /// Dispatch an integration event envelope to registered handlers.
    /// Handles type resolution, deserialization, idempotency checking, and handler routing.
    /// </summary>
    /// <param name="envelope">The integration event envelope to dispatch</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result of the dispatch operation</returns>
    Task<InboundDispatchResult> DispatchAsync(
        IntegrationEventEnvelope envelope, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Results of inbound integration event dispatch operations.
/// </summary>
public enum InboundDispatchResult
{
    /// <summary>
    /// Event was successfully processed by all handlers.
    /// </summary>
    Success,
    
    /// <summary>
    /// Event was already processed (duplicate based on IdempotencyKey).
    /// </summary>
    AlreadyProcessed,
    
    /// <summary>
    /// No handlers were registered for this event type (treated as success).
    /// </summary>
    NoHandlers,
    
    /// <summary>
    /// Event type name could not be resolved to a known type.
    /// </summary>
    UnknownType,
    
    /// <summary>
    /// Event payload could not be deserialized.
    /// </summary>
    BadPayload,
    
    /// <summary>
    /// One or more handlers failed during execution.
    /// </summary>
    HandlerFailed
}