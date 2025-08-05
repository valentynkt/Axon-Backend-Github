using BuildingBlocks.Core.Event;

namespace BuildingBlocks.Core.Model;

/// <summary>
/// Interface for domain aggregates with strongly-typed identifier.
/// Extends base entity with domain event capabilities.
/// </summary>
/// <typeparam name="T">The type of the aggregate identifier</typeparam>
public interface IAggregate<T> : IAggregate, IEntity<T>
{
}

/// <summary>
/// Base interface for all domain aggregates.
/// Defines domain event management capabilities following DDD patterns.
/// Non-generic base interface to allow polymorphic handling of aggregates.
/// </summary>
public interface IAggregate
{
    /// <summary>
    /// Gets the list of domain events that have occurred on this aggregate.
    /// </summary>
    IReadOnlyList<IDomainEvent> DomainEvents { get; }
    
    /// <summary>
    /// Clears and returns all domain events from this aggregate.
    /// Should be called after events are dispatched.
    /// </summary>
    IEvent[] ClearDomainEvents();
}