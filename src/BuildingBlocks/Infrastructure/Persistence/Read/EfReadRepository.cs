using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Persistence.Read;

/// <summary>
/// Generic Entity Framework read repository implementation
/// Optimized for OData queries with no tracking
/// </summary>
public class EfReadRepository<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TReadModel, TId> : IReadRepository<TReadModel, TId>
    where TReadModel : class
    where TId : notnull
{
    private readonly DbContext _context;
    private readonly DbSet<TReadModel> _dbSet;

    protected DbContext Context => _context;
    protected DbSet<TReadModel> DbSet => _dbSet;

    public EfReadRepository(DbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _dbSet = _context.Set<TReadModel>();
    }

    // ——— Query builder (for OData) ———
    public virtual IQueryable<TReadModel> Query(Expression<Func<TReadModel, bool>>? predicate = null)
    {
        var query = _dbSet.AsNoTracking();
        
        // Apply user-scoped security filter (override in derived classes)
        query = ApplyUserScopeFilter(query);
        
        // Apply additional predicate if provided
        if (predicate != null)
            query = query.Where(predicate);
        
        // Apply stable ordering (always end with Id for cursor stability)
        // This ensures consistent pagination with $skiptoken
        query = ApplyStableOrdering(query);
        
        return query;
    }

    /// <summary>
    /// Override this to apply user-scoped filtering
    /// </summary>
    protected virtual IQueryable<TReadModel> ApplyUserScopeFilter(IQueryable<TReadModel> query)
    {
        // Default: no filtering (override in derived classes for security)
        return query;
    }

    /// <summary>
    /// Apply stable ordering for consistent pagination
    /// </summary>
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2060:MakeGenericMethod",
        Justification = "Reflection used for dynamic ordering is safe for known Queryable methods")]
    protected virtual IQueryable<TReadModel> ApplyStableOrdering(IQueryable<TReadModel> query)
    {
        // Try to find UpdatedAt property
        var updatedAtProperty = typeof(TReadModel).GetProperty("UpdatedAt");
        if (updatedAtProperty != null)
        {
            var parameter = Expression.Parameter(typeof(TReadModel), "x");
            var property = Expression.Property(parameter, updatedAtProperty);
            var lambda = Expression.Lambda(property, parameter);

            var orderByMethod = typeof(Queryable).GetMethods()
                .First(m => m.Name == "OrderBy" && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(TReadModel), updatedAtProperty.PropertyType);

            query = (IQueryable<TReadModel>)orderByMethod.Invoke(null, new object[] { query, lambda })!;
        }

        // Always end with Id for stable ordering
        var idProperty = GetIdProperty();
        var idParameter = Expression.Parameter(typeof(TReadModel), "x");
        var idPropertyAccess = Expression.Property(idParameter, idProperty);
        var idLambda = Expression.Lambda(idPropertyAccess, idParameter);

        var thenByMethod = typeof(Queryable).GetMethods()
            .First(m => m.Name == "ThenBy" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(TReadModel), idProperty.PropertyType);

        if (updatedAtProperty != null)
        {
            query = (IQueryable<TReadModel>)thenByMethod.Invoke(null, new object[] { query, idLambda })!;
        }
        else
        {
            var orderByIdMethod = typeof(Queryable).GetMethods()
                .First(m => m.Name == "OrderBy" && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(TReadModel), idProperty.PropertyType);

            query = (IQueryable<TReadModel>)orderByIdMethod.Invoke(null, new object[] { query, idLambda })!;
        }

        return query;
    }

    // ——— Simple fetches ———
    public virtual async Task<TReadModel?> FindByIdAsync(TId id, CancellationToken ct = default)
    {
        return await _dbSet.FindAsync([id], ct);
    }

    public virtual async Task<TReadModel?> FindOneAsync(
        Expression<Func<TReadModel, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _dbSet.AsNoTracking()
            .FirstOrDefaultAsync(predicate, ct);
    }

    public virtual async Task<IReadOnlyList<TReadModel>> FindAsync(
        Expression<Func<TReadModel, bool>> predicate,
        CancellationToken ct = default)
    {
        var result = await _dbSet.AsNoTracking()
            .Where(predicate)
            .ToListAsync(ct);
        return result.AsReadOnly();
    }

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2060:MakeGenericMethod",
        Justification = "Reflection used for Contains method is safe for known types")]
    public virtual async Task<IReadOnlyList<TReadModel>> GetByIdsAsync(
        IReadOnlyList<TId> ids,
        CancellationToken ct = default)
    {
        if (ids == null || ids.Count == 0)
            return Array.Empty<TReadModel>();

        var idList = ids.Distinct().ToList();

        // Build predicate: idList.Contains(x.Id)
        var parameter = Expression.Parameter(typeof(TReadModel), "x");
        var idProperty = GetIdProperty();
        var idPropertyAccess = Expression.Property(parameter, idProperty);

        var containsMethod = typeof(Enumerable)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(TId));

        var contains = Expression.Call(
            containsMethod,
            Expression.Constant(idList),
            idPropertyAccess);

        var predicate = Expression.Lambda<Func<TReadModel, bool>>(contains, parameter);

        var result = await _dbSet.AsNoTracking()
            .Where(predicate)
            .ToListAsync(ct);

        return result.AsReadOnly();
    }

    // ——— Aggregate functions ———
    public virtual async Task<long> CountAsync(
        Expression<Func<TReadModel, bool>>? predicate = null,
        CancellationToken ct = default)
    {
        var query = _dbSet.AsNoTracking();
        
        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        return await query.LongCountAsync(ct);
    }

    public virtual async Task<long> CountAsync(CancellationToken ct)
    {
        return await CountAsync(null, ct);
    }

    public virtual async Task<bool> AnyAsync(
        Expression<Func<TReadModel, bool>>? predicate = null,
        CancellationToken ct = default)
    {
        var query = _dbSet.AsNoTracking();
        
        if (predicate != null)
        {
            return await query.AnyAsync(predicate, ct);
        }

        return await query.AnyAsync(ct);
    }

    public virtual async Task<bool> AnyAsync(CancellationToken ct)
    {
        return await AnyAsync(null, ct);
    }

    // ——— H e l p e r  M e t h o d s ———
    protected virtual PropertyInfo GetIdProperty()
    {
        var idProperty = typeof(TReadModel).GetProperty("Id");
        if (idProperty != null && idProperty.PropertyType == typeof(TId))
        {
            return idProperty;
        }
        
        var properties = typeof(TReadModel).GetProperties()
            .Where(p => p.Name.EndsWith("Id") && p.PropertyType == typeof(TId))
            .ToList();
            
        if (properties.Count == 1)
        {
            return properties[0];
        }
        
        throw new InvalidOperationException(
            $"Unable to determine ID property for type {typeof(TReadModel).Name}. " +
            "Please ensure the model has a property named 'Id'.");
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        // Repository doesn't own the DbContext, so we don't dispose it
    }
}

[UnconditionalSuppressMessage("ReflectionAnalysis", "IL2091",
    Justification = "Entity Framework repository pattern requires reflection for entity operations")]
public class EfReadRepository<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TReadModel> : EfReadRepository<TReadModel, Guid>, IReadRepository<TReadModel>
    where TReadModel : class
{
    public EfReadRepository(DbContext context) : base(context) { }
}