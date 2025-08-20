#nullable enable
using System.Linq.Expressions;

namespace BuildingBlocks.Application;

/// <summary>
/// Read-side repository abstraction (query-only, no tracking).
/// Returns IQueryable for composition (OData/EF Core).
/// </summary>
public interface IReadRepository<TReadModel, in TId> : IDisposable
    where TReadModel : class
    where TId : notnull
{
    // Query builder (projectors can compose on top)
    IQueryable<TReadModel> Query(Expression<Func<TReadModel, bool>>? predicate = null);

    // Simple fetches
    Task<TReadModel?> FindByIdAsync(TId id, CancellationToken ct = default);

    Task<TReadModel?> FindOneAsync(
        Expression<Func<TReadModel, bool>> predicate,
        CancellationToken ct = default);

    Task<IReadOnlyList<TReadModel>> FindAsync(
        Expression<Func<TReadModel, bool>> predicate,
        CancellationToken ct = default);

    Task<IReadOnlyList<TReadModel>> GetByIdsAsync(
        IReadOnlyList<TId> ids,
        CancellationToken ct = default);

    // Aggregates
    Task<long> CountAsync(
        Expression<Func<TReadModel, bool>>? predicate = null,
        CancellationToken ct = default);

    Task<long> CountAsync(CancellationToken ct); // convenience

    Task<bool> AnyAsync(
        Expression<Func<TReadModel, bool>>? predicate = null,
        CancellationToken ct = default);

    Task<bool> AnyAsync(CancellationToken ct); // convenience
}

/// <summary>Convenience shortcut for Guid keys.</summary>
public interface IReadRepository<TReadModel> : IReadRepository<TReadModel, Guid>
    where TReadModel : class
{
}