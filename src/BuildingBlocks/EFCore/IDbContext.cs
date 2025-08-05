using BuildingBlocks.Core.Event;
using BuildingBlocks.Core.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BuildingBlocks.EFCore;

/// <summary>
/// Pure PostgreSQL database context interface
/// Clean, high-performance contract without legacy dependencies
/// 
/// DEPRECATED: Use BuildingBlocks.Postgres interfaces instead for new development
/// This interface is maintained for backward compatibility only
/// </summary>
[Obsolete("Use BuildingBlocks.Postgres.IPostgresDbContext instead. This interface will be removed in a future version.")]
public interface IDbContext
{
    DbSet<TEntity> Set<TEntity>() where TEntity : class;
    IReadOnlyList<IDomainEvent> GetDomainEvents();
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    IExecutionStrategy CreateExecutionStrategy();
    Task ExecuteTransactionalAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Pure PostgreSQL repository interface
/// High-performance, clean architecture contract
/// 
/// DEPRECATED: Use BuildingBlocks.Postgres.IRepository interfaces instead
/// </summary>
[Obsolete("Use BuildingBlocks.Postgres.IRepository<T, TId> instead. This interface will be removed in a future version.")]
public interface IEfRepository<TEntity, in TId> : IDisposable
    where TEntity : class, IAggregate<TId>
{
    Task<TEntity?> FindByIdAsync(TId id, CancellationToken cancellationToken = default);
    Task<TEntity?> FindOneAsync(System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TEntity>> FindAsync(System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TEntity>> RawQuery(string query, CancellationToken cancellationToken = default, params object[] queryParams);
    Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task DeleteRangeAsync(IReadOnlyList<TEntity> entities, CancellationToken cancellationToken = default);
    Task DeleteAsync(System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);
    Task DeleteByIdAsync(TId id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Pure PostgreSQL repository interface for long ID entities
/// </summary>
public interface IEfRepository<TEntity> : IEfRepository<TEntity, long>
    where TEntity : class, IAggregate<long>
{
}

/// <summary>
/// Pure PostgreSQL Unit of Work interface
/// Transaction management without legacy dependencies
/// </summary>
public interface IEfUnitOfWork : IDisposable
{
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Pure PostgreSQL Unit of Work interface with typed context
/// </summary>
public interface IEfUnitOfWork<out TContext> : IEfUnitOfWork
    where TContext : class
{
    TContext Context { get; }
}
