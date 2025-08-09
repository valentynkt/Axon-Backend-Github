namespace BuildingBlocks.Core.Abstractions.Pagination;

/// <summary>
/// Interface for sortable page queries with both client-provided and default sorting support.
/// Provides effective sorting that combines client preferences with fallback defaults.
/// </summary>
/// <typeparam name="TResponse">The type of response this query returns</typeparam>
public interface ISortablePageQuery<TResponse> : IPageQuery<TResponse>
{
    /// <summary>
    /// Client-provided sorting criteria (optional).
    /// When provided, takes precedence over DefaultSort.
    /// </summary>
    IReadOnlyList<SortCriteria>? SortBy { get; init; }

    /// <summary>
    /// Query-defined default sort criteria (should be stable and end with unique key).
    /// Used as fallback when SortBy is null or empty.
    /// </summary>
    IReadOnlyList<SortCriteria> DefaultSort { get; }

    /// <summary>
    /// The effective sorting to use in handlers, combining SortBy and DefaultSort.
    /// Implementation provided by PageQueryBase with unique key enforcement.
    /// </summary>
    IReadOnlyList<SortCriteria> EffectiveSortBy { get; }
}