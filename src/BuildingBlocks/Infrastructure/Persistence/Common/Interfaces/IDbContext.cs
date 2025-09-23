using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using BuildingBlocks.Core.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;

/// <summary>
/// Unified database context port (Infrastructure).
/// NOTE:
/// - Domain must not depend on this.
/// - Application should prefer repositories/UoW and pipeline behaviors.
/// - Infrastructure provides EF (or other) implementations.
/// </summary>
public interface IDbContext : IDisposable, IAsyncDisposable
{
    /// <summary>DbSet accessor. Prefer repositories in Application.</summary>
    DbSet<TEntity> Set<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties | DynamicallyAccessedMemberTypes.Interfaces)] TEntity>() where TEntity : class;

    /// <summary>Persist pending changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    // ---------- Transactions (Infra-owned) ----------
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>True if a database transaction is currently active.</summary>
    bool HasActiveTransaction { get; }

    /// <summary>Opaque transaction id for diagnostics/correlation.</summary>
    string? CurrentTransactionId { get; }

    /// <summary>Provider execution strategy (EF-specific).</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    IExecutionStrategy CreateExecutionStrategy();

    /// <summary>Execute an operation within a transaction boundary.</summary>
    Task ExecuteTransactionalAsync(
        Func<Task> operation,
        CancellationToken cancellationToken = default);

    /// <summary>Execute an operation within a transaction boundary and return a result.</summary>
    Task<T> ExecuteTransactionalAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Collect domain events from tracked aggregates (for Application post-commit publishing).
    /// Implementations should not dispatch here; only collect.
    /// </summary>
    IReadOnlyList<IDomainEvent> GetDomainEvents();
}
