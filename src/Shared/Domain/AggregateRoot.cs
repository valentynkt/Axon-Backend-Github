using System.Collections.ObjectModel;

namespace Axon.Shared.Domain;

/// <summary>
/// Non-generic interface for Unit of Work scanning as per SPARC architecture
/// </summary>
public interface IAggregateRoot
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}

/// <summary>
/// Base class for domain aggregate roots following DDD principles
/// Provides domain event collection and management capabilities
/// Follows SPARC architecture with non-generic IAggregateRoot interface for UoW scanning
/// </summary>
/// <typeparam name="TId">The type of the aggregate's identifier</typeparam>
public abstract class AggregateRoot<TId> : BaseEntity<TId>, IAggregateRoot
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = new();

    /// <summary>
    /// Gets the read-only collection of domain events raised by this aggregate
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected AggregateRoot(TId id) : base(id)
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