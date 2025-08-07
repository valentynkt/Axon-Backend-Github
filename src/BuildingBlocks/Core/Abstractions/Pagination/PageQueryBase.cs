using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Core.Pagination;

/// <summary>
/// Abstract base record for paginated queries.
/// Provides consistent implementation of pagination parameters with validation.
/// Uses record type for value equality and immutability benefits.
/// All paginated queries return Result&lt;T&gt; for consistent error handling.
/// </summary>
/// <typeparam name="TResponse">The type of paginated response this query returns</typeparam>
public abstract record PageQueryBase<TResponse> : RequestBase<Result<TResponse>>, IPageQuery<TResponse>
    where TResponse : class
{
    /// <summary>
    /// Current page number (1-based)
    /// </summary>
    public int PageNumber { get; init; } = 1;
    
    /// <summary>
    /// Number of items per page
    /// </summary>
    public int PageSize { get; init; } = PaginationExtensions.DefaultPageSize;
    
    /// <summary>
    /// Filtering criteria in Sieve format
    /// </summary>
    public string? Filters { get; init; }
    
    /// <summary>
    /// Sorting criteria in Sieve format
    /// </summary>
    public string? SortOrder { get; init; }
    
    /// <summary>
    /// Validates pagination parameters during construction
    /// </summary>
    protected PageQueryBase()
    {
        if (PageNumber < 1)
            throw new ArgumentException("Page number must be greater than 0", nameof(PageNumber));
        
        if (PageSize < 1 || PageSize > PaginationExtensions.MaxPageSize)
            throw new ArgumentException($"Page size must be between 1 and {PaginationExtensions.MaxPageSize}", nameof(PageSize));
    }
}