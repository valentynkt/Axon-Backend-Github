using BuildingBlocks.Core.Domain.Primitives;

namespace BuildingBlocks.Core.Domain.Entities.Abstractions;

/// <summary>Marker for domain entities.</summary>
public interface IEntity { }

/// <summary>Strongly-typed entity identity.</summary>
public interface IEntity<out TId> : IEntity, IIdentifiable<TId> where TId : IStrongId
{
}