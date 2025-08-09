using System.ComponentModel;
using BuildingBlocks.Core.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;

/// <summary>
/// Unified database context interface (Infrastructure port).
/// 
/// Clean Architecture guidance:
/// - <b>Domain</b>: must not depend on this.
/// - <b>Application</b>: avoid using DbContext directly; prefer repositories/UoW ports.
/// - <b>Infrastructure</b>: provides the implementation (EF Core, Dapper, etc.).
/// 
/// Keep the surface provider-agnostic where possible. EF-specific helpers are marked as Advanced.
/// </summary>
public interface IDbContext : IDisposable
{
    /// <summary>
    /// Returns a <see cref="DbSet{TEntity}"/> for the given entity type.
    /// Application code should prefer repositories over raw DbSet access.
    /// </summary>
    DbSet<TEntity> Set<TEntity>() where TEntity : class;

    /// <summary>
    /// Persist pending changes.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    // ——— Transaction control ———

    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether an infrastructure transaction is active (diagnostics only).</summary>
    bool HasActiveTransaction { get; }

    /// <summary>Opaque id for logging/correlation (diagnostics only).</summary>
    string? CurrentTransactionId { get; }

    /// <summary>
    /// Provider execution strategy (EF-specific). Marked Advanced to discourage use in Application layer.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    IExecutionStrategy CreateExecutionStrategy();

    /// <summary>
    /// Execute the given operation inside a transaction boundary.
    /// </summary>
    Task ExecuteTransactionalAsync(
        Func<Task> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute the given operation inside a transaction boundary and return a result.
    /// </summary>
    Task<T> ExecuteTransactionalAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Collect domain events from tracked aggregates (to be published by the Application layer).
    /// </summary>
    IReadOnlyList<IDomainEvent> GetDomainEvents();
}
