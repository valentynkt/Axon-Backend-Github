using System.Linq.Expressions;
using BuildingBlocks.Core.Model;

namespace BuildingBlocks.Mongo;

public interface IRepository<T> : IRepository<T, Guid> where T : class, IEntity<Guid>;

public interface IRepository<T, in TId> where T : class, IEntity<TId>
{
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> AddRangeAsync(IReadOnlyList<T> entities, CancellationToken cancellationToken = default);
    Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task<T?> FindByIdAsync(TId id, CancellationToken cancellationToken = default);
    Task<T?> FindOneAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task DeleteRangeAsync(IReadOnlyList<T> entities, CancellationToken cancellationToken = default);
    Task DeleteAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteByIdAsync(TId id, CancellationToken cancellationToken = default);
    Task<long> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetByIdsAsync(IReadOnlyList<TId> ids, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetAllPaginatedAsync(IPageRequest pageRequest, CancellationToken cancellationToken = default);
    Task<IPagedList<T>> GetByPageFilter<TPageRequest>(TPageRequest request, CancellationToken cancellationToken = default) 
        where TPageRequest : IPageRequest;
}

public interface IPageRequest
{
    int Page { get; }
    int PageSize { get; }
}

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