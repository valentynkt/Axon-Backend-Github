using System.Collections.Concurrent;
using Ardalis.GuardClauses;
using BuildingBlocks.Core.Event;
using BuildingBlocks.Core.Model;
using BuildingBlocks.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Postgres;

/// <summary>
/// PostgreSQL DbContext implementation with command queuing
/// Maintains same patterns as MongoDB implementation but for PostgreSQL
/// </summary>
public abstract class PostgresDbContext : DbContext, IPostgresDbContext
{
    private readonly ConcurrentQueue<Func<Task>> _commands = new();
    private IDbContextTransaction? _currentTransaction;
    private readonly ICurrentUserProvider? _currentUserProvider;
    private readonly ILogger<PostgresDbContext> _logger;

    protected PostgresDbContext(
        DbContextOptions options,
        ICurrentUserProvider? currentUserProvider = null,
        ILogger<PostgresDbContext>? logger = null)
        : base(options)
    {
        _currentUserProvider = currentUserProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PostgresDbContext>.Instance;
    }

    /// <summary>
    /// Get DbSet for entity type (PostgreSQL equivalent of GetCollection)
    /// </summary>
    public virtual DbSet<T> GetCollection<T>() where T : class
    {
        return Set<T>();
    }

    /// <summary>
    /// Get DbSet for entity type with optional table name
    /// </summary>
    public virtual DbSet<T> GetCollection<T>(string? name = null) where T : class
    {
        // In PostgreSQL/EF Core, table names are configured in OnModelCreating
        // This method maintains interface compatibility with MongoDB
        return Set<T>();
    }

    /// <summary>
    /// Get domain events from all aggregate roots
    /// </summary>
    public virtual IReadOnlyList<IDomainEvent> GetDomainEvents()
    {
        var domainEvents = new List<IDomainEvent>();

        var aggregateRoots = ChangeTracker.Entries<IAggregate>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        foreach (var aggregateRoot in aggregateRoots)
        {
            domainEvents.AddRange(aggregateRoot.DomainEvents);
            aggregateRoot.ClearDomainEvents();
        }

        return domainEvents.AsReadOnly();
    }

    /// <summary>
    /// Save all changes including queued commands
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var result = 0;

        // Apply audit information before saving
        ApplyAuditInformation();

        if (_commands.IsEmpty)
        {
            return await base.SaveChangesAsync(cancellationToken);
        }

        // Execute commands within transaction if not already in one
        if (_currentTransaction == null)
        {
            using var transaction = await Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await ExecuteQueuedCommandsAsync(cancellationToken);
                result = await base.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        else
        {
            await ExecuteQueuedCommandsAsync(cancellationToken);
            result = await base.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// Begin a new database transaction
    /// </summary>
    public virtual async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            _logger.LogWarning("Transaction already started. Current transaction ID: {TransactionId}", 
                _currentTransaction.TransactionId);
            return;
        }

        _logger.LogDebug("Beginning PostgreSQL transaction");
        _currentTransaction = await Database.BeginTransactionAsync(cancellationToken);
    }

    /// <summary>
    /// Commit the current transaction
    /// </summary>
    public virtual async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            _logger.LogWarning("No active transaction to commit");
            return;
        }

        try
        {
            _logger.LogDebug("Committing PostgreSQL transaction: {TransactionId}", 
                _currentTransaction.TransactionId);
            
            await base.SaveChangesAsync(cancellationToken);
            await _currentTransaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    /// <summary>
    /// Rollback the current transaction
    /// </summary>
    public virtual async Task RollbackTransaction(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            _logger.LogWarning("No active transaction to rollback");
            return;
        }

        try
        {
            _logger.LogDebug("Rolling back PostgreSQL transaction: {TransactionId}", 
                _currentTransaction.TransactionId);
            
            await _currentTransaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    /// <summary>
    /// Add command to execution queue for deferred execution
    /// </summary>
    public virtual void AddCommand(Func<Task> func)
    {
        Guard.Against.Null(func, nameof(func));
        _commands.Enqueue(func);
        _logger.LogDebug("Command added to queue. Total queued: {Count}", _commands.Count);
    }

    /// <summary>
    /// Execute all queued commands
    /// </summary>
    public virtual async Task ExecuteQueuedCommandsAsync(CancellationToken cancellationToken = default)
    {
        var commandCount = _commands.Count;
        if (commandCount == 0) return;

        _logger.LogDebug("Executing {Count} queued commands", commandCount);

        while (_commands.TryDequeue(out var command))
        {
            await command();
        }

        _logger.LogDebug("Completed executing {Count} queued commands", commandCount);
    }

    /// <summary>
    /// Check if context has active transaction
    /// </summary>
    public virtual bool HasActiveTransaction => _currentTransaction != null;

    /// <summary>
    /// Get current transaction ID for tracking
    /// </summary>
    public virtual string? CurrentTransactionId => _currentTransaction?.TransactionId.ToString();

    /// <summary>
    /// Execute operation within transaction scope
    /// </summary>
    public virtual async Task ExecuteTransactionalAsync(
        Func<Task> action,
        CancellationToken cancellationToken = default)
    {
        var wasTransactionStarted = HasActiveTransaction;
        
        if (!wasTransactionStarted)
        {
            await BeginTransactionAsync(cancellationToken);
        }

        try
        {
            await action();
            
            if (!wasTransactionStarted)
            {
                await CommitTransactionAsync(cancellationToken);
            }
        }
        catch
        {
            if (!wasTransactionStarted)
            {
                await RollbackTransaction(cancellationToken);
            }
            throw;
        }
    }

    /// <summary>
    /// Execute function within transaction scope and return result
    /// </summary>
    public virtual async Task<T> ExecuteTransactionalAsync<T>(
        Func<Task<T>> func,
        CancellationToken cancellationToken = default)
    {
        var wasTransactionStarted = HasActiveTransaction;
        
        if (!wasTransactionStarted)
        {
            await BeginTransactionAsync(cancellationToken);
        }

        try
        {
            var result = await func();
            
            if (!wasTransactionStarted)
            {
                await CommitTransactionAsync(cancellationToken);
            }
            
            return result;
        }
        catch
        {
            if (!wasTransactionStarted)
            {
                await RollbackTransaction(cancellationToken);
            }
            throw;
        }
    }

    /// <summary>
    /// Apply audit information to tracked entities
    /// </summary>
    protected virtual void ApplyAuditInformation()
    {
        var currentUser = _currentUserProvider?.GetCurrentUserId()?.ToString() ?? "system";
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = currentUser;
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = currentUser;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = currentUser;
                    break;
            }
        }
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public override void Dispose()
    {
        _currentTransaction?.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Dispose resources asynchronously
    /// </summary>
    public override async ValueTask DisposeAsync()
    {
        if (_currentTransaction != null)
        {
            await _currentTransaction.DisposeAsync();
        }
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Interface for auditable entities
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    string CreatedBy { get; set; }
    DateTime UpdatedAt { get; set; }
    string UpdatedBy { get; set; }
}