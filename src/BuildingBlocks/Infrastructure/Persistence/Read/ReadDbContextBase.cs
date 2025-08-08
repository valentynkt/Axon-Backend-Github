using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Persistence.Read;

/// <summary>
/// Base class for read-side database contexts in CQRS architecture
/// Optimized for query performance with no tracking and compiled queries
/// </summary>
public abstract class ReadDbContextBase<TModule> : DbContext, IReadDbContext<TModule> 
    where TModule : class
{
    private readonly ILogger<ReadDbContextBase<TModule>> _logger;
    private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? _currentTransaction;

    protected ReadDbContextBase(
        DbContextOptions options,
        ILogger<ReadDbContextBase<TModule>>? logger = null) : base(options)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ReadDbContextBase<TModule>>.Instance;
        
        // Optimize for read operations
        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        ChangeTracker.LazyLoadingEnabled = false;
        ChangeTracker.AutoDetectChangesEnabled = false;
    }

    /// <summary>
    /// Module name for schema separation - must be implemented by derived classes
    /// </summary>
    public abstract string ModuleName { get; }

    /// <summary>
    /// Check if context has active transaction
    /// </summary>
    public virtual bool HasActiveTransaction => _currentTransaction != null;    /// <summary>
    /// Get current transaction ID for tracking
    /// </summary>
    public virtual string? CurrentTransactionId => _currentTransaction?.TransactionId.ToString();

    public virtual IReadOnlyList<IDomainEvent> GetDomainEvents()
    {
        // Read contexts typically don't handle domain events
        // Return empty list for interface compliance
        return new List<IDomainEvent>();
    }

    /// <summary>
    /// Create execution strategy for resilience
    /// </summary>
    public virtual Microsoft.EntityFrameworkCore.Storage.IExecutionStrategy CreateExecutionStrategy() => 
        Database.CreateExecutionStrategy();

    /// <summary>
    /// Get queryable for read model with no tracking for optimal performance
    /// </summary>
    public virtual IQueryable<TReadModel> Query<TReadModel>() where TReadModel : class
    {
        return Set<TReadModel>().AsNoTracking();
    }

    /// <summary>
    /// Execute compiled query for maximum performance
    /// </summary>
    public virtual async Task<TResult> ExecuteCompiledQueryAsync<TResult>(
        Func<IReadDbContext<TModule>, Task<TResult>> compiledQuery,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await compiledQuery(this);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Failed to execute compiled query for module {Module}", ModuleName);
            throw;
        }
    }

    /// <summary>
    /// Configure model for module-specific schema and read optimizations
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Set module-specific schema
        modelBuilder.HasDefaultSchema(ModuleName.ToLowerInvariant());
        
        // Apply configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        
        // Configure read model optimizations
        ConfigureReadModelOptimizations(modelBuilder);
        
        base.OnModelCreating(modelBuilder);
    }    /// <summary>
    /// Begin a new database transaction (rarely used in read contexts)
    /// </summary>
    public virtual async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            _logger.LogWarning("Transaction already started for read module {Module}. Current transaction ID: {TransactionId}", 
                ModuleName, _currentTransaction.TransactionId);
            return;
        }

        _logger.LogDebug("Beginning read transaction for module {Module}", ModuleName);
        _currentTransaction = await Database.BeginTransactionAsync(
            System.Data.IsolationLevel.ReadCommitted, cancellationToken);
    }

    /// <summary>
    /// Commit the current transaction
    /// </summary>
    public virtual async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            _logger.LogWarning("No active transaction to commit for read module {Module}", ModuleName);
            return;
        }

        try
        {
            _logger.LogDebug("Committing read transaction for module {Module}: {TransactionId}", 
                ModuleName, _currentTransaction.TransactionId);
            
            await base.SaveChangesAsync(cancellationToken);
            await _currentTransaction.CommitAsync(cancellationToken);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Failed to commit read transaction for module {Module}", ModuleName);
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }    /// <summary>
    /// Rollback the current transaction
    /// </summary>
    public virtual async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            _logger.LogWarning("No active transaction to rollback for read module {Module}", ModuleName);
            return;
        }

        try
        {
            _logger.LogDebug("Rolling back read transaction for module {Module}: {TransactionId}", 
                ModuleName, _currentTransaction.TransactionId);
            
            await _currentTransaction.RollbackAsync(cancellationToken);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Failed to rollback read transaction for module {Module}", ModuleName);
            throw;
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    /// <summary>
    /// Execute operation within transaction scope (rarely used for read operations)
    /// </summary>
    public virtual async Task ExecuteTransactionalAsync(CancellationToken cancellationToken = default)
    {
        var strategy = CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await Database.BeginTransactionAsync(
                System.Data.IsolationLevel.ReadCommitted, cancellationToken);
            try
            {
                await SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }    /// <summary>
    /// Execute operation within transaction scope and return result
    /// </summary>
    public virtual async Task<T> ExecuteTransactionalAsync<T>(
        Func<Task<T>> operation, 
        CancellationToken cancellationToken = default)
    {
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

    /// <summary>
    /// Override SaveChangesAsync (read contexts typically don't save changes)
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Read contexts typically don't modify data
        // This is here for interface compliance and rare cases
        _logger.LogWarning("SaveChangesAsync called on read context for module {Module}. " +
                          "Consider if this operation should be in a write context instead.", ModuleName);
        
        return await base.SaveChangesAsync(cancellationToken);
    }    /// <summary>
    /// Configure read model optimizations for query performance
    /// </summary>
    protected virtual void ConfigureReadModelOptimizations(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var builder = modelBuilder.Entity(entityType.ClrType);
            
            // Configure indexes for common query patterns
            // This should be overridden in derived classes for specific optimizations
            
            // Add created/modified date indexes for temporal queries
            if (entityType.FindProperty("CreatedAt") != null)
            {
                builder.HasIndex("CreatedAt")
                    .HasDatabaseName($"ix_{GetTableName(entityType.ClrType)}_created_at");
            }
            
            if (entityType.FindProperty("UpdatedAt") != null)
            {
                builder.HasIndex("UpdatedAt")
                    .HasDatabaseName($"ix_{GetTableName(entityType.ClrType)}_updated_at");
            }
        }
    }

    /// <summary>
    /// Get table name from entity type for index naming
    /// </summary>
    protected virtual string GetTableName(Type entityType)
    {
        return entityType.Name.ToLowerInvariant();
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