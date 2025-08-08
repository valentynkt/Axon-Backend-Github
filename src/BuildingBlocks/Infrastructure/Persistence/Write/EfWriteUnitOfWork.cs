using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Core.Event;
using BuildingBlocks.Core.Model;
using BuildingBlocks.Persistence.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Persistence.Write;

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

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var dbContext = _context as DbContext ?? throw new InvalidOperationException("Context must inherit from DbContext");
        if (dbContext.Database.CurrentTransaction == null)
        {
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
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
                await BeginTransactionAsync(cancellationToken);
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
                await BeginTransactionAsync(cancellationToken);
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