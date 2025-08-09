using System.Data;
using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Persistence.Write;

/// <summary>
/// Entity Framework implementation of ITransaction
/// </summary>
internal sealed class EfTransaction : ITransaction
{
    private readonly Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction _transaction;
    private bool _disposed;

    public EfTransaction(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
    {
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        Id = _transaction.TransactionId.ToString();
    }

    public string Id { get; }
    public string TransactionId => Id;
    public bool IsCompleted { get; private set; }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await _transaction.CommitAsync(cancellationToken);
        IsCompleted = true;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        await _transaction.RollbackAsync(cancellationToken);
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
public class EfWriteUnitOfWork<TContext> : IWriteUnitOfWork<TContext>
    where TContext : class, IWriteDbContext<object>
{
    private readonly TContext _context;
    private bool _disposed;

    public EfWriteUnitOfWork(TContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public TContext Context => _context;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var dbContext = _context as DbContext ?? throw new InvalidOperationException("Context must inherit from DbContext");
        return await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> SaveChangesAndDispatchEventsAsync(CancellationToken cancellationToken = default)
    {
        var result = await SaveChangesAsync(cancellationToken);
        
        // TODO: Add domain event dispatching logic here
        // This would typically dispatch domain events after successful persistence
        
        return result;
    }

    public async Task<Result<ITransaction>> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var dbContext = _context as DbContext ?? throw new InvalidOperationException("Context must inherit from DbContext");
            if (dbContext.Database.CurrentTransaction == null)
            {
                var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
                return Result<ITransaction>.Success(new EfTransaction(transaction));
            }
            return Result<ITransaction>.Success(new EfTransaction(dbContext.Database.CurrentTransaction));
        }
        catch (Exception ex)
        {
            return Result<ITransaction>.Failure(new BuildingBlocks.Core.Diagnostics.Errors.Error("TRANSACTION_START_FAILED", ex.Message));
        }
    }

    public async Task<Result<ITransaction>> BeginTransactionAsync(IsolationLevel level, CancellationToken cancellationToken = default)
    {
        try
        {
            var dbContext = _context as DbContext ?? throw new InvalidOperationException("Context must inherit from DbContext");
            if (dbContext.Database.CurrentTransaction == null)
            {
                var transaction = await dbContext.Database.BeginTransactionAsync(level, cancellationToken);
                return Result<ITransaction>.Success(new EfTransaction(transaction));
            }
            return Result<ITransaction>.Success(new EfTransaction(dbContext.Database.CurrentTransaction));
        }
        catch (Exception ex)
        {
            return Result<ITransaction>.Failure(new BuildingBlocks.Core.Diagnostics.Errors.Error("TRANSACTION_START_FAILED", ex.Message));
        }
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        var dbContext = _context as DbContext ?? throw new InvalidOperationException("Context must inherit from DbContext");
        var transaction = dbContext.Database.CurrentTransaction;
        if (transaction != null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        var dbContext = _context as DbContext ?? throw new InvalidOperationException("Context must inherit from DbContext");
        var transaction = dbContext.Database.CurrentTransaction;
        if (transaction != null)
        {
            await transaction.RollbackAsync(cancellationToken);
        }
    }

    public bool HasChanges
    {
        get
        {
            var dbContext = _context as DbContext ?? throw new InvalidOperationException("Context must inherit from DbContext");
            return dbContext.ChangeTracker.HasChanges();
        }
    }

    public bool HasActiveTransaction
    {
        get
        {
            var dbContext = _context as DbContext ?? throw new InvalidOperationException("Context must inherit from DbContext");
            return dbContext.Database.CurrentTransaction != null;
        }
    }

    public string? CurrentTransactionId
    {
        get
        {
            var dbContext = _context as DbContext ?? throw new InvalidOperationException("Context must inherit from DbContext");
            return dbContext.Database.CurrentTransaction?.TransactionId.ToString();
        }
    }

    public bool HasDomainEvents => GetDomainEvents().Count > 0;

    public IReadOnlyList<IDomainEvent> GetDomainEvents()
    {
        return _context.GetDomainEvents();
    }

    public void ClearDomainEvents()
    {
        _context.ClearDomainEvents();
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        
        var transactionStarted = false;
        
        try
        {
            if (!HasActiveTransaction)
            {
                var transactionResult = await BeginTransactionAsync(cancellationToken);
                if (transactionResult.IsFailure)
                {
                    throw new InvalidOperationException($"Failed to begin transaction: {transactionResult.Error.Message}");
                }
                transactionStarted = true;
            }
            
            var result = await operation();
            
            if (transactionStarted)
            {
                await CommitTransactionAsync(cancellationToken);
            }
            
            return result;
        }
        catch
        {
            if (transactionStarted && HasActiveTransaction)
            {
                await RollbackTransactionAsync(cancellationToken);
            }
            throw;
        }
    }

    public async Task ExecuteInTransactionAsync(
        Func<Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        
        var transactionStarted = false;
        
        try
        {
            if (!HasActiveTransaction)
            {
                var transactionResult = await BeginTransactionAsync(cancellationToken);
                if (transactionResult.IsFailure)
                {
                    throw new InvalidOperationException($"Failed to begin transaction: {transactionResult.Error.Message}");
                }
                transactionStarted = true;
            }
            
            await operation();
            
            if (transactionStarted)
            {
                await CommitTransactionAsync(cancellationToken);
            }
        }
        catch
        {
            if (transactionStarted && HasActiveTransaction)
            {
                await RollbackTransactionAsync(cancellationToken);
            }
            throw;
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _context?.Dispose();
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}