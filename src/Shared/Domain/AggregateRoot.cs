using System.Collections.ObjectModel;

namespace Axon.Shared.Domain;

/// <summary>
/// Base class for domain aggregate roots following DDD principles
/// </summary>
/// <typeparam name="TId">The type of the aggregate's identifier</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>
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
}