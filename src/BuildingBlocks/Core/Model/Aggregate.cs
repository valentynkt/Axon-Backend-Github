using BuildingBlocks.Core.Event;

namespace BuildingBlocks.Core.Model;

/// <summary>
/// Base implementation for aggregate roots with identity, versioning, and soft deletion.
/// Provides domain event management and basic lifecycle support.
/// Use this for aggregates that don't need audit trail capabilities.
/// </summary>
/// <typeparam name="TId">The type of the aggregate identifier</typeparam>
public abstract record BaseAggregate<TId> : BaseAuditableEntity<TId>, IAggregate<TId> where TId : struct, IEquatable<TId>
{
    private readonly List<IDomainEvent> _domainEvents = new();
    
    /// <summary>
    /// Gets the list of domain events that have occurred on this aggregate.
    /// </summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Adds a domain event to this aggregate.
    /// Events will be dispatched when the aggregate is saved.
    /// </summary>
    /// <param name="domainEvent">The domain event to add</param>
    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Clears and returns all domain events from this aggregate.
    /// Should be called after events are dispatched.
    /// </summary>
    /// <returns>Array of domain events that were cleared</returns>
    public IEvent[] ClearDomainEvents()
    {
        IEvent[] dequeuedEvents = _domainEvents.ToArray();
        _domainEvents.Clear();
        return dequeuedEvents;
    }
}

/// <summary>
/// Base implementation for aggregate roots with full audit trail capabilities.
/// Provides domain event management, audit tracking, versioning, and soft deletion.
/// Use this for aggregates that need to track who created/modified them.
/// </summary>
/// <typeparam name="TId">The type of the aggregate identifier</typeparam>
public abstract record BaseAuditableAggregate<TId> : BaseAuditableEntity<TId>, IAuditableAggregate<TId>  where TId : struct, IEquatable<TId>
{
    private readonly List<IDomainEvent> _domainEvents = new();
    
    /// <summary>
    /// Gets the list of domain events that have occurred on this aggregate.
    /// </summary>
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Adds a domain event to this aggregate.
    /// Events will be dispatched when the aggregate is saved.
    /// </summary>
    /// <param name="domainEvent">The domain event to add</param>
    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Clears and returns all domain events from this aggregate.
    /// Should be called after events are dispatched.
    /// </summary>
    /// <returns>Array of domain events that were cleared</returns>
    public IEvent[] ClearDomainEvents()
    {
        IEvent[] dequeuedEvents = _domainEvents.ToArray();
        _domainEvents.Clear();
        return dequeuedEvents;
    }
}
