using BuildingBlocks.Core.Domain.Entities.Abstractions;
using BuildingBlocks.Core.Domain.Primitives;

namespace BuildingBlocks.Core.Domain.Core.Model.Abstractions;

/// <summary>
/// Non-generic marker for aggregate roots. Enables EF scanning via ChangeTracker.
/// </summary>
public interface IAggregateRoot : IEntity, IHasDomainEvents { }

/// <summary>
/// Strongly-typed aggregate root with a strong id.
/// </summary>
public interface IAggregateRoot<TId> : IAggregateRoot, IEntity<TId>
    where TId : IStrongId
{
}