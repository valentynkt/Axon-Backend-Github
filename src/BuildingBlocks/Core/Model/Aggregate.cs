using BuildingBlocks.Core.Event;

namespace BuildingBlocks.Core.Model;

/// <summary>
/// Base implementation for domain aggregates following DDD patterns.
/// Inherits from BaseEntity for core entity capabilities and adds domain event management.
/// Use BaseAuditableAggregate if audit tracking is needed.
/// </summary>
/// <typeparam name="TId">The type of the aggregate identifier</typeparam>
public abstract record BaseAggregate<TId> : BaseEntity<TId>, IAggregate<TId>
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
/// Base implementation for auditable domain aggregates.
/// Inherits from BaseAuditableEntity and adds domain event management.
/// Use this for aggregates that require creation/modification tracking.
/// </summary>
/// <typeparam name="TId">The type of the aggregate identifier</typeparam>
public abstract record BaseAuditableAggregate<TId> : BaseAuditableEntity<TId>, IAggregate<TId>
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
/// Legacy alias for backward compatibility.
/// Use BaseAggregate<TId> or BaseAuditableAggregate<TId> in new code.
/// </summary>
/// <typeparam name="TId">The type of the aggregate identifier</typeparam>
[Obsolete("Use BaseAggregate<TId> or BaseAuditableAggregate<TId> instead")]
public abstract record Aggregate<TId> : BaseAuditableAggregate<TId>
{
}