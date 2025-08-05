using System.Linq.Expressions;
using BuildingBlocks.Core.Model;
using BuildingBlocks.Core.Pagination;

namespace BuildingBlocks.Postgres;

/// <summary>
/// Base repository interface for entities with Guid primary key
/// PostgreSQL implementation of repository pattern
/// </summary>
public interface IRepository<T> : IRepository<T, Guid> where T : class, IEntity<Guid>;

/// <summary>
/// Generic repository interface for all CRUD operations
/// PostgreSQL implementation maintaining architectural patterns
/// Compatible with both IEntity<TId> (BuildingBlocks) and domain entities (Shared.Domain)
/// </summary>
public interface IRepository<T, in TId> : IReadRepository<T, TId>, IWriteRepository<T, TId>
    where T : class
{
}

/// <summary>
/// Aggregate repository interface for domain aggregates
/// Specialized interface for aggregate roots with domain event support
/// </summary>
public interface IAggregateRepository<T, in TId> : IRepository<T, TId>
    where T : class, IAggregate<TId>
{
    /// <summary>
    /// Get aggregate with domain events for publishing
    /// </summary>
    Task<T?> GetAggregateWithEventsAsync(TId id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all aggregates that have pending domain events
    /// </summary>
    Task<IReadOnlyList<T>> GetAggregatesWithEventsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only repository interface for queries and data retrieval
/// Supports separation of read concerns from write concerns (CQRS pattern)
/// Compatible with all entity types regardless of inheritance hierarchy
/// </summary>
public interface IReadRepository<T, in TId> : IDisposable where T : class
{
    /// <summary>
    /// Find entity by its primary key
    /// </summary>
    Task<T?> FindByIdAsync(TId id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Find single entity matching predicate
    /// </summary>
    Task<T?> FindOneAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Find all entities matching predicate
    /// </summary>
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all entities (use with caution)
    /// </summary>
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if any entity matches predicate
    /// </summary>
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Count entities matching predicate
    /// </summary>
    Task<long> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if any entities match predicate
    /// </summary>
    Task<bool> AnyAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get entities by their IDs
    /// </summary>
    Task<IReadOnlyList<T>> GetByIdsAsync(IReadOnlyList<TId> ids, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get paginated results
    /// </summary>
    Task<IReadOnlyList<T>> GetAllPaginatedAsync(IPageRequest pageRequest, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get paged results with filtering
    /// </summary>
    Task<IPagedList<T>> GetByPageFilter<TPageRequest>(TPageRequest request, CancellationToken cancellationToken = default) 
        where TPageRequest : IPageRequest;
    
    /// <summary>
    /// Execute raw SQL query
    /// </summary>
    Task<IReadOnlyList<T>> RawQuery(string query, CancellationToken cancellationToken = default, params object[] queryParams);
}

/// <summary>
/// Write-only repository interface for data modifications
/// Supports separation of write concerns from read concerns (CQRS pattern)
/// Compatible with all entity types regardless of inheritance hierarchy
/// </summary>
public interface IWriteRepository<T, in TId> : IDisposable where T : class
{
    /// <summary>
    /// Add new entity
    /// </summary>
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add multiple entities
    /// </summary>
    Task<IReadOnlyList<T>> AddRangeAsync(IReadOnlyList<T> entities, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update existing entity
    /// </summary>
    Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update multiple entities
    /// </summary>
    Task<IReadOnlyList<T>> UpdateRangeAsync(IReadOnlyList<T> entities, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete entities by predicate
    /// </summary>
    Task DeleteAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete specific entity
    /// </summary>
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete entity by ID
    /// </summary>
    Task DeleteByIdAsync(TId id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete multiple entities
    /// </summary>
    Task DeleteRangeAsync(IReadOnlyList<T> entities, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Execute bulk update with predicate
    /// </summary>
    Task<int> BulkUpdateAsync(Expression<Func<T, bool>> predicate, Expression<Func<Microsoft.EntityFrameworkCore.Query.SetPropertyCalls<T>, Microsoft.EntityFrameworkCore.Query.SetPropertyCalls<T>>> updateExpression, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Execute bulk delete with predicate
    /// </summary>
    Task<int> BulkDeleteAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
}


/// <summary>
/// Paged list result with metadata
/// </summary>
public interface IPagedList<T>
{
    IReadOnlyList<T> Items { get; }
    int Page { get; }
    int PageSize { get; }
    int TotalPages { get; }
    int TotalCount { get; }
    bool HasPreviousPage { get; }
    bool HasNextPage { get; }
}

/// <summary>
/// Concrete implementation of page request
/// </summary>
public class PageRequest : IPageRequest
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? Filters { get; init; }
    public string? SortOrder { get; init; }
    
    public PageRequest() { }
    
    public PageRequest(int pageNumber, int pageSize)
    {
        PageNumber = pageNumber;
        PageSize = pageSize;
    }
}

/// <summary>
/// Concrete implementation of paged list
/// </summary>
public class PagedList<T> : IPagedList<T>
{
    public IReadOnlyList<T> Items { get; }
    public int Page { get; }
    public int PageSize { get; }
    public int TotalPages { get; }
    public int TotalCount { get; }
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    public PagedList(IReadOnlyList<T> items, int pageNumber, int pageSize, int totalCount)
    {
        Items = items;
        Page = pageNumber;
        PageSize = pageSize;
        TotalCount = totalCount;
        TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
    }
}