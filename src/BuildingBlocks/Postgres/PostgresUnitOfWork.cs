using BuildingBlocks.Core.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Postgres;

/// <summary>
/// PostgreSQL Unit of Work implementation
/// Maintains same patterns as MongoDB UnitOfWork but for PostgreSQL
/// </summary>
public class PostgresUnitOfWork : IUnitOfWork
{
    protected readonly IPostgresDbContext Context;
    protected readonly ILogger<PostgresUnitOfWork> _logger;
    private bool _disposed;

    public PostgresUnitOfWork(
        IPostgresDbContext context,
        ILogger<PostgresUnitOfWork>? logger = null)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PostgresUnitOfWork>.Instance;
    }

    /// <summary>
    /// Check if there are pending changes
    /// </summary>
    public virtual bool HasChanges
    {
        get
        {
            if (Context is DbContext dbContext)
            {
                return dbContext.ChangeTracker.HasChanges();
            }
            return false;
        }
    }

    /// <summary>
    /// Save all changes to database and return number of affected records
    /// </summary>
    public virtual async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Saving changes to PostgreSQL database");
        
        try
        {
            var result = await Context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Successfully saved {Count} changes", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save changes to PostgreSQL database");
            throw;
        }
    }

    /// <summary>
    /// Begin a new database transaction
    /// </summary>
    public virtual async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Beginning PostgreSQL transaction");
        await Context.BeginTransactionAsync(cancellationToken);
    }

    /// <summary>
    /// Commit the current transaction
    /// </summary>
    public virtual async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Committing PostgreSQL transaction");
        
        try
        {
            await Context.CommitTransactionAsync(cancellationToken);
            _logger.LogDebug("PostgreSQL transaction committed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to commit PostgreSQL transaction");
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Rollback the current transaction
    /// </summary>
    public virtual async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Rolling back PostgreSQL transaction");
        
        try
        {
            await Context.RollbackTransaction(cancellationToken);
            _logger.LogDebug("PostgreSQL transaction rolled back successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rollback PostgreSQL transaction");
            throw;
        }
    }

    /// <summary>
    /// Execute operation within transaction scope
    /// </summary>
    public virtual async Task<T> ExecuteInTransactionAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        
        var wasTransactionActive = Context.HasActiveTransaction;
        
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
    /// Execute operation within transaction scope
    /// </summary>
    public virtual async Task ExecuteInTransactionAsync(
        Func<Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        
        var wasTransactionActive = Context.HasActiveTransaction;
        
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

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            Context?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Generic PostgreSQL Unit of Work implementation with typed context
/// </summary>
public class PostgresUnitOfWork<TContext> : PostgresUnitOfWork, IUnitOfWork<TContext>
    where TContext : class, IPostgresDbContext
{
    private readonly IServiceProvider _serviceProvider;

    public TContext Context { get; }

    public PostgresUnitOfWork(
        TContext context,
        IServiceProvider serviceProvider,
        ILogger<PostgresUnitOfWork<TContext>>? logger = null)
        : base(context, logger)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }
}

/// <summary>
/// PostgreSQL-specific Unit of Work implementation with repository management
/// </summary>
public class PostgresRepositoryUnitOfWork<TContext> : PostgresUnitOfWork<TContext>, IPostgresUnitOfWork<TContext>
    where TContext : class, IPostgresDbContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<Type, object> _repositories = new();

    public PostgresRepositoryUnitOfWork(
        TContext context,
        IServiceProvider serviceProvider,
        ILogger<PostgresRepositoryUnitOfWork<TContext>>? logger = null)
        : base(context, serviceProvider, logger)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Get repository for entity type
    /// </summary>
    public virtual IRepository<T, TId> GetRepository<T, TId>() where T : class
    {
        var type = typeof(IRepository<T, TId>);
        
        if (_repositories.TryGetValue(type, out var repository))
        {
            return (IRepository<T, TId>)repository;
        }

        var newRepository = _serviceProvider.GetRequiredService<IRepository<T, TId>>();
        _repositories[type] = newRepository;
        
        return newRepository;
    }

    /// <summary>
    /// Get repository for entity type with Guid key
    /// </summary>
    public virtual IRepository<T> GetRepository<T>() where T : class, IEntity<Guid>
    {
        return GetRepository<T, Guid>();
    }

    /// <summary>
    /// Get aggregate repository for domain aggregates with event support
    /// </summary>
    public virtual IAggregateRepository<T, TId> GetAggregateRepository<T, TId>() where T : class, IAggregate<TId>
    {
        var type = typeof(IAggregateRepository<T, TId>);
        
        if (_repositories.TryGetValue(type, out var repository))
        {
            return (IAggregateRepository<T, TId>)repository;
        }

        var newRepository = _serviceProvider.GetRequiredService<IAggregateRepository<T, TId>>();
        _repositories[type] = newRepository;
        
        return newRepository;
    }

    /// <summary>
    /// Get read-only repository for entity type
    /// </summary>
    public virtual IReadRepository<T, TId> GetReadRepository<T, TId>() where T : class
    {
        var type = typeof(IReadRepository<T, TId>);
        
        if (_repositories.TryGetValue(type, out var repository))
        {
            return (IReadRepository<T, TId>)repository;
        }

        var newRepository = _serviceProvider.GetRequiredService<IReadRepository<T, TId>>();
        _repositories[type] = newRepository;
        
        return newRepository;
    }

    /// <summary>
    /// Get write-only repository for entity type
    /// </summary>
    public virtual IWriteRepository<T, TId> GetWriteRepository<T, TId>() where T : class
    {
        var type = typeof(IWriteRepository<T, TId>);
        
        if (_repositories.TryGetValue(type, out var repository))
        {
            return (IWriteRepository<T, TId>)repository;
        }

        var newRepository = _serviceProvider.GetRequiredService<IWriteRepository<T, TId>>();
        _repositories[type] = newRepository;
        
        return newRepository;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var repository in _repositories.Values)
            {
                if (repository is IDisposable disposableRepository)
                {
                    disposableRepository.Dispose();
                }
            }
            _repositories.Clear();
        }
        
        base.Dispose(disposing);
    }
}