namespace BuildingBlocks.Core.Model;

/// <summary>
/// Base interface for all entities with strongly-typed identifier.
/// Minimal interface following ISP - only identity concern.
/// </summary>
/// <typeparam name="T">The type of the entity identifier</typeparam>
public interface IBaseEntity<T> : IIdentifiable<T> where T : struct, IEquatable<T>
{
}

/// <summary>
/// Interface for entities that support versioning and soft deletion.
/// Extends base entity with concurrency and deletion concerns.
/// </summary>
/// <typeparam name="T">The type of the entity identifier</typeparam>
public interface IEntity<T> : IBaseEntity<T>, IVersioned, ISoftDeletable where T : struct, IEquatable<T>
{
}

/// <summary>
/// Interface for entities that support audit tracking.
/// Composes base entity with audit trail concerns following ISP.
/// </summary>
/// <typeparam name="T">The type of the entity identifier</typeparam>
public interface IAuditableEntity<T> : IBaseEntity<T>, IAuditable where T : struct, IEquatable<T>
{
}