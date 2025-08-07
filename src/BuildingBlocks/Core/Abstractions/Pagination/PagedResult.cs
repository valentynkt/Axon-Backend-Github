namespace BuildingBlocks.Core.Pagination;

/// <summary>
/// Immutable record implementation of paginated results following Clean Architecture principles
/// Provides efficient, type-safe pagination with comprehensive metadata
/// </summary>
/// <typeparam name="T">The type of items in the paginated result</typeparam>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, PaginationMeta Meta) : IPagedResult<T>
    where T : class
{
    /// <summary>
    /// Total number of items across all pages
    /// </summary>
    public int TotalCount => Meta.TotalCount;
    
    /// <summary>
    /// Current page number (1-based)
    /// </summary>
    public int PageNumber => Meta.PageNumber;
    
    /// <summary>
    /// Number of items per page
    /// </summary>
    public int PageSize => Meta.PageSize;
    
    /// <summary>
    /// Total number of pages
    /// </summary>
    public int TotalPages => Meta.TotalPages;
    
    /// <summary>
    /// Indicates if there is a previous page
    /// </summary>
    public bool HasPrevious => Meta.HasPrevious;
    
    /// <summary>
    /// Indicates if there is a next page
    /// </summary>
    public bool HasNext => Meta.HasNext;
    
    /// <summary>
    /// Number of items in the current page
    /// </summary>
    public int CurrentPageSize => Meta.CurrentPageSize;
    
    /// <summary>
    /// Starting index of the current page (1-based)
    /// </summary>
    public int CurrentStartIndex => Meta.CurrentStartIndex;
    
    /// <summary>
    /// Ending index of the current page (1-based)
    /// </summary>
    public int CurrentEndIndex => Meta.CurrentEndIndex;
    

}

/// <summary>
/// Non-generic helper class for PagedResult creation
/// Provides factory methods for creating paginated results
/// </summary>
public static class PagedResult
{
    /// <summary>
    /// Creates an empty paginated result
    /// </summary>
    /// <typeparam name="T">The type of items</typeparam>
    /// <returns>An empty PagedResult instance</returns>
    public static PagedResult<T> Empty<T>() where T : class => 
        new(Array.Empty<T>(), PaginationMeta.Empty);

    /// <summary>
    /// Creates a paginated result with validation
    /// </summary>
    /// <typeparam name="T">The type of items</typeparam>
    /// <param name="items">The items for the current page</param>
    /// <param name="pageNumber">Current page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="totalCount">Total number of items across all pages</param>
    /// <returns>A new PagedResult instance</returns>
    public static PagedResult<T> Create<T>(
        IReadOnlyList<T> items, 
        int pageNumber, 
        int pageSize, 
        int totalCount) where T : class
    {
        ArgumentNullException.ThrowIfNull(items);
        
        var meta = PaginationMeta.Create(pageNumber, pageSize, totalCount, items.Count);
        return new PagedResult<T>(items, meta);
    }
    
    /// <summary>
    /// Creates a single page result containing all items
    /// </summary>
    /// <typeparam name="T">The type of items</typeparam>
    /// <param name="items">All items to include</param>
    /// <returns>A single-page PagedResult instance</returns>
    public static PagedResult<T> CreateSinglePage<T>(IReadOnlyList<T> items) where T : class
    {
        ArgumentNullException.ThrowIfNull(items);
        
        var meta = PaginationMeta.Create(1, items.Count, items.Count, items.Count);
        return new PagedResult<T>(items, meta);
    }
}