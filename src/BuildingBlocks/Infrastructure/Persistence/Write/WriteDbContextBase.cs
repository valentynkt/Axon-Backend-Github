using System.Linq.Expressions;
using System.Reflection;
using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Persistence.Write;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder
    )
    {
        modelBuilder.HasDefaultSchema(ModuleName.ToLowerInvariant());

        // Configure MassTransit outbox entities for the module schema
        var schema = ModuleName.ToLowerInvariant();
        modelBuilder.AddInboxStateEntity(x => x.Metadata.SetSchema(schema));
        modelBuilder.AddOutboxMessageEntity(x => x.Metadata.SetSchema(schema));
        modelBuilder.AddOutboxStateEntity(x => x.Metadata.SetSchema(schema));

        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        ApplySoftDeleteQueryFilter(modelBuilder);
        ApplyVersionConcurrencyToken(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    // --------- Transactions ---------

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null) return;
        // Use RepeatableRead to prevent phantom reads during concurrent operations
        _currentTransaction = await Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);
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
            await using var tx = await Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);
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
            await using var tx = await Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);
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
        ApplyAuditInformation();

        const int maxRetries = 3;
        var retryCount = 0;

        while (retryCount < maxRetries)
        {
            try
            {
                var affected = await base.SaveChangesAsync(cancellationToken);
                if (retryCount > 0)
                {
                    _logger.LogInformation("SaveChanges succeeded after {RetryCount} retries", retryCount);
                }
                return affected;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                retryCount++;
                var affectedEntityTypes = ex.Entries.Select(e => e.Entity.GetType().Name).Distinct().ToList();

                _logger.LogWarning("DbUpdateConcurrencyException on attempt {AttemptNumber}/{MaxRetries}. " +
                    "Affected entities: {EntityCount}. Types: {EntityTypes}",
                    retryCount, maxRetries, ex.Entries.Count, string.Join(", ", affectedEntityTypes));

                if (retryCount >= maxRetries)
                {
                    _logger.LogError("Maximum retries ({MaxRetries}) exceeded for SaveChanges. " +
                        "Final affected entity types: {EntityTypes}. Module: {ModuleName}",
                        maxRetries, string.Join(", ", affectedEntityTypes), ModuleName);
                    throw;
                }

                // Simple reload strategy: refresh conflicted entities with database values
                foreach (var entry in ex.Entries)
                {
                    if (entry.State == EntityState.Added) continue; // New entities don't exist in DB

                    var databaseValues = await entry.GetDatabaseValuesAsync(cancellationToken);
                    if (databaseValues != null)
                    {
                        entry.OriginalValues.SetValues(databaseValues);
                        _logger.LogDebug("Refreshed {EntityType} with database values", entry.Entity.GetType().Name);
                    }
                    else
                    {
                        // Entity was deleted - detach it
                        entry.State = EntityState.Detached;
                        _logger.LogWarning("{EntityType} was deleted by another process - detached", entry.Entity.GetType().Name);
                    }
                }

                // Brief delay before retry to reduce collision probability
                var delay = TimeSpan.FromMilliseconds(50 * retryCount);
                _logger.LogDebug("Waiting {DelayMs}ms before retry {RetryNumber}/{MaxRetries}",
                    delay.TotalMilliseconds, retryCount + 1, maxRetries);
                await Task.Delay(delay, cancellationToken);
            }
        }

        // This should never be reached due to the throw in the catch block
        throw new InvalidOperationException("Unexpected end of retry loop");
    }


    // --------- Domain events (no dispatch here; App layer coordinates) ---------
    public IReadOnlyList<IDomainEvent> GetDomainEvents() => Array.Empty<IDomainEvent>();
    public void ClearDomainEvents() { }

    [Obsolete("Use Application TransactionBehavior + IDomainEventCollector. Do not dispatch from DbContext.")]
    public Task<int> SaveChangesAndDispatchDomainEventsAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Dispatch must be coordinated by Application layer behaviors.");

    // --------- Hooks & Conventions (kept minimal) ---------
    protected virtual void ApplyAuditInformation() { }

    private static void ApplySoftDeleteQueryFilter(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(Core.Domain.Entities.Abstractions.ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                var p = Expression.Parameter(entityType.ClrType, "e");
                var prop = Expression.Property(p, nameof(Core.Domain.Entities.Abstractions.ISoftDeletable.IsDeleted));
                var filter = Expression.Lambda(Expression.Not(prop), p);
                entityType.SetQueryFilter(filter);
            }
        }
    }

    private static void ApplyVersionConcurrencyToken(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var versionProp = entityType.FindProperty("Version");
            if (versionProp != null)
            {
                versionProp.IsConcurrencyToken = true;
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
