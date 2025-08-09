using BuildingBlocks.Core.Domain.Events;

namespace BuildingBlocks.Core.Domain.Entities.Abstractions;

/// <summary>
/// Exposes the domain event buffer on entities/aggregates.
/// Keeping it read-only avoids leaking mutation outside the entity.
/// </summary>
public interface IHasDomainEvents
{
    /// <summary>Current domain events raised by the aggregate/entity.</summary>
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    /// <summary>Clears the event buffer (typically after persistence/dispatch).</summary>
    void ClearDomainEvents();
}