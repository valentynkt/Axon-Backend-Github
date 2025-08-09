using System.Data;
using BuildingBlocks.Application.Abstractions.Persistence;
using BuildingBlocks.Core.Domain.Entities.Abstractions;
using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Diagnostics.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BuildingBlocks.Infrastructure.Persistence.Write;

/// <summary>
/// Entity Framework implementation of ITransaction
/// </summary>
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
        if (IsCompleted)
            throw new InvalidOperationException("Transaction has already been completed.");
            
        await _transaction.CommitAsync(ct);
        IsCompleted = true;
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (IsCompleted)
            throw new InvalidOperationException("Transaction has already been completed.");
            
        await _transaction.RollbackAsync(ct);
        IsCompleted = true;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _transaction.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Generic Entity Framework Unit of Work implementation
/// Handles transactional consistency and domain event processing
/// </summary>
public class EfWriteUnitOfWork : IWriteUnitOfWork
{
    private readonly DbContext _context;
    private readonly IDomainEventDispatcher? _eventDispatcher;
    private IDbContextTransaction? _currentTransaction;
    private bool _disposed;

    public EfWriteUnitOfWork(DbContext context, IDomainEventDispatcher? eventDispatcher = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _eventDispatcher = eventDispatcher;
    }

    // ——— P e r s i s t e n c e ———
    public virtual async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }

    public virtual async Task<int> SaveChangesAndDispatchEventsAsync(CancellationToken ct = default)
    {
        // Collect domain events before saving
        var domainEvents = GetDomainEvents().ToList();
        
        // Save changes to database
        var result = await SaveChangesAsync(ct);
        
        // Dispatch domain events after successful persistence
        if (_eventDispatcher != null && domainEvents.Any())
        {
            foreach (var @event in domainEvents)
            {
                await _eventDispatcher.PublishAsync(@event, ct);
            }
        }
        
        // Clear domain events after dispatching
        ClearDomainEvents();
        
        return result;
    }

    // ——— T r a n s a c t i o n ———
    public virtual async Task<Result<ITransaction>> BeginTransactionAsync(CancellationToken ct = default)
    {
        try
        {
            if (_currentTransaction != null)
            {
                // Return existing transaction wrapped in our interface
                return Result<ITransaction>.Success(new EfTransaction(_currentTransaction));
            }

            _currentTransaction = await _context.Database.BeginTransactionAsync(ct);
            return Result<ITransaction>.Success(new EfTransaction(_currentTransaction));
        }
        catch (Exception ex)
        {
            return Result<ITransaction>.Failure(
                new Error("TRANSACTION_START_FAILED", $"Failed to start transaction: {ex.Message}"));
        }
    }

    public virtual async Task<Result<ITransaction>> BeginTransactionAsync(
        IsolationLevel level, 
        CancellationToken ct = default)
    {
        try
        {
            if (_currentTransaction != null)
            {
                // Return existing transaction wrapped in our interface
                return Result<ITransaction>.Success(new EfTransaction(_currentTransaction));
            }

            _currentTransaction = await _context.Database.BeginTransactionAsync(level, ct);
            return Result<ITransaction>.Success(new EfTransaction(_currentTransaction));
        }
        catch (Exception ex)
        {
            return Result<ITransaction>.Failure(
                new Error("TRANSACTION_START_FAILED", $"Failed to start transaction: {ex.Message}"));
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

    // ——— D o m a i n  e v e n t s ———
    public virtual IReadOnlyList<IDomainEvent> GetDomainEvents()
    {
        var domainEntities = _context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Where(x => x.Entity.DomainEvents?.Any() == true)
            .Select(x => x.Entity)
            .ToList();

        var domainEvents = domainEntities
            .SelectMany(x => x.DomainEvents)
            .ToList();

        return domainEvents.AsReadOnly();
    }

    public bool HasDomainEvents => GetDomainEvents().Any();

    public virtual void ClearDomainEvents()
    {
        var domainEntities = _context.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Where(x => x.Entity.DomainEvents?.Any() == true)
            .Select(x => x.Entity)
            .ToList();

        domainEntities.ForEach(entity => entity.ClearDomainEvents());
    }

    // ——— E x e c u t e  i n  t x ———
    public virtual async Task<T> ExecuteInTransactionAsync<T>(
        Func<Task<T>> operation, 
        CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                var result = await operation();
                await transaction.CommitAsync(ct);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    public virtual async Task ExecuteInTransactionAsync(
        Func<Task> operation, 
        CancellationToken ct = default)
    {
        await ExecuteInTransactionAsync(async () =>
        {
            await operation();
            return 0; // Dummy return value
        }, ct);
    }

    // ——— D i s p o s a l ———
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _currentTransaction?.Dispose();
                _context?.Dispose();
            }
            _disposed = true;
        }
    }
}