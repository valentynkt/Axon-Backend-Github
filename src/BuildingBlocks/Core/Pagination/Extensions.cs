using Microsoft.EntityFrameworkCore;
using Sieve.Models;
using Sieve.Services;

namespace BuildingBlocks.Core.Pagination;

/// <summary>
/// Extension methods for efficient pagination with optimized query patterns
/// Eliminates N+1 problems and provides clean API for paginated queries
/// </summary>
public static class PaginationExtensions
{
    /// <summary>
    /// Maximum allowed page size to prevent resource exhaustion
    /// </summary>
    public const int MaxPageSize = 1000;
    
    /// <summary>
    /// Default page size when none is specified
    /// </summary>
    public const int DefaultPageSize = 10;
    
    /// <summary>
    /// Applies pagination to a queryable with optimized database queries
    /// Uses single query approach to avoid N+1 problems
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <param name="queryable">The source queryable</param>
    /// <param name="pageRequest">Pagination parameters</param>
    /// <param name="sieveProcessor">Sieve processor for filtering and sorting</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated result with items and metadata</returns>
    public static async Task<IPagedResult<TEntity>> ToPagedResultAsync<TEntity>(
        this IQueryable<TEntity> queryable,
        IPageRequest pageRequest,
        ISieveProcessor sieveProcessor,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(queryable);
        ArgumentNullException.ThrowIfNull(pageRequest);
        ArgumentNullException.ThrowIfNull(sieveProcessor);
        
        // Validate pagination parameters
        var validationResult = ValidatePageRequest(pageRequest);
        if (validationResult != null)
        {
            throw new ArgumentException(validationResult.Message, nameof(pageRequest));
        }
        
        var sieveModel = CreateSieveModel(pageRequest);
        
        // Apply filtering and sorting, but not pagination yet
        var filteredQuery = sieveProcessor.Apply(sieveModel, queryable, applyPagination: false);
        
        // Get total count efficiently
        var totalCount = await filteredQuery.CountAsync(cancellationToken);
        
        if (totalCount == 0)
        {
            return PagedResult.Empty<TEntity>();
        }
        
        // Apply pagination to the filtered query
        var paginatedQuery = sieveProcessor.Apply(sieveModel, filteredQuery, 
            applyFiltering: false, applySorting: false);
        
        // Fetch items
        var items = await paginatedQuery.ToListAsync(cancellationToken);
        
        return PagedResult.Create(
            items.AsReadOnly(), 
            pageRequest.PageNumber, 
            pageRequest.PageSize, 
            totalCount);
    }
    
    /// <summary>
    /// Applies pagination to a queryable without Sieve processing
    /// For simple pagination without filtering or sorting
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <param name="queryable">The source queryable</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated result with items and metadata</returns>
    public static async Task<IPagedResult<TEntity>> ToPagedResultAsync<TEntity>(
        this IQueryable<TEntity> queryable,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(queryable);
        
        if (pageNumber < 1)
            throw new ArgumentException("Page number must be greater than 0", nameof(pageNumber));
        
        if (pageSize < 1 || pageSize > MaxPageSize)
            throw new ArgumentException($"Page size must be between 1 and {MaxPageSize}", nameof(pageSize));
        
        // Get total count efficiently
        var totalCount = await queryable.CountAsync(cancellationToken);
        
        if (totalCount == 0)
        {
            return PagedResult.Empty<TEntity>();
        }
        
        // Apply pagination
        var items = await queryable
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        
        return PagedResult.Create(
            items.AsReadOnly(), 
            pageNumber, 
            pageSize, 
            totalCount);
    }
    
    /// <summary>
    /// Applies pagination to an in-memory collection
    /// Use only for small datasets that are already loaded
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <param name="source">The source collection</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <returns>Paginated result with items and metadata</returns>
    public static IPagedResult<TEntity> ToPagedResult<TEntity>(
        this IEnumerable<TEntity> source,
        int pageNumber,
        int pageSize)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(source);
        
        if (pageNumber < 1)
            throw new ArgumentException("Page number must be greater than 0", nameof(pageNumber));
        
        if (pageSize < 1 || pageSize > MaxPageSize)
            throw new ArgumentException($"Page size must be between 1 and {MaxPageSize}", nameof(pageSize));
        
        var sourceList = source as IReadOnlyList<TEntity> ?? source.ToList().AsReadOnly();
        var totalCount = sourceList.Count;
        
        if (totalCount == 0)
        {
            return PagedResult.Empty<TEntity>();
        }
        
        var items = sourceList
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList()
            .AsReadOnly();
        
        return PagedResult.Create(items, pageNumber, pageSize, totalCount);
    }
    
    /// <summary>
    /// Creates a Sieve model from page request parameters
    /// </summary>
    /// <param name="pageRequest">The page request</param>
    /// <returns>Configured SieveModel</returns>
    private static SieveModel CreateSieveModel(IPageRequest pageRequest)
    {
        return new SieveModel
        {
            PageSize = pageRequest.PageSize,
            Page = pageRequest.PageNumber,
            Sorts = pageRequest.SortOrder,
            Filters = pageRequest.Filters
        };
    }
    
    /// <summary>
    /// Validates page request parameters
    /// </summary>
    /// <param name="pageRequest">The page request to validate</param>
    /// <returns>Error if validation fails, null if valid</returns>
    private static Error? ValidatePageRequest(IPageRequest pageRequest)
    {
        if (pageRequest.PageNumber < 1)
            return PaginationErrors.InvalidPageNumber;
        
        if (pageRequest.PageSize < 1)
            return PaginationErrors.InvalidPageSize;
        
        if (pageRequest.PageSize > MaxPageSize)
            return PaginationErrors.PageSizeExceedsMaximum(MaxPageSize);
        
        return null;
    }
}