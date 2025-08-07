namespace BuildingBlocks.Core.Pagination;

/// <summary>
/// Value object containing pagination metadata following Clean Architecture principles
/// Immutable and encapsulates all pagination-related calculations and navigation information
/// </summary>
public sealed record PaginationMeta
{
    /// <summary>
    /// Current page number (1-based)
    /// </summary>
    public int PageNumber { get; }
    
    /// <summary>
    /// Number of items per page
    /// </summary>
    public int PageSize { get; }
    
    /// <summary>
    /// Total number of items across all pages
    /// </summary>
    public int TotalCount { get; }
    
    /// <summary>
    /// Total number of pages
    /// </summary>
    public int TotalPages { get; }
    
    /// <summary>
    /// Number of items in the current page
    /// </summary>
    public int CurrentPageSize { get; }
    
    /// <summary>
    /// Starting index of the current page (1-based)
    /// </summary>
    public int CurrentStartIndex { get; }
    
    /// <summary>
    /// Ending index of the current page (1-based)
    /// </summary>
    public int CurrentEndIndex { get; }
    
    /// <summary>
    /// Indicates if there is a previous page
    /// </summary>
    public bool HasPrevious { get; }
    
    /// <summary>
    /// Indicates if there is a next page
    /// </summary>
    public bool HasNext { get; }
    
    /// <summary>
    /// Indicates if this is the first page
    /// </summary>
    public bool IsFirstPage => PageNumber == 1;
    
    /// <summary>
    /// Indicates if this is the last page
    /// </summary>
    public bool IsLastPage => PageNumber == TotalPages;
    
    /// <summary>
    /// Creates pagination metadata with validation and calculations
    /// </summary>
    /// <param name="pageNumber">Current page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="totalCount">Total number of items</param>
    /// <param name="currentPageSize">Number of items in current page</param>
    public PaginationMeta(int pageNumber, int pageSize, int totalCount, int currentPageSize)
    {
        if (pageNumber < 1)
            throw new ArgumentException("Page number must be greater than 0", nameof(pageNumber));
        
        if (pageSize < 1)
            throw new ArgumentException("Page size must be greater than 0", nameof(pageSize));
        
        if (totalCount < 0)
            throw new ArgumentException("Total count cannot be negative", nameof(totalCount));
        
        if (currentPageSize < 0)
            throw new ArgumentException("Current page size cannot be negative", nameof(currentPageSize));
        
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalCount = totalCount;
        CurrentPageSize = currentPageSize;
        
        // Calculate derived properties
        TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        CurrentStartIndex = totalCount == 0 ? 0 : ((pageNumber - 1) * pageSize) + 1;
        CurrentEndIndex = totalCount == 0 ? 0 : CurrentStartIndex + currentPageSize - 1;
        HasPrevious = pageNumber > 1;
        HasNext = pageNumber < TotalPages;
    }
    
    /// <summary>
    /// Creates empty pagination metadata for no results
    /// </summary>
    public static PaginationMeta Empty => new(1, 0, 0, 0);
    
    /// <summary>
    /// Creates pagination metadata from basic parameters
    /// </summary>
    /// <param name="pageNumber">Current page number</param>
    /// <param name="pageSize">Items per page</param>
    /// <param name="totalCount">Total items</param>
    /// <param name="actualItemsCount">Actual items in current page</param>
    public static PaginationMeta Create(int pageNumber, int pageSize, int totalCount, int actualItemsCount)
    {
        return new PaginationMeta(pageNumber, pageSize, totalCount, actualItemsCount);
    }
}