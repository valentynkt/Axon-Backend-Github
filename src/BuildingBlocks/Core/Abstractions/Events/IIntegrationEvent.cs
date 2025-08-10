namespace BuildingBlocks.Core.Abstractions.Events;

/// <summary>
/// Contract for integration events that cross bounded context boundaries.
/// These events represent business significant happenings that external systems care about.
/// Transport neutral - no dependencies on specific messaging infrastructure.
/// </summary>
public interface IIntegrationEvent : IEvent
{
    /// <summary>
    /// The bounded context or service that published this event.
    /// Used for routing and debugging in distributed scenarios.
    /// </summary>
    string Source { get; }

    /// <summary>
    /// Correlation ID to track related events across boundaries.
    /// Enables distributed tracing and debugging.
    /// </summary>
    string? CorrelationId { get; }

    /// <summary>
    /// Optional metadata for routing, filtering, or processing hints.
    /// Should remain transport-neutral.
    /// </summary>
    IReadOnlyDictionary<string, object>? Metadata { get; }
}