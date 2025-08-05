using System.Collections.ObjectModel;
using BuildingBlocks.Core.Model;
using BuildingBlocks.Core.Event;

namespace Axon.Shared.Domain;

/// <summary>
/// Base class for auditable domain aggregate roots following DDD principles
/// Combines audit capabilities with domain event management
/// Compatible with both Shared.Domain and BuildingBlocks.Core patterns
/// </summary>
/// <typeparam name="TId">The type of the aggregate's identifier</typeparam>
public abstract class AuditableAggregateRoot<TId> : AuditableEntity<TId>, IAggregateRoot, IAggregate<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = new();

    /// <summary>
    /// Gets the read-only collection of domain events raised by this aggregate
    /// Compatible with both IAggregateRoot and IAggregate interfaces
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    
    /// <summary>
    /// Gets domain events as IReadOnlyList for BuildingBlocks.Core.IAggregate compatibility
    /// </summary>
    IReadOnlyList<IDomainEvent> IAggregate.DomainEvents => _domainEvents.AsReadOnly().ToList();

    protected AuditableAggregateRoot(TId id) : base(id)
    {
    }

    /// <summary>
    /// Raises a domain event to be published after the aggregate is persisted
    /// </summary>
    /// <param name="domainEvent">The domain event to raise</param>
    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Clears all domain events. This should be called after events are published.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
    
    /// <summary>
    /// Clears domain events and returns them as IEvent array for BuildingBlocks.Core.IAggregate compatibility
    /// </summary>
    IEvent[] IAggregate.ClearDomainEvents()
    {
        var events = _domainEvents.Cast<IEvent>().ToArray();
        _domainEvents.Clear();
        return events;
    }

    /// <summary>
    /// Gets whether this aggregate has any pending domain events
    /// </summary>
    public bool HasDomainEvents => _domainEvents.Count > 0;

    /// <summary>
    /// Applies a domain event to this aggregate for event sourcing scenarios.
    /// This method should be implemented by derived classes to handle event replay.
    /// </summary>
    /// <param name="domainEvent">The domain event to apply</param>
    protected virtual void ApplyEvent(IDomainEvent domainEvent)
    {
        // Default implementation - derived classes should override for specific event handling
        // This enables event sourcing replay capability
    }

    /// <summary>
    /// Creates a snapshot of the current aggregate state for event sourcing optimization.
    /// Override this method in derived classes to implement snapshot creation.
    /// </summary>
    /// <returns>A snapshot object representing the current state, or null if snapshots are not supported</returns>
    protected virtual object? CreateSnapshot()
    {
        // Default implementation returns null - derived classes should override for snapshot support
        // This enables event sourcing snapshot optimization
        return null;
    }

    /// <summary>
    /// Restores the aggregate state from a snapshot for event sourcing optimization.
    /// Override this method in derived classes to implement snapshot restoration.
    /// </summary>
    /// <param name="snapshot">The snapshot object to restore from</param>
    protected virtual void RestoreFromSnapshot(object snapshot)
    {
        // Default implementation does nothing - derived classes should override for snapshot support
        // This enables event sourcing snapshot restoration
    }
}