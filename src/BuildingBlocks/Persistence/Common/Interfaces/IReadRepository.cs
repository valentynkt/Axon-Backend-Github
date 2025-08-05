using BuildingBlocks.Core.Pagination;
using System.Linq.Expressions;

namespace BuildingBlocks.Persistence.Common.Interfaces;

/// <summary>
/// Read repository interface for CQRS query operations
/// Optimized for read models with no tracking and query performance
/// </summary>
public interface IReadRepository<TReadModel, in TId> : IDisposable 
    where TReadModel : class
    where TId : notnull
{
    /// <summary>
    /// Find read model by ID (no tracking)
    /// </summary>
    Task<TReadModel?> FindByIdAsync(TId id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Find single read model matching predicate (no tracking)
    /// </summary>
    Task<TReadModel?> FindOneAsync(
        Expression<Func<TReadModel, bool>> predicate, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Find all read models matching predicate (no tracking)
    /// </summary>
    Task<IReadOnlyList<TReadModel>> FindAsync(
        Expression<Func<TReadModel, bool>> predicate, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get paginated results with filtering and sorting
    /// </summary>
    Task<IPagedResult<TReadModel>> GetPagedAsync<TPageRequest>(
        TPageRequest request, 
        CancellationToken cancellationToken = default) 
        where TPageRequest : IPageRequest;
    
    /// <summary>
    /// Get paginated results with custom predicate
    /// </summary>
    Task<IPagedResult<TReadModel>> GetPagedAsync(
        Expression<Func<TReadModel, bool>>? predicate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Execute raw SQL query for complex scenarios
    /// </summary>
    Task<IReadOnlyList<TReadModel>> RawQueryAsync(
        string sql, 
        CancellationToken cancellationToken = default, 
        params object[] parameters);
    
    /// <summary>
    /// Get count of read models matching predicate
    /// </summary>
    Task<long> CountAsync(
        Expression<Func<TReadModel, bool>>? predicate = null, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if any read models match predicate
    /// </summary>
    Task<bool> AnyAsync(
        Expression<Func<TReadModel, bool>>? predicate = null, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get read models by multiple IDs
    /// </summary>
    Task<IReadOnlyList<TReadModel>> GetByIdsAsync(
        IReadOnlyList<TId> ids, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get queryable for advanced query composition (no tracking)
    /// </summary>
    IQueryable<TReadModel> Query();
    
    /// <summary>
    /// Execute compiled query for maximum performance
    /// </summary>
    Task<TResult> ExecuteCompiledQueryAsync<TResult>(
        Func<IQueryable<TReadModel>, Task<TResult>> compiledQuery,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Simplified read repository interface for read models with Guid IDs
/// </summary>
public interface IReadRepository<TReadModel> : IReadRepository<TReadModel, Guid> 
    where TReadModel : class
{
}