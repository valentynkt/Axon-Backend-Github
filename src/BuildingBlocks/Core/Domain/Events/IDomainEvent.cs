using BuildingBlocks.Core.Abstractions.Events;

namespace BuildingBlocks.Core.Domain.Events;

/// <summary>
/// Marker for domain events raised inside a bounded context.
/// Pure domain contract (no MediatR/transport).
/// </summary>
public interface IDomainEvent : IEvent
{
    // Inherits all members from IEvent:
    // - Guid EventId { get; }
    // - DateTime OccurredAt { get; }  
    // - int Version { get; }
    // - string Name { get; }
}