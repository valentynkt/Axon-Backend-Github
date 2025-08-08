using System.Collections.Generic;

namespace BuildingBlocks.Core.Abstractions.Pagination;

public interface IPageList<out T>
{
    IReadOnlyList<T> Items { get; }
    PaginationMeta Meta { get; }
}

// For backward compatibility - preserve existing interface
public interface IPagedResult<T>
{
    IReadOnlyList<T> Items { get; }
    PaginationMeta Meta { get; }
    long TotalCount { get; }
    int PageNumber { get; }
    int PageSize { get; }
    long TotalPages { get; }
    bool HasPrevious { get; }
    bool HasNext { get; }
    int CurrentPageSize { get; }
    int CurrentStartIndex { get; }
    int CurrentEndIndex { get; }
}