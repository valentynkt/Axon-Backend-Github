namespace BuildingBlocks.Core.Domain.Events;

/// <summary>
/// Marker for domain events raised inside a bounded context.
/// Pure domain contract (no MediatR/transport).
/// </summary>
public interface IDomainEvent
{
    /// <summary>Unique event identifier.</summary>
    Guid EventId { get; }

    /// <summary>UTC timestamp when the event occurred.</summary>
    DateTime OccurredAt { get; }

    /// <summary>Schema/version of the event payload. Defaults to 1.</summary>
    int Version { get; }

    /// <summary>Stable name for routing/diagnostics (defaults to concrete type name).</summary>
    string Name { get; }
}