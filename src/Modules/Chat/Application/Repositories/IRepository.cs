using Axon.Shared.Common;
using Axon.Shared.Domain;

namespace Axon.Modules.Chat.Application.Repositories;

/// <summary>
/// Base repository interface following SPARC patterns with Result pattern integration
/// Provides common CRUD operations for all entity types
/// </summary>
/// <typeparam name="TEntity">The entity type</typeparam>
/// <typeparam name="TId">The entity identifier type</typeparam>
public interface IRepository<TEntity, TId>
    where TEntity : BaseEntity<TId>
    where TId : notnull
{
    /// <summary>
    /// Gets an entity by its identifier
    /// </summary>
    Task<Result<TEntity?>> GetByIdAsync(TId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new entity
    /// </summary>
    Task<Result> AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing entity
    /// </summary>
    Task<Result> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an entity by its identifier
    /// </summary>
    Task<Result> DeleteAsync(TId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an entity exists
    /// </summary>
    Task<Result<bool>> ExistsAsync(TId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of entities
    /// </summary>
    Task<Result<int>> CountAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for aggregate roots following DDD patterns
/// Extends base repository with aggregate-specific capabilities
/// </summary>
/// <typeparam name="TAggregate">The aggregate root type</typeparam>
/// <typeparam name="TId">The aggregate identifier type</typeparam>
public interface IAggregateRepository<TAggregate, TId> : IRepository<TAggregate, TId>
    where TAggregate : AuditableAggregateRoot<TId>
    where TId : notnull
{
    /// <summary>
    /// Gets an aggregate with all its related entities (with tracking for domain events)
    /// </summary>
    Task<Result<TAggregate?>> GetWithTrackingAsync(TId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets aggregates that have pending domain events
    /// </summary>
    Task<Result<IReadOnlyList<TAggregate>>> GetAggregatesWithEventsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for domain entities following DDD patterns
/// Extends base repository with entity-specific capabilities
/// </summary>
/// <typeparam name="TEntity">The entity type</typeparam>
/// <typeparam name="TId">The entity identifier type</typeparam>
public interface IEntityRepository<TEntity, TId> : IRepository<TEntity, TId>
    where TEntity : AuditableEntity<TId>
    where TId : notnull
{
    /// <summary>
    /// Gets entities by their parent aggregate identifier
    /// </summary>
    Task<Result<IReadOnlyList<TEntity>>> GetByAggregateAsync<TAggregateId>(TAggregateId aggregateId, CancellationToken cancellationToken = default)
        where TAggregateId : notnull;

    /// <summary>
    /// Gets paged entities by their parent aggregate identifier
    /// </summary>
    Task<Result<PagedResult<TEntity>>> GetPagedByAggregateAsync<TAggregateId>(
        TAggregateId aggregateId, 
        int pageNumber = 1, 
        int pageSize = 20, 
        CancellationToken cancellationToken = default)
        where TAggregateId : notnull;
}