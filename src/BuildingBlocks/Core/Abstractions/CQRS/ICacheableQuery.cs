// /BuildingBlocks/Core/Abstractions/CQRS/Policies/ICacheableQuery.cs
#nullable enable
using System;

namespace BuildingBlocks.Core.Abstractions.CQRS.Policies;

/// <summary>
/// Opt-in marker for read-through caching on queries (handled by Application behavior).
/// Kept in Core so modules can declare intent without referencing Application.
/// </summary>
public interface ICacheableQuery
{
    /// <summary>Enable/disable caching for this request instance.</summary>
    bool UseCache { get; }

    /// <summary>Per-request TTL override; null to use behavior default.</summary>
    TimeSpan? CacheDuration { get; }

    /// <summary>Optional prefix to namespace cache keys per query type/tenant.</summary>
    string? CacheKeyPrefix { get; }
}