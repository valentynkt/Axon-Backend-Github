using BuildingBlocks.Core.Event;

namespace BuildingBlocks.Core.Model;

/// <summary>
/// Base interface for all aggregate roots.
/// Defines domain event handling capabilities required by all aggregates.
/// Follows Interface Segregation Principle by focusing solely on domain events.
/// </summary>
public interface IBaseAggregate
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

/// <summary>
/// Interface for aggregate roots with identity, versioning, and soft deletion support.
/// Composes base aggregate capabilities with entity lifecycle management.
/// This is the standard interface that most aggregates should implement.
/// </summary>
/// <typeparam name="T">The type of the aggregate identifier</typeparam>
public interface IAggregate<T> : IBaseAggregate, IEntity<T> where T : struct, IEquatable<T>
{
}

/// <summary>
/// Interface for aggregate roots that require full audit trail capabilities.
/// Extends base aggregate with comprehensive audit tracking.
/// Use this for aggregates that need to track who created/modified them.
/// </summary>
/// <typeparam name="T">The type of the aggregate identifier</typeparam>
public interface IAuditableAggregate<T> : IBaseAggregate, IAuditableEntity<T>, IVersioned, ISoftDeletable where T : struct, IEquatable<T>
{
}
