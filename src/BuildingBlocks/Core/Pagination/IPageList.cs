namespace BuildingBlocks.Core.Pagination;

/// <summary>
/// Unified interface for paginated results following Clean Architecture principles
/// Provides comprehensive pagination metadata and data access
/// </summary>
/// <typeparam name="T">The type of items in the paginated result</typeparam>
public interface IPagedResult<T> where T : class
{
    /// <summary>
    /// The items in the current page
    /// </summary>
    IReadOnlyList<T> Items { get; }
    
    /// <summary>
    /// Pagination metadata containing all navigation and statistical information
    /// </summary>
    PaginationMeta Meta { get; }
    
    /// <summary>
    /// Total number of items across all pages
    /// </summary>
    int TotalCount { get; }
    
    /// <summary>
    /// Current page number (1-based)
    /// </summary>
    int PageNumber { get; }
    
    /// <summary>
    /// Number of items per page
    /// </summary>
    int PageSize { get; }
    
    /// <summary>
    /// Total number of pages
    /// </summary>
    int TotalPages { get; }
    
    /// <summary>
    /// Indicates if there is a previous page
    /// </summary>
    bool HasPrevious { get; }
    
    /// <summary>
    /// Indicates if there is a next page
    /// </summary>
    bool HasNext { get; }
    
    /// <summary>
    /// Number of items in the current page
    /// </summary>
    int CurrentPageSize { get; }
    
    /// <summary>
    /// Starting index of the current page (1-based)
    /// </summary>
    int CurrentStartIndex { get; }
    
    /// <summary>
    /// Ending index of the current page (1-based)
    /// </summary>
    int CurrentEndIndex { get; }
}