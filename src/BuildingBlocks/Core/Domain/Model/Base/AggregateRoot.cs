using System.Diagnostics;
using BuildingBlocks.Core.Domain.Core.Model.Abstractions;
using BuildingBlocks.Core.Domain.Entities.Base;
using BuildingBlocks.Core.Domain.Primitives;

namespace BuildingBlocks.Core.Domain.Core.Model.Base;

/// <summary>
/// Minimal base for aggregate roots:
/// - Inherits Entity<TId>
/// - Captures domain events raised during a unit-of-work
/// - Provides a protected Raise() to publish domain facts
/// </summary>
[DebuggerDisplay("{GetType().Name,nq}({Id})")]
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot<TId>
    where TId : IStrongId
{
    private readonly List<IDomainEvent> _domainEvents = new();

    /// <summary>ORM-only ctor.</summary>
    protected AggregateRoot() { }

    protected AggregateRoot(TId id) : base(id) { }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Raise a domain event (idempotent within this aggregate instance).
    /// Prefer immutable event records. Avoid side-effects here.
    /// </summary>
    protected void Raise(IDomainEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        _domainEvents.Add(@event);
    }

    /// <summary>
    /// Clear events after they are dispatched by the infrastructure layer.
    /// </summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}