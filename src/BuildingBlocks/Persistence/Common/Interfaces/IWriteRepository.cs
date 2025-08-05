using BuildingBlocks.Core.Model;
using System.Linq.Expressions;

namespace BuildingBlocks.Persistence.Common.Interfaces;

/// <summary>
/// Write repository interface for CQRS command operations
/// Handles aggregates with domain events and transactional consistency
/// </summary>
public interface IWriteRepository<TAggregate, in TId> : IDisposable 
    where TAggregate : class, IAggregate<TId>
    where TId : notnull
{
    /// <summary>
    /// Add new aggregate to the repository
    /// </summary>
    Task<TAggregate> AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add multiple aggregates to the repository
    /// </summary>
    Task<IReadOnlyList<TAggregate>> AddRangeAsync(
        IReadOnlyList<TAggregate> aggregates, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update existing aggregate
    /// </summary>
    Task<TAggregate> UpdateAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update multiple aggregates
    /// </summary>
    Task<IReadOnlyList<TAggregate>> UpdateRangeAsync(
        IReadOnlyList<TAggregate> aggregates, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get aggregate by ID for modification (with change tracking)
    /// </summary>
    Task<TAggregate?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get aggregate by ID with specified includes for modification
    /// </summary>
    Task<TAggregate?> GetByIdAsync(
        TId id, 
        params Expression<Func<TAggregate, object>>[] includes);
    
    /// <summary>
    /// Delete aggregate by ID
    /// </summary>
    Task DeleteAsync(TId id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete aggregate entity
    /// </summary>
    Task DeleteAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete multiple aggregates
    /// </summary>
    Task DeleteRangeAsync(
        IReadOnlyList<TAggregate> aggregates, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if aggregate exists
    /// </summary>
    Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if any aggregate matches predicate
    /// </summary>
    Task<bool> AnyAsync(
        Expression<Func<TAggregate, bool>> predicate, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Simplified write repository interface for aggregates with Guid IDs
/// </summary>
public interface IWriteRepository<TAggregate> : IWriteRepository<TAggregate, Guid> 
    where TAggregate : class, IAggregate<Guid>
{
}