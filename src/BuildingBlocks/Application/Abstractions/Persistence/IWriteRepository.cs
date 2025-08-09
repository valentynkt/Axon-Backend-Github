using System.Linq.Expressions;
using BuildingBlocks.Core.Domain.Entities.Abstractions;
using BuildingBlocks.Core.Domain.Primitives;

namespace BuildingBlocks.Application.Abstractions.Persistence;

/// <summary>
/// Write-side repository abstraction for aggregate roots (full tracking).
/// Provider-agnostic: no includes/IQueryable/raw SQL exposure.
/// </summary>
public interface IWriteRepository<TAggregate, in TId> : IDisposable
    where TAggregate : class, IAggregateRoot<TId>
    where TId : IStrongId
{
    // ——— C R E A T E ———
    Task<TAggregate> AddAsync(TAggregate aggregate, CancellationToken ct = default);
    Task<IReadOnlyList<TAggregate>> AddRangeAsync(IReadOnlyList<TAggregate> aggregates, CancellationToken ct = default);

    // ——— U P D A T E ———
    Task<TAggregate> UpdateAsync(TAggregate aggregate, CancellationToken ct = default);
    Task<IReadOnlyList<TAggregate>> UpdateRangeAsync(IReadOnlyList<TAggregate> aggregates, CancellationToken ct = default);

    // ——— R E A D  (for modification) ———
    Task<TAggregate?> GetByIdAsync(TId id, CancellationToken ct = default);

    // ——— D E L E T E ———
    Task DeleteAsync(TId id, CancellationToken ct = default);
    Task DeleteAsync(TAggregate aggregate, CancellationToken ct = default);
    Task DeleteRangeAsync(IReadOnlyList<TAggregate> aggregates, CancellationToken ct = default);

    // ——— E x i s t e n c e  /  A n y ———
    Task<bool> ExistsAsync(TId id, CancellationToken ct = default);

    Task<bool> AnyAsync(Expression<Func<TAggregate, bool>> predicate, CancellationToken ct = default);
    Task<bool> AnyAsync(CancellationToken ct); // convenience
}

/// <summary>
/// Convenience shortcut for StrongId&lt;Guid&gt;-based aggregates.
/// </summary>
public interface IWriteRepository<TAggregate> : IWriteRepository<TAggregate, StrongId<Guid>>
    where TAggregate : class, IAggregateRoot<StrongId<Guid>>
{
}