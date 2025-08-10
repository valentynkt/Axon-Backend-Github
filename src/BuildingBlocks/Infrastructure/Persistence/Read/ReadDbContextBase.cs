using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Persistence.Read;

/// <summary>
/// Base class for read-side DbContexts (CQRS).
/// - No tracking by default
/// - No lazy loading
/// - Transaction helpers for edge cases only
/// </summary>
public abstract class ReadDbContextBase<TModule> : DbContext, IReadDbContext<TModule>
    where TModule : class
{
    private readonly ILogger<ReadDbContextBase<TModule>> _logger;
    private IDbContextTransaction? _currentTransaction;

    protected ReadDbContextBase(
        DbContextOptions options,
        ILogger<ReadDbContextBase<TModule>>? logger = null) : base(options)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ReadDbContextBase<TModule>>.Instance;

        ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        ChangeTracker.AutoDetectChangesEnabled = false;
        ChangeTracker.LazyLoadingEnabled = false;
    }

    public abstract string ModuleName { get; }

    public bool HasActiveTransaction => _currentTransaction != null;
    public string? CurrentTransactionId => _currentTransaction?.TransactionId.ToString();

    public IExecutionStrategy CreateExecutionStrategy() => Database.CreateExecutionStrategy();

    public IQueryable<TReadModel> Query<TReadModel>() where TReadModel : class
        => Set<TReadModel>().AsNoTracking();

    public Task<TResult> ExecuteCompiledQueryAsync<TResult>(
        Func<IReadDbContext<TModule>, Task<TResult>> compiledQuery,
        CancellationToken cancellationToken = default)
        => compiledQuery(this);

    public virtual IReadOnlyList<IDomainEvent> GetDomainEvents()
    {
        // Read contexts don’t raise domain events. Return empty for interface compliance.
        return Array.Empty<IDomainEvent>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Default schema per module (if provider supports it)
        modelBuilder.HasDefaultSchema(ModuleName.ToLowerInvariant());

        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
        ConfigureReadModelOptimizations(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>Optional: index conventions commonly used by read models.</summary>
    protected virtual void ConfigureReadModelOptimizations(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var builder = modelBuilder.Entity(entityType.ClrType);

            if (entityType.FindProperty("CreatedAt") is not null)
                builder.HasIndex("CreatedAt").HasDatabaseName($"ix_{entityType.GetTableName()}_created_at");

            if (entityType.FindProperty("UpdatedAt") is not null)
                builder.HasIndex("UpdatedAt").HasDatabaseName($"ix_{entityType.GetTableName()}_updated_at");
        }
    }

    // ---------- Transactions (rare on read side; provided for completeness) ----------

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null) return;
        _currentTransaction = await Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null) return;
        try
        {
            await _currentTransaction.CommitAsync(cancellationToken);
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
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
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public async Task ExecuteTransactionalAsync(
        Func<Task> operation,
        CancellationToken cancellationToken = default)
    {
        var strategy = CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
            try
            {
                await operation();
                await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<T> ExecuteTransactionalAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        var strategy = CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);
            try
            {
                var result = await operation();
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

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Read contexts typically should not save. Keep for rare cases, but warn.
        _logger.LogWarning("SaveChangesAsync called on READ context ({Module}). Verify this is intended.", ModuleName);
        return base.SaveChangesAsync(cancellationToken);
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
