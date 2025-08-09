namespace BuildingBlocks.Core.Domain.Entities.Abstractions;

/// <summary>
/// Opt-in trait for entities that support soft deletion.
/// Infra typically applies a global query filter on <see cref="IsDeleted"/>.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; }
    DateTimeOffset? DeletedAt { get; }
}