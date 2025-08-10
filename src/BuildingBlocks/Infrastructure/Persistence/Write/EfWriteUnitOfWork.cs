using System.Data;
using BuildingBlocks.Application;
using BuildingBlocks.Core.Domain.Entities.Abstractions;
using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Diagnostics.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Persistence.Write;

/// <summary>
/// ⚠️ Transitional EF Unit of Work.
/// - Keeps transaction helpers and SaveChanges
/// - NO LONGER DISPATCHES DOMAIN EVENTS (Application layer owns that)
/// - Kept to avoid breaking brownfield code while you migrate callers to DbContext/Repositories
/// </summary>
public class EfWriteUnitOfWork : IWriteUnitOfWork, IDisposable
{
    private readonly DbContext _context;
    private readonly ILogger<EfWriteUnitOfWork> _logger;
    private IDbContextTransaction? _currentTransaction;
    private bool _disposed;

    public EfWriteUnitOfWork(DbContext context, ILogger<EfWriteUnitOfWork>? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<EfWriteUnitOfWork>.Instance;
    }

    // ——— P e r s i s t e n c e ———
    public virtual Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);

    /// <summary>
    /// ❗️Deprecated: Domain events are published by Application behaviors after commit.
    /// This method now simply saves changes. It does NOT publish or clear domain events.
    /// </summary>
    [Obsolete("Domain events are published post-commit by Application layer (IPostCommitDomainEventPublisher). Use SaveChangesAsync().")]
    public virtual Task<int> SaveChangesAndDispatchEventsAsync(CancellationToken ct = default)
    {
        _logger.LogWarning("SaveChangesAndDispatchEventsAsync is obsolete. It will NOT dispatch domain events. Use SaveChangesAsync and rely on Application behaviors.");
        return SaveChangesAsync(ct);
    }

    // ——— T r a n s a c t i o n ———
    public virtual async Task<Result<ITransaction>> BeginTransactionAsync(CancellationToken ct = default)
    {
        try
        {
            if (_currentTransaction != null)
                return Result<ITransaction>.Success(new EfTransaction(_currentTransaction));

            _currentTransaction = await _context.Database.BeginTransactionAsync(ct);
            return Result<ITransaction>.Success(new EfTransaction(_currentTransaction));
        }
        catch (Exception ex)
        {
            return Result<ITransaction>.Failure(Error.Persistence($"Failed to start transaction: {ex.Message}", "TRANSACTION_START_FAILED", ex));
        }
    }

    public virtual async Task<Result<ITransaction>> BeginTransactionAsync(IsolationLevel level, CancellationToken ct = default)
    {
        try
        {
            if (_currentTransaction != null)
                return Result<ITransaction>.Success(new EfTransaction(_currentTransaction));

            _currentTransaction = await _context.Database.BeginTransactionAsync(level, ct);
            return Result<ITransaction>.Success(new EfTransaction(_currentTransaction));
        }
        catch (Exception ex)
        {
            return Result<ITransaction>.Failure(Error.Persistence($"Failed to start transaction: {ex.Message}", "TRANSACTION_START_FAILED", ex));
        }
    }

    public virtual async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_currentTransaction == null)
            throw new InvalidOperationException("No active transaction to commit.");

        await _currentTransaction.CommitAsync(ct);
        await _currentTransaction.DisposeAsync();
        _currentTransaction = null;
    }

    public virtual async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        if (_currentTransaction == null)
            throw new InvalidOperationException("No active transaction to rollback.");

        await _currentTransaction.RollbackAsync(ct);
        await _currentTransaction.DisposeAsync();
        _currentTransaction = null;
    }

    public bool HasChanges => _context.ChangeTracker.HasChanges();
    public bool HasActiveTransaction => _currentTransaction != null;
    public string? CurrentTransactionId => _currentTransaction?.TransactionId.ToString();

    // ——— D o m a i n  e v e n t s (collection only; no dispatch/clear here) ———
    public virtual IReadOnlyList<IDomainEvent> GetDomainEvents()
    {
        var domainEntities = _context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Where(x => x.Entity.DomainEvents?.Any() == true)
            .Select(x => x.Entity)
            .ToList();

        return domainEntities.SelectMany(x => x.DomainEvents).ToList().AsReadOnly();
    }

    public bool HasDomainEvents => GetDomainEvents().Any();

    /// <summary>
    /// Keep available in case old callers rely on it, but DO NOT clear here by default,
    /// otherwise you’ll steal events from Application post-commit publisher.
    /// </summary>
    public virtual void ClearDomainEvents()
    {
        _logger.LogDebug("ClearDomainEvents() called on EfWriteUnitOfWork. Prefer letting Application post-commit publisher clear after publish.");
        var domainEntities = _context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Where(x => x.Entity.DomainEvents?.Any() == true)
            .Select(x => x.Entity)
            .ToList();

        domainEntities.ForEach(e => e.ClearDomainEvents());
    }

    // ——— E x e c u t e  i n  t x ———
    public virtual async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                var result = await operation();
                await tx.CommitAsync(ct);
                return result;
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });
    }

    public virtual Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct = default)
        => ExecuteInTransactionAsync(async () => { await operation(); return 0; }, ct);

    // ——— D i s p o s a l ———
    public void Dispose()
    {
        if (_disposed) return;
        _currentTransaction?.Dispose();
        _context?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

internal sealed class EfTransaction : ITransaction
{
    private readonly IDbContextTransaction _transaction;
    private bool _disposed;

    public EfTransaction(IDbContextTransaction transaction)
    {
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        TransactionId = _transaction.TransactionId.ToString();
    }

    public string TransactionId { get; }
    public bool IsCompleted { get; private set; }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (IsCompleted) throw new InvalidOperationException("Transaction has already been completed.");
        await _transaction.CommitAsync(ct);
        IsCompleted = true;
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (IsCompleted) throw new InvalidOperationException("Transaction has already been completed.");
        await _transaction.RollbackAsync(ct);
        IsCompleted = true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _transaction.Dispose();
        _disposed = true;
    }
}
