using BuildingBlocks.Core.Domain.Primitives;

namespace BuildingBlocks.Core.Model;

/// <summary>
/// Interface for entities with strongly-typed identifiers following Epic 2 specifications.
/// Integrates with IStrongId system for type safety.
/// </summary>
/// <typeparam name="TId">The type of the entity identifier implementing IStrongId</typeparam>
public interface IEntity<TId> : IEntity
    where TId : IStrongId
{
    TId Id { get; }
}

/// <summary>
/// Non-generic entity interface with core entity concerns.
/// Provides audit trail, versioning, and soft delete capabilities.
/// </summary>
public interface IEntity : IAuditable
{
    uint Version { get; }
    bool IsDeleted { get; }
    DateTime? DeletedAt { get; }
}

/// <summary>
/// Interface for auditable entities with timestamp tracking.
/// Follows Single Responsibility Principle for audit concerns.
/// </summary>
public interface IAuditable
{
    DateTime CreatedAt { get; }
    DateTime UpdatedAt { get; }
}