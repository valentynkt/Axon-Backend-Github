using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Persistence;

/// <summary>
/// Unified base class for module DbContexts that handle both read and write operations.
/// Merges capabilities from WriteDbContextBase and ReadDbContextBase into a single,
/// cohesive abstraction that reflects the unified context pattern adopted in the codebase.
///
/// Features:
/// - Transaction management with execution strategies
/// - Audit trail support (CreatedAt/UpdatedAt)
/// - Optimistic concurrency with PostgreSQL xmin
/// - Soft delete query filters
/// - Read optimization helpers (Query, ExecuteCompiledQueryAsync)
/// - Configurable command timeout (default 30s)
/// </summary>
public abstract class DbContextBase<TModule> : DbContext, IDbContext
    where TModule : class
{
    private readonly ILogger<DbContextBase<TModule>> _logger;
    private IDbContextTransaction? _currentTransaction;

    protected DbContextBase(
        DbContextOptions options,
        ILogger<DbContextBase<TModule>>? logger = null) : base(options)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DbContextBase<TModule>>.Instance;

        // Keep change tracking enabled for write operations
        // Use Query<T>() for no-tracking read queries
        ChangeTracker.LazyLoadingEnabled = false;

        // Configure command timeout (default 30s, can be overridden)
        Database.SetCommandTimeout(CommandTimeout);
    }

    public abstract string ModuleName { get; }

    /// <summary>
    /// Command timeout in seconds. Default is 30 seconds.
    /// Override in derived class to customize.
    /// </summary>
    protected virtual TimeSpan CommandTimeout => TimeSpan.FromSeconds(30);

    public bool HasActiveTransaction => _currentTransaction != null;
    public string? CurrentTransactionId => _currentTransaction?.TransactionId.ToString();

    public IExecutionStrategy CreateExecutionStrategy() => Database.CreateExecutionStrategy();

    // --------- Read Optimization Helpers ---------

    /// <summary>
    /// No-tracking queryable for read-only operations.
    /// Use this for queries that don't need change tracking for better performance.
    /// </summary>
    public IQueryable<TEntity> Query<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields | DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties | DynamicallyAccessedMemberTypes.Interfaces)] TEntity>() where TEntity : class
        => Set<TEntity>().AsNoTracking();

    /// <summary>
    /// Execute a compiled query for performance-sensitive read paths.
    /// Compiled queries are cached and reused, providing significant performance benefits.
    /// </summary>
    public Task<TResult> ExecuteCompiledQueryAsync<TResult>(
        Func<IDbContext, Task<TResult>> compiledQuery,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compiledQuery);
        _ = cancellationToken; // Parameter required for interface compatibility
        return compiledQuery(this);
    }

    // --------- Model Configuration ---------

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(ModuleName.ToLowerInvariant());

        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        ApplySoftDeleteQueryFilter(modelBuilder);

        // Apply read optimization indexes (module-specific override available)
        ConfigureReadOptimizations(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Configure read-optimized indexes for query performance.
    /// Override in derived classes to add module-specific indexes.
    /// Base implementation is intentionally empty - modules define their own indexes.
    /// </summary>
    protected virtual void ConfigureReadOptimizations(ModelBuilder modelBuilder)
    {
        // Empty by design - modules override to add their specific read optimization indexes
        // This prevents conflicts with module-specific index configurations
    }

    // --------- Transactions ---------

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null) return;
        // Use ReadCommitted for better performance with optimistic concurrency
        // Optimistic concurrency relies on version checks, not isolation levels
        _currentTransaction = await Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null) return;
        try
        {
            await base.SaveChangesAsync(cancellationToken);
            await _currentTransaction.CommitAsync(cancellationToken);
        }
        catch
        {
            // Use CancellationToken.None for rollback to ensure it completes even if commit was cancelled
            await RollbackTransactionAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null) return;
        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }
    }

    public async Task ExecuteTransactionalAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        var strategy = CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
            try
            {
                await operation();
                await base.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<T> ExecuteTransactionalAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        var strategy = CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
            try
            {
                var result = await operation();
                await base.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    // --------- SaveChanges with Audit & Concurrency ---------

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = TimeProvider.System.GetUtcNow();

        // Apply audit information to all changed entities
        foreach (var entry in ChangeTracker.Entries())
        {
            switch (entry.State)
            {
                case EntityState.Added when entry.Entity is BuildingBlocks.Core.Domain.Entities.Base.AuditableDeletableEntity<Guid> addedAuditable:
                    addedAuditable.SetCreatedAtInternal(now);
                    addedAuditable.SetUpdatedAtInternal(now);
                    break;

                case EntityState.Modified when entry.Entity is BuildingBlocks.Core.Domain.Entities.Base.AuditableDeletableEntity<Guid> modifiedAuditable:
                    modifiedAuditable.SetUpdatedAtInternal(now);
                    break;

                // Handle owned entities (e.g., WalletOwnership, PrincipalChainDefault)
                case EntityState.Added when entry.Entity is BuildingBlocks.Core.Domain.Entities.Base.OwnedAuditableEntity addedOwned:
                    addedOwned.SetCreatedAtInternal(now);
                    addedOwned.SetUpdatedAtInternal(now);
                    break;

                case EntityState.Modified when entry.Entity is BuildingBlocks.Core.Domain.Entities.Base.OwnedAuditableEntity modifiedOwned:
                    modifiedOwned.SetUpdatedAtInternal(now);
                    break;
            }
        }

        ApplyAuditInformation();

        try
        {
            // Let EF Core handle concurrency with PostgreSQL's xmin column
            // The .IsRowVersion() configuration in entity configurations handles everything
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Log the concurrency conflict
            var affectedEntityTypes = ex.Entries
                .Select(e => e.Entity.GetType().Name)
                .Distinct()
                .ToList();

            _logger.LogError(ex,
                "Concurrency conflict detected. Affected types: {EntityTypes}. Module: {ModuleName}",
                string.Join(", ", affectedEntityTypes), ModuleName);

            // Translate to our custom ConcurrencyException for consistent error handling
            ThrowConcurrencyException(ex);
            throw; // Never reached, but required for compiler
        }
    }

    private static void ThrowConcurrencyException(DbUpdateConcurrencyException ex)
    {
        var firstEntry = ex.Entries.Count > 0 ? ex.Entries[0] : null;
        if (firstEntry == null)
        {
            throw ex; // Re-throw original if no entries
        }

        var entityType = firstEntry.Entity.GetType().Name;

        // Use EF Core's metadata to get primary key values
        var keyValues = firstEntry.Metadata.FindPrimaryKey()?.Properties
            .Select(p => firstEntry.CurrentValues[p]?.ToString() ?? "null")
            .ToArray() ?? ["unknown"];
        var entityId = string.Join(", ", keyValues);

        // With PostgreSQL xmin, version details are managed by the database
        // so we don't have access to specific version numbers
        throw new Core.Diagnostics.Exceptions.ConcurrencyException(
            $"The {entityType} with key [{entityId}] has been modified by another user. Please refresh and try again.",
            entityType,
            entityId,
            "xmin", // Using PostgreSQL xmin for concurrency
            "xmin");
    }


    // Domain events are raised by aggregates and used for testing purposes only
    // No event dispatching/publishing infrastructure in MVP

    // --------- Hooks & Conventions ---------

    /// <summary>
    /// Virtual hook for module-specific audit logic.
    /// Called during SaveChangesAsync before persisting changes.
    /// Override to add custom audit behavior (e.g., using TimeProvider).
    /// </summary>
    protected virtual void ApplyAuditInformation() { }

    private static void ApplySoftDeleteQueryFilter(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Skip owned entity types - they cannot have query filters
            if (entityType.IsOwned())
                continue;

            if (typeof(Core.Domain.Entities.Abstractions.ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                var p = Expression.Parameter(entityType.ClrType, "e");
                var prop = Expression.Property(p, nameof(Core.Domain.Entities.Abstractions.ISoftDeletable.IsDeleted));
                var filter = Expression.Lambda(Expression.Not(prop), p);
                entityType.SetQueryFilter(filter);
            }
        }
    }

    public override void Dispose()
    {
        _currentTransaction?.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_currentTransaction != null)
            await _currentTransaction.DisposeAsync();

        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
