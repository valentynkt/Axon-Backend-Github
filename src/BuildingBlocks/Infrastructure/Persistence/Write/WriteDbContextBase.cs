using System.Linq.Expressions;
using System.Reflection;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Persistence.Write;

/// <summary>
/// Base class for write-side DbContexts.
///
/// MIGRATION NOTE: Consider using DbContextBase{TModule} instead, which provides
/// both write capabilities AND read optimizations (Query{T}(), ExecuteCompiledQueryAsync, etc.).
/// WriteDbContextBase is kept for backwards compatibility, but DbContextBase is the
/// recommended choice for new module contexts.
///
/// Current usage: Legacy support only. All active module contexts (IdentityDbContext, ChatDbContext)
/// now inherit from DbContextBase.
/// </summary>
public abstract class WriteDbContextBase<TModule> : DbContext, IWriteDbContext<TModule>
    where TModule : class
{
    private readonly ILogger<WriteDbContextBase<TModule>> _logger;
    private IDbContextTransaction? _currentTransaction;

    protected WriteDbContextBase(
        DbContextOptions options,
        ILogger<WriteDbContextBase<TModule>>? logger = null) : base(options)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<WriteDbContextBase<TModule>>.Instance;
        ChangeTracker.LazyLoadingEnabled = false;
    }

    public abstract string ModuleName { get; }

    public bool HasActiveTransaction => _currentTransaction != null;
    public string? CurrentTransactionId => _currentTransaction?.TransactionId.ToString();

    public IExecutionStrategy CreateExecutionStrategy() => Database.CreateExecutionStrategy();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(ModuleName.ToLowerInvariant());

        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        ApplySoftDeleteQueryFilter(modelBuilder);
        // Removed ApplyVersionConcurrencyToken - redundant as entity configurations already use .IsRowVersion()
        base.OnModelCreating(modelBuilder);
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

    // --------- SaveChanges ---------

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

    // --------- Hooks & Conventions (kept minimal) ---------
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

    // Removed ApplyVersionConcurrencyToken method - redundant as entity configurations
    // already configure concurrency using .IsRowVersion() which properly maps to PostgreSQL xmin

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
