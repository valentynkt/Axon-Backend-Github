using BuildingBlocks.Core.Domain.Entities.Abstractions;
using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Core.Domain.Primitives;

namespace BuildingBlocks.Core.Domain.Entities.Base;

/// <summary>
/// Base type for aggregate roots: the gateway to a consistency boundary.
/// Aggregates are responsible for raising domain events and exposing an
/// optimistic concurrency token.
/// </summary>
/// <typeparam name="TId">Strongly typed ID type</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot<TId>
    where TId : notnull, IStrongId
{
    private readonly List<IDomainEvent> _domainEvents = new();

    protected AggregateRoot(TId id) : base(id) { }

    /// <summary>
    /// Parameterless ctor for ORM materialization only.
    /// </summary>
    protected AggregateRoot() : base() { }

    /// <summary>
    /// Optimistic concurrency token (rowversion/etag). Persistence maps it.
    /// </summary>
    public uint Version { get; protected set; }

    /// <summary>Current domain events raised by this aggregate.</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Raise (enqueue) a domain event. Prefer immutable event records.
    /// </summary>
    /// <param name="event">Domain event instance</param>
    protected void RaiseDomainEvent(IDomainEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        _domainEvents.Add(@event);
    }

    /// <summary>
    /// Clears all domain events (typically after persistence/dispatch).
    /// </summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}