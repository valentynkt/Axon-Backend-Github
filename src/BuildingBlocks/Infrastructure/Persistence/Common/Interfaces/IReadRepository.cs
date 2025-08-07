using BuildingBlocks.Core.Pagination;
using System.Linq.Expressions;

namespace BuildingBlocks.Persistence.Common.Interfaces;

/// <summary>
/// Read-side repository (query only, <b>no tracking</b>).
/// </summary>
public interface IReadRepository<TReadModel, in TId> : IDisposable
    where TReadModel : class
    where TId : notnull
{
    // ——— Simple fetches ———
    Task<TReadModel?> FindByIdAsync(TId id, CancellationToken ct = default);
    Task<TReadModel?> FindOneAsync(Expression<Func<TReadModel, bool>> predicate, CancellationToken ct = default);

    Task<IReadOnlyList<TReadModel>> FindAsync(Expression<Func<TReadModel, bool>> predicate,
        CancellationToken ct = default);

    Task<IReadOnlyList<TReadModel>> GetByIdsAsync(IReadOnlyList<TId> ids, CancellationToken ct = default);

    // ——— Paged / filtered ———
    Task<IPagedResult<TReadModel>> GetPagedAsync<TPageRequest>(TPageRequest request, CancellationToken ct = default)
        where TPageRequest : IPageRequest;

    Task<IPagedResult<TReadModel>> GetPagedAsync(Expression<Func<TReadModel, bool>>? predicate,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default);

    // ——— Aggregate functions ———
    Task<long> CountAsync(Expression<Func<TReadModel, bool>>? predicate = null, CancellationToken ct = default);
    Task<long> CountAsync(CancellationToken ct); // ★ new  – no predicate overload
    Task<bool> AnyAsync(Expression<Func<TReadModel, bool>>? predicate = null, CancellationToken ct = default);
    Task<bool> AnyAsync(CancellationToken ct); // ★ new

    // ——— Raw / compiled ———
    Task<IReadOnlyList<TReadModel>> RawQueryAsync(string sql, CancellationToken ct = default,
        params object[] parameters);

    Task<TResult> ExecuteCompiledQueryAsync<TResult>(Func<IQueryable<TReadModel>, Task<TResult>> compiledQuery,
        CancellationToken ct = default);

    // ——— Advanced composition ———
    IQueryable<TReadModel> Query(); // still no-tracking
}

/// <summary> Convenience shortcut for Guid keys. </summary>
public interface IReadRepository<TReadModel> : IReadRepository<TReadModel, Guid> where TReadModel : class
{
}