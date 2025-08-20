#nullable enable
using System.Linq.Expressions;
using BuildingBlocks.Core.Domain.Entities.Abstractions;

namespace BuildingBlocks.Application;

/// <summary>
/// Write-side repository abstraction for aggregate roots (full tracking).
/// Provider-agnostic: no IQueryable/Include/raw SQL exposure.
/// </summary>
public interface IWriteRepository<TAggregate, TId> : IDisposable
    where TAggregate : class, IAggregateRoot<TId>
    where TId : notnull
{
    // C R E A T E
    Task<TAggregate> AddAsync(TAggregate aggregate, CancellationToken ct = default);
    Task<IReadOnlyList<TAggregate>> AddRangeAsync(IReadOnlyList<TAggregate> aggregates, CancellationToken ct = default);

    // U P D A T E
    Task<TAggregate> UpdateAsync(TAggregate aggregate, CancellationToken ct = default);
    Task<IReadOnlyList<TAggregate>> UpdateRangeAsync(IReadOnlyList<TAggregate> aggregates, CancellationToken ct = default);

    // R E A D (for modification)
    Task<TAggregate?> GetByIdAsync(TId id, CancellationToken ct = default);

    // D E L E T E
    Task DeleteAsync(TId id, CancellationToken ct = default);
    Task DeleteAsync(TAggregate aggregate, CancellationToken ct = default);
    Task DeleteRangeAsync(IReadOnlyList<TAggregate> aggregates, CancellationToken ct = default);

    // Existence / Any
    Task<bool> ExistsAsync(TId id, CancellationToken ct = default);
    Task<bool> AnyAsync(Expression<Func<TAggregate, bool>> predicate, CancellationToken ct = default);
    Task<bool> AnyAsync(CancellationToken ct); // convenience
}

/// <summary>
/// Convenience shortcut for Guid keys.
/// </summary>
public interface IWriteRepository<TAggregate> : IWriteRepository<TAggregate, Guid>
    where TAggregate : class, IAggregateRoot<Guid>
{
}
