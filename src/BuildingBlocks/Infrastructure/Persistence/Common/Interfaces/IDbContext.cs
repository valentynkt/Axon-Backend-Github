using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using BuildingBlocks.Core.Event;

namespace BuildingBlocks.Persistence.Common.Interfaces;

/// <summary>
/// Unified database context interface for all modules
/// Provides common transaction and persistence operations
/// </summary>
public interface IDbContext : IDisposable
{
    DbSet<TEntity> Set<TEntity>() where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    // ——— Transaction control ———
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

    bool HasActiveTransaction { get; }
    string? CurrentTransactionId { get; }

    IExecutionStrategy CreateExecutionStrategy();

    // ★ Removed parameter-less overload (ambiguous & unused)
    Task ExecuteTransactionalAsync(
        Func<Task> operation,
        CancellationToken cancellationToken = default);

    Task<T> ExecuteTransactionalAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken = default);

    IReadOnlyList<IDomainEvent> GetDomainEvents();
}