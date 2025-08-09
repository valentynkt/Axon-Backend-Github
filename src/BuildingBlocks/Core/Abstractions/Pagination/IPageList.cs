namespace BuildingBlocks.Core.Abstractions.Pagination;

/// <summary>
/// Modern interface for paginated data with clean separation of concerns.
/// Provides items and metadata in a structured format.
/// </summary>
public interface IPageList<out T>
{
    /// <summary>
    /// The items in the current page.
    /// </summary>
    IReadOnlyList<T> Items { get; }
    
    /// <summary>
    /// Pagination metadata including counts, navigation info, etc.
    /// </summary>
    PaginationMeta Meta { get; }
}

/// <summary>
/// Legacy interface for backward compatibility - prefer IPageList&lt;T&gt; for new code.
/// Provides flattened access to pagination properties.
/// </summary>
[Obsolete("Use IPageList<T> for new code - provides better separation of concerns")]
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