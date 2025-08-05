using BuildingBlocks.Core.Pagination;
using System.Linq.Expressions;

namespace BuildingBlocks.Persistence.Common.Interfaces;

/// <summary>
/// Read-side database context for CQRS query operations
/// Optimized for read models and query performance
/// </summary>
public interface IReadDbContext<TModule> : IDbContext where TModule : class
{
    /// <summary>
    /// Module name for schema separation
    /// </summary>
    string ModuleName { get; }
    
    /// <summary>
    /// Get queryable for read model with no tracking
    /// </summary>
    IQueryable<TReadModel> Query<TReadModel>() where TReadModel : class;
    
    /// <summary>
    /// Execute compiled query for performance
    /// </summary>
    Task<TResult> ExecuteCompiledQueryAsync<TResult>(
        Func<IReadDbContext<TModule>, Task<TResult>> compiledQuery,
        CancellationToken cancellationToken = default);
}