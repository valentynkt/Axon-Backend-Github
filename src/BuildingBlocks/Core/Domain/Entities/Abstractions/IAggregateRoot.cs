namespace BuildingBlocks.Core.Domain.Entities.Abstractions;

/// <summary>
/// Marker for aggregate roots (entry point to a consistency boundary).
/// Aggregates are the only place where domain events and optimistic concurrency are exposed.
/// </summary>
public interface IAggregateRoot : IHasDomainEvents, IVersioned { }

/// <summary>
/// Generic marker for aggregate roots with identity.
/// Useful for generic constraints in repositories/specifications.
/// </summary>
/// <typeparam name="TId">Strong ID type</typeparam>
public interface IAggregateRoot<out TId> : IAggregateRoot, IIdentifiable<TId>
    where TId : notnull
{
}