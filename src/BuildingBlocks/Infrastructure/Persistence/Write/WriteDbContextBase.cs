using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using BuildingBlocks.Core.Domain.Events;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.Persistence.Write;

/// <summary>
/// Base class for write-side DbContexts (CQRS).
/// Responsibilities here:
/// - Provider schema + configuration
/// - Transaction helpers
/// - Minimal, provider-safe auditing hook (override)
/// - Domain event collection (no dispatch)
/// - Global soft-delete filter (if bool property "IsDeleted" exists)
/// - Concurrency token convention for 'Version' (uint/int/long) when present
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
        // Module schema (when supported)
        modelBuilder.HasDefaultSchema(ModuleName.ToLowerInvariant());

        // Apply configurations
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

        // Conventions
        ApplySoftDeleteQueryFilter(modelBuilder);
        WriteDbContextBase<TModule>.ApplyVersionConcurrencyToken(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    // --------- Transactions ---------

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
            await base.SaveChangesAsync(cancellationToken);
            await _currentTransaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
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

    // --------- SaveChanges (auditing hook + concurrency handling) ---------

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInformation(); // hook for derived contexts
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Standard reconciliation: refresh and re-throw for upper layer handling if needed.
            foreach (var entry in ex.Entries)
            {
                var databaseValues = await entry.GetDatabaseValuesAsync(cancellationToken);
                if (databaseValues is null) continue;
                entry.OriginalValues.SetValues(databaseValues);
            }
            throw;
        }
    }

    /// <summary>
    /// Collect domain events from tracked aggregates.
    /// Duck-types entities exposing a readable property named 'DomainEvents'
    /// of IEnumerable&lt;IDomainEvent&gt; (or compatible), returning a flat list.
    /// </summary>
    public IReadOnlyList<IDomainEvent> GetDomainEvents()
    {
        var events = new List<IDomainEvent>(capacity: 32);

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is null) continue;

            var getter = DomainEventsGetterCache.Get(entry.Entity.GetType());
            if (getter is null) continue;

            if (getter(entry.Entity) is IEnumerable<IDomainEvent> list)
                events.AddRange(list);
        }

        return events;
    }

    /// <summary>
    /// Clears domain events on tracked aggregates that expose a 'ClearDomainEvents()' method.
    /// </summary>
    public void ClearDomainEvents()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is null) continue;

            var clearer = DomainEventsClearerCache.Get(entry.Entity.GetType());
            clearer?.Invoke(entry.Entity);
        }
    }

    [Obsolete("Use Application TransactionBehavior + IDomainEventCollector. Do not dispatch from DbContext.")]
    public Task<int> SaveChangesAndDispatchDomainEventsAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Dispatch must be coordinated by Application layer behaviors.");

    // --------- Hooks & Conventions ---------

    /// <summary>
    /// Override to apply auditing (CreatedAt/By, UpdatedAt/By, etc.) using your own interfaces/conventions.
    /// Default: no-op to keep base decoupled from specific domain contracts.
    /// </summary>
    protected virtual void ApplyAuditInformation() { }

    private void ApplySoftDeleteQueryFilter(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Only for real entity types.
            if (entityType.IsOwned() || entityType.ClrType.IsAbstract) continue;

            var isDeletedProp = entityType.FindProperty("IsDeleted");
            if (isDeletedProp?.ClrType == typeof(bool))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                // EF.Property<bool>(e, "IsDeleted") == false
                var body = Expression.Equal(
                    Expression.Call(
                        typeof(EF).GetMethod(nameof(EF.Property))!.MakeGenericMethod(typeof(bool)),
                        parameter,
                        Expression.Constant("IsDeleted")),
                    Expression.Constant(false));

                var lambda = Expression.Lambda(body, parameter);
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }

    private static void ApplyVersionConcurrencyToken(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var versionProp =
                entityType.FindProperty("Version") ??
                entityType.FindProperty("RowVersion");

            if (versionProp is null) continue;

            // Accept uint/int/long (common patterns)
            var t = versionProp.ClrType;
            if (t == typeof(uint) || t == typeof(int) || t == typeof(long) || t == typeof(byte[]))
            {
                modelBuilder
                    .Entity(entityType.ClrType)
                    .Property(versionProp.Name)
                    .IsConcurrencyToken();
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

    // --------- Reflection helpers (cached) ---------

    private static class DomainEventsGetterCache
    {
        private static readonly ConcurrentDictionary<Type, Func<object, object?>?> Cache = new();

        public static Func<object, object?>? Get(Type type) =>
            Cache.GetOrAdd(type, Create);

        private static Func<object, object?>? Create(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var prop = type.GetProperty("DomainEvents", flags);
            if (prop is null) return null;

            // Must be IEnumerable<IDomainEvent> compatible
            if (!typeof(System.Collections.IEnumerable).IsAssignableFrom(prop.PropertyType))
                return null;

            var obj = Expression.Parameter(typeof(object), "o");
            var cast = Expression.Convert(obj, type);
            var read = Expression.Property(cast, prop);
            var box = Expression.Convert(read, typeof(object));
            return Expression.Lambda<Func<object, object?>>(box, obj).Compile();
        }
    }

    private static class DomainEventsClearerCache
    {
        private static readonly ConcurrentDictionary<Type, Action<object>?> Cache = new();

        public static Action<object>? Get(Type type) =>
            Cache.GetOrAdd(type, Create);

        private static Action<object>? Create(Type type)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var method = type.GetMethod("ClearDomainEvents", flags, Array.Empty<Type>());
            if (method is null) return null;

            var obj = Expression.Parameter(typeof(object), "o");
            var cast = Expression.Convert(obj, type);
            var call = Expression.Call(cast, method);
            return Expression.Lambda<Action<object>>(call, obj).Compile();
        }
    }
}
