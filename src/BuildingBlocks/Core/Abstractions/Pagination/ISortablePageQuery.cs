using System.Collections.Generic;

namespace BuildingBlocks.Core.Abstractions.Pagination;

public interface ISortablePageQuery<TResponse> : IPageQuery<TResponse>
{
    // Client-provided sort (optional).
    IReadOnlyList<SortCriteria>? SortBy { get; init; }

    // Query-defined default sort (should be stable and end with unique key).
    IReadOnlyList<SortCriteria> DefaultSort { get; }

    // Use this in handlers. Implementation provided by PageQueryBase.
    IReadOnlyList<SortCriteria> EffectiveSortBy { get; }
}