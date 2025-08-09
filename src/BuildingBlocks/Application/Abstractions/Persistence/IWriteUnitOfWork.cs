using System.Data;
using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Application.Abstractions.Persistence;

/// <summary>
/// Application-layer Unit of Work: transaction boundary + domain event dispatch coordination.
/// Provider-agnostic; infra implements with EF/Dapper/etc.
/// </summary>
public interface IWriteUnitOfWork : IDisposable
{
    // ——— P e r s i s t e n c e ———
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Persist changes and publish domain events in a single call.
    /// </summary>
    Task<int> SaveChangesAndDispatchEventsAsync(CancellationToken ct = default);

    // ——— T r a n s a c t i o n ———
    Task<Result<ITransaction>> BeginTransactionAsync(CancellationToken ct = default);
    Task<Result<ITransaction>> BeginTransactionAsync(IsolationLevel level, CancellationToken ct = default);

    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);

    bool HasChanges { get; }
    bool HasActiveTransaction { get; }
    string? CurrentTransactionId { get; }

    // ——— D o m a i n  e v e n t s ———
    IReadOnlyList<IDomainEvent> GetDomainEvents();
    bool HasDomainEvents { get; }
    void ClearDomainEvents();

    // ——— E x e c u t e  i n  t x ———
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken ct = default);
    Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct = default);
}