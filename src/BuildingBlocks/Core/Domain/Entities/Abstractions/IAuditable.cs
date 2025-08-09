namespace BuildingBlocks.Core.Domain.Entities.Abstractions;

/// <summary>
/// Opt-in trait for entities that track creation/update timestamps.
/// Infrastructure (e.g., EF interceptors) should populate these consistently.
/// </summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; }
    DateTimeOffset UpdatedAt { get; }
}