using System;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BuildingBlocks.Persistence.Common.Interfaces;
using BuildingBlocks.Web;
using BuildingBlocks.Core.Event;
using BuildingBlocks.Core.Model;
using System.Collections.Immutable;
using BuildingBlocks.Core.Domain.Events;

namespace BuildingBlocks.Persistence.Write;

/// <summary>
/// Base class for write-side database contexts in CQRS architecture
/// Handles aggregates, domain events, audit tracking, and transactions
/// </summary>
public abstract class WriteDbContextBase<TModule> : DbContext, IWriteDbContext<TModule> 
    where TModule : class
{
    private readonly ICurrentUserProvider? _currentUserProvider;
    private readonly ILogger<WriteDbContextBase<TModule>> _logger;
    private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? _currentTransaction;

    protected WriteDbContextBase(
        DbContextOptions options,
        ICurrentUserProvider? currentUserProvider = null,
        ILogger<WriteDbContextBase<TModule>>? logger = null) : base(options)
    {
        _currentUserProvider = currentUserProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<WriteDbContextBase<TModule>>.Instance;
    }

    /// <summary>
    /// Module name for schema separation - must be implemented by derived classes
    /// </summary>
    public abstract string ModuleName { get; }

    /// <summary>
    /// Check if context has active transaction
    /// </summary>
    public virtual bool HasActiveTransaction => _currentTransaction != null;
    /// <summary>
    /// Get current transaction ID for tracking
    /// </summary>
    public virtual string? CurrentTransactionId => _currentTransaction?.TransactionId.ToString();

    /// <summary>
    /// Create execution strategy for resilience
    /// </summary>
    public virtual Microsoft.EntityFrameworkCore.Storage.IExecutionStrategy CreateExecutionStrategy() => 
        Database.CreateExecutionStrategy();

    /// <summary>
    /// Configure model for module-specific schema and conventions
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Set module-specific schema
        modelBuilder.HasDefaultSchema(ModuleName.ToLowerInvariant());
        
        // Apply configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        
        // Configure audit properties for all aggregate roots
        ConfigureAuditProperties(modelBuilder);
        
        // Configure domain event handling
        ConfigureDomainEventProperties(modelBuilder);
        
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Begin a new database transaction
    /// </summary>
    public virtual async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            _logger.LogWarning("Transaction already started for module {Module}. Current transaction ID: {TransactionId}", 
                ModuleName, _currentTransaction.TransactionId);
            return;
        }

        _logger.LogDebug("Beginning transaction for module {Module}", ModuleName);
        _currentTransaction = await Database.BeginTransactionAsync(
            System.Data.IsolationLevel.ReadCommitted, cancellationToken);
    }    /// <summary>
    /// Commit the current transaction
    /// </summary>
    public virtual async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            _logger.LogWarning("No active transaction to commit for module {Module}", ModuleName);
            return;
        }

        try
        {
            _logger.LogDebug("Committing transaction for module {Module}: {TransactionId}", 
                ModuleName, _currentTransaction.TransactionId);
            
            await base.SaveChangesAsync(cancellationToken);
            await _currentTransaction.CommitAsync(cancellationToken);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Failed to commit transaction for module {Module}", ModuleName);
            await RollbackTransactionAsync(cancellationToken);
            throw;
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
    public virtual async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            _logger.LogWarning("No active transaction to rollback for module {Module}", ModuleName);
            return;
        }

        try
        {
            _logger.LogDebug("Rolling back transaction for module {Module}: {TransactionId}", 
                ModuleName, _currentTransaction.TransactionId);
            
            await _currentTransaction.RollbackAsync(cancellationToken);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Failed to rollback transaction for module {Module}", ModuleName);
            throw;
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }    /// <summary>
    /// Execute operation within transaction scope
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
    }

    /// <summary>
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
    }    /// <summary>
    /// Override SaveChangesAsync to handle domain events and audit tracking
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Apply audit information before saving
        ApplyAuditInformation();
        
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
        {
            foreach (var entry in ex.Entries)
            {
                var databaseValues = await entry.GetDatabaseValuesAsync(cancellationToken);
                
                if (databaseValues == null)
                {
                    _logger.LogError("The record no longer exists in the database for module {Module}", ModuleName);
                    throw;
                }

                // Refresh the original values to bypass next concurrency check
                entry.OriginalValues.SetValues(databaseValues);
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Get domain events from all aggregate roots
    /// </summary>
    public virtual IReadOnlyList<IDomainEvent> GetDomainEvents()
    {
        var domainEntities = ChangeTracker
            .Entries<IAggregate>()
            .Where(x => x.Entity.DomainEvents.Any())
            .Select(x => x.Entity)
            .ToList();

        var domainEvents = domainEntities
            .SelectMany(x => x.DomainEvents)
            .ToImmutableList();

        return domainEvents;
    }    /// <summary>
    /// Clear all domain events after processing
    /// </summary>
    public virtual void ClearDomainEvents()
    {
        var domainEntities = ChangeTracker
            .Entries<IAggregate>()
            .Where(x => x.Entity.DomainEvents.Any())
            .Select(x => x.Entity)
            .ToList();

        domainEntities.ForEach(entity => entity.ClearDomainEvents());
    }

    /// <summary>
    /// Apply audit information to tracked entities
    /// </summary>
    protected virtual void ApplyAuditInformation()
    {
        var currentUser = _currentUserProvider?.GetCurrentUserId() ?? 0;
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IAggregate>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedBy = currentUser;
                    entry.Entity.CreatedAt = now;
                    entry.Entity.LastModifiedBy = currentUser;
                    entry.Entity.LastModified = now;
                    break;

                case EntityState.Modified:
                    entry.Entity.LastModifiedBy = currentUser;
                    entry.Entity.LastModified = now;
                    entry.Entity.Version++;
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.LastModifiedBy = currentUser;
                    entry.Entity.LastModified = now;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.Version++;
                    break;
            }
        }
    }    
    
    // ToDo: Consider refactoring to use the clean AuditingIntercepto approach.
    /// <summary>
    /// Configure audit properties for all aggregate roots
    /// </summary>
    protected virtual void ConfigureAuditProperties(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IAggregate).IsAssignableFrom(entityType.ClrType))
            {
                var builder = modelBuilder.Entity(entityType.ClrType);
                
                // Configure audit properties
                builder.Property<long>("CreatedBy").IsRequired();
                builder.Property<DateTime>("CreatedAt").IsRequired();
                builder.Property<long>("LastModifiedBy").IsRequired();
                builder.Property<DateTime>("LastModified").IsRequired();
                builder.Property<long>("Version").IsRequired();
                builder.Property<bool>("IsDeleted").IsRequired().HasDefaultValue(false);
                
                // Add global query filter for soft deletes
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var property = Expression.Property(parameter, "IsDeleted");
                var filter = Expression.Lambda(Expression.Not(property), parameter);
                builder.HasQueryFilter(filter);
            }
        }
    }

    /// <summary>
    /// Configure domain event properties (placeholder for future event sourcing)
    /// </summary>
    protected virtual void ConfigureDomainEventProperties(ModelBuilder modelBuilder)
    {
        // Domain events are handled in-memory and not persisted directly
        // This method is reserved for future event sourcing implementation
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