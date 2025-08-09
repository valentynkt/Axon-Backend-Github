using System.Linq.Expressions;
using BuildingBlocks.Core.Abstractions.Pagination;

namespace BuildingBlocks.Application.Abstractions.Persistence;

/// <summary>
/// Read-side repository abstraction (query only, <b>no tracking</b>).
/// Provider-agnostic: no IQueryable/raw SQL/compiled-query exposure.
/// </summary>
public interface IReadRepository<TReadModel, in TId> : IDisposable
    where TReadModel : class
    where TId : notnull
{
    // ——— Simple fetches ———
    Task<TReadModel?> FindByIdAsync(TId id, CancellationToken ct = default);

    /// <summary>
    /// Returns a single item that matches the predicate or null.
    /// NOTE: The expression is a query description; infra translates it.
    /// </summary>
    Task<TReadModel?> FindOneAsync(
        Expression<Func<TReadModel, bool>> predicate,
        CancellationToken ct = default);

    Task<IReadOnlyList<TReadModel>> FindAsync(
        Expression<Func<TReadModel, bool>> predicate,
        CancellationToken ct = default);

    Task<IReadOnlyList<TReadModel>> GetByIdsAsync(
        IReadOnlyList<TId> ids,
        CancellationToken ct = default);

    // ——— Paged / filtered ———
    Task<IPageList<TReadModel>> GetPagedAsync<TPageRequest>(
        TPageRequest request,
        CancellationToken ct = default)
        where TPageRequest : IPageRequest;

    Task<IPageList<TReadModel>> GetPagedAsync(
        Expression<Func<TReadModel, bool>>? predicate,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default);

    // ——— Aggregate functions ———
    Task<long> CountAsync(
        Expression<Func<TReadModel, bool>>? predicate = null,
        CancellationToken ct = default);

    Task<long> CountAsync(CancellationToken ct); // convenience

    Task<bool> AnyAsync(
        Expression<Func<TReadModel, bool>>? predicate = null,
        CancellationToken ct = default);

    Task<bool> AnyAsync(CancellationToken ct); // convenience
}

/// <summary> Convenience shortcut for Guid keys. </summary>
public interface IReadRepository<TReadModel> : IReadRepository<TReadModel, Guid>
    where TReadModel : class
{
}
