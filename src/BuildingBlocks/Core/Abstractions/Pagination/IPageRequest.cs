namespace BuildingBlocks.Core.Abstractions.Pagination;

/// <summary>
/// Interface for pagination request parameters with support for both modern and legacy patterns.
/// Provides default implementations to avoid breaking existing implementers.
/// </summary>
public interface IPageRequest
{
    /// <summary>
    /// Legacy page number property - prefer using 1-based Page property.
    /// Kept for compatibility with existing handlers.
    /// </summary>
    [Obsolete("Use Page (1-based) instead of PageNumber.")]
    int PageNumber { get; init; }

    /// <summary>
    /// Number of items per page. Server caps will be enforced by PageQueryBase.
    /// </summary>
    int PageSize { get; init; }

    /// <summary>
    /// Whether to include total count in results (for performance optimization).
    /// Default implementation avoids breaking existing implementers.
    /// </summary>
    bool IncludeTotalCount => false;

    /// <summary>
    /// Typed sorting criteria - preferred approach. Server will inject unique tiebreaker.
    /// Default implementation avoids breaking existing implementers.
    /// </summary>
    IReadOnlyList<SortCriteria>? SortBy => null;

    /// <summary>
    /// Legacy string-based filtering - discouraged, use explicit filter properties.
    /// </summary>
    [Obsolete("Use explicit filter properties + SortBy.")]
    string? Filters => null;

    /// <summary>
    /// Legacy string-based sorting - discouraged, use SortBy property.
    /// </summary>
    [Obsolete("Use SortBy.")]
    string? SortOrder => null;
}