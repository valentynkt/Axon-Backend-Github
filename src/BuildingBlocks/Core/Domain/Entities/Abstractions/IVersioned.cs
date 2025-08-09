namespace BuildingBlocks.Core.Domain.Entities.Abstractions;

/// <summary>
/// Optimistic concurrency marker (e.g., row version).
/// </summary>
public interface IVersioned
{
    /// <summary>
    /// Version used for optimistic concurrency checks.
    /// Typically mapped to a rowversion/timestamp/etag by the persistence layer.
    /// </summary>
    uint Version { get; }
}