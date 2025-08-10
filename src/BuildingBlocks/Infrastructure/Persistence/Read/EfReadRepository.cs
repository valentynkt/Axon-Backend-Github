using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using BuildingBlocks.Application;
using BuildingBlocks.Core.Abstractions.Pagination;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Persistence.Read;

/// <summary>
/// Generic Entity Framework read repository implementation
/// Optimized for queries with no tracking
/// </summary>
public class EfReadRepository<TReadModel, TId> : IReadRepository<TReadModel, TId>
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

    // ——— Paged / filtered ———
    public virtual async Task<IPageList<TReadModel>> GetPagedAsync<TPageRequest>(
        TPageRequest request,
        CancellationToken ct = default)
        where TPageRequest : IPageRequest
    {
        var query = _dbSet.AsNoTracking();
        
        // Apply sorting if request implements ISortablePageQuery
        if (request is ISortablePageQuery<TReadModel> sortableQuery && sortableQuery.EffectiveSortBy != null)
        {
            query = ApplySorting(query, sortableQuery.EffectiveSortBy);
        }

        var totalItems = await query.CountAsync(ct);
        
        var items = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var meta = PaginationMeta.CreateWithTotals(
            totalItems,
            request.PageNumber,
            request.PageSize,
            items.Count);

        return new PageList<TReadModel>(
            items,
            request.PageNumber,
            request.PageSize,
            totalItems,
            meta);
    }

    public virtual async Task<IPageList<TReadModel>> GetPagedAsync(
        Expression<Func<TReadModel, bool>>? predicate,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _dbSet.AsNoTracking();
        
        if (predicate != null)
        {
            query = query.Where(predicate);
        }

        var totalItems = await query.CountAsync(ct);
        
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var meta = PaginationMeta.CreateWithTotals(
            totalItems,
            pageNumber,
            pageSize,
            items.Count);

        return new PageList<TReadModel>(
            items,
            pageNumber,
            pageSize,
            totalItems,
            meta);
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
    private IQueryable<TReadModel> ApplySorting(IQueryable<TReadModel> query, IReadOnlyList<SortCriteria> sortCriteria)
    {
        if (sortCriteria.Count == 0) return query;

        IOrderedQueryable<TReadModel>? ordered = null;

        foreach (var criteria in sortCriteria)
        {
            var parameter = Expression.Parameter(typeof(TReadModel), "x");

            // Allow nested property paths ("Foo.Bar.Baz")
            Expression property = parameter;
            foreach (var part in criteria.PropertyName.Split('.'))
            {
                var prop = property.Type.GetProperty(
                    part,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                           ?? throw new ArgumentException($"Property '{part}' not found on '{property.Type.Name}'");

                property = Expression.Property(property, prop);
            }

            var lambda = Expression.Lambda(property, parameter);

            var methodName =
                ordered == null
                    ? (criteria.Direction == SortDirection.Asc ? "OrderBy" : "OrderByDescending")
                    : (criteria.Direction == SortDirection.Asc ? "ThenBy" : "ThenByDescending");

            var method = typeof(Queryable).GetMethods()
                .First(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(TReadModel), property.Type);

            ordered = (IOrderedQueryable<TReadModel>)method.Invoke(null, new object[] { ordered ?? query, lambda })!;
        }

        return ordered ?? query;
    }

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

public class EfReadRepository<TReadModel> : EfReadRepository<TReadModel, Guid>, IReadRepository<TReadModel>
    where TReadModel : class
{
    public EfReadRepository(DbContext context) : base(context) { }
}

internal class PageList<T> : IPageList<T>, IEnumerable
{
    private readonly List<T> _items;

    public PageList(IEnumerable<T> items, int pageNumber, int pageSize, long totalItems, PaginationMeta meta)
    {
        _items = items?.ToList() ?? new List<T>();
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalItems = totalItems;
        Meta = meta;
        TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
    }

    public IReadOnlyList<T> Items => _items.AsReadOnly();
    public PaginationMeta Meta { get; }
    public int PageNumber { get; }
    public int PageSize { get; }
    public long TotalItems { get; }
    public int TotalPages { get; }
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public T this[int index] => _items[index];
    public int Count => _items.Count;

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
