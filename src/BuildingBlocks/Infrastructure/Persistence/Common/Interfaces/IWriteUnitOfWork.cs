using System.Data;
using BuildingBlocks.Core.Event;

namespace BuildingBlocks.Persistence.Common.Interfaces;

/// <summary>
/// Write-side Unit-of-Work (transaction + domain-events).
/// </summary>
public interface IWriteUnitOfWork : IDisposable
{
    // ——— P e r s i s t e n c e ———
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Convenience helper: persists changes <i>and</i> publishes domain events
    /// in one call.  (<b>High</b> priority DX addition)
    /// </summary>
    Task<int> SaveChangesAndDispatchEventsAsync(CancellationToken ct = default); // ★ new

    // ——— T r a n s a c t i o n ———
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(IsolationLevel level, CancellationToken ct = default); // ★ new
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);

    bool HasChanges { get; }
    bool HasActiveTransaction { get; }
    string? CurrentTransactionId { get; } // ★ new parity with IDbContext

    // ——— D o m a i n  e v e n t s ———
    IReadOnlyList<IDomainEvent> GetDomainEvents();
    bool HasDomainEvents { get; } // ★ new
    void ClearDomainEvents();

    // ——— E x e c u t e  i n  t x ———
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation,
        CancellationToken ct = default);

    Task ExecuteInTransactionAsync(Func<Task> operation,
        CancellationToken ct = default);
}

/// <summary>Typed UoW exposing its concrete write DbContext.</summary>
public interface IWriteUnitOfWork<out TWriteContext> : IWriteUnitOfWork
    where TWriteContext : class, IWriteDbContext<object>
{
    TWriteContext Context { get; }
}