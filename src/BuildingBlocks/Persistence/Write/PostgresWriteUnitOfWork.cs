using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Persistence.Common.Interfaces;
using BuildingBlocks.Core.Event;

namespace BuildingBlocks.Persistence.Write;

/// <summary>
/// PostgreSQL implementation of write unit of work for CQRS command operations
/// Handles transactional consistency and domain event processing
/// </summary>
public class PostgresWriteUnitOfWork<TWriteContext> : IWriteUnitOfWork<TWriteContext>
    where TWriteContext : class, IWriteDbContext<object>
{
    private readonly TWriteContext _context;
    private readonly ILogger<PostgresWriteUnitOfWork<TWriteContext>> _logger;
    private bool _disposed;

    public PostgresWriteUnitOfWork(
        TWriteContext context,
        ILogger<PostgresWriteUnitOfWork<TWriteContext>>? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PostgresWriteUnitOfWork<TWriteContext>>.Instance;
    }

    public TWriteContext Context => _context;

    public virtual bool HasChanges
    {
        get
        {
            if (_context is DbContext dbContext)
            {
                return dbContext.ChangeTracker.HasChanges();
            }
            return false;
        }
    }

    public virtual bool HasActiveTransaction => _context.HasActiveTransaction;
    
    public virtual async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Saving changes for write context {ContextType}", typeof(TWriteContext).Name);
        
        try
        {
            var result = await _context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Successfully saved {Count} changes", result);
            return result;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency conflict occurred while saving changes");
            throw;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Failed to save changes to database");
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation while saving changes");
            throw;
        }
    }

    public virtual async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Beginning transaction for write context {ContextType}", typeof(TWriteContext).Name);
        await _context.BeginTransactionAsync(cancellationToken);
    }

    public virtual async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Committing transaction for write context {ContextType}", typeof(TWriteContext).Name);
        
        try
        {
            await _context.CommitTransactionAsync(cancellationToken);
            _logger.LogDebug("Transaction committed successfully");
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Failed to commit transaction");
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public virtual async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Rolling back transaction for write context {ContextType}", typeof(TWriteContext).Name);
        
        try
        {
            await _context.RollbackTransactionAsync(cancellationToken);
            _logger.LogDebug("Transaction rolled back successfully");
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Failed to rollback transaction");
            throw;
        }
    }    
    
    public virtual IReadOnlyList<IDomainEvent> GetDomainEvents()
    {
        _logger.LogDebug("Getting domain events from write context {ContextType}", typeof(TWriteContext).Name);
        return _context.GetDomainEvents();
    }

    public virtual void ClearDomainEvents()
    {
        _logger.LogDebug("Clearing domain events from write context {ContextType}", typeof(TWriteContext).Name);
        _context.ClearDomainEvents();
    }

    public virtual async Task<T> ExecuteInTransactionAsync<T>(
        Func<Task<T>> operation, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var wasTransactionActive = HasActiveTransaction;
        
        if (!wasTransactionActive)
        {
            await BeginTransactionAsync(cancellationToken);
        }

        try
        {
            var result = await operation();
            
            if (!wasTransactionActive)
            {
                await CommitTransactionAsync(cancellationToken);
            }
            
            return result;
        }
        catch
        {
            if (!wasTransactionActive)
            {
                await RollbackTransactionAsync(cancellationToken);
            }
            throw;
        }
    }

    public virtual async Task ExecuteInTransactionAsync(
        Func<Task> operation, 
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var wasTransactionActive = HasActiveTransaction;
        
        if (!wasTransactionActive)
        {
            await BeginTransactionAsync(cancellationToken);
        }

        try
        {
            await operation();
            
            if (!wasTransactionActive)
            {
                await CommitTransactionAsync(cancellationToken);
            }
        }
        catch
        {
            if (!wasTransactionActive)
            {
                await RollbackTransactionAsync(cancellationToken);
            }
            throw;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _context?.Dispose();
            _disposed = true;
        }
    }
}