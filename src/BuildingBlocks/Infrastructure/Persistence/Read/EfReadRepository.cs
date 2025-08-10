using System.Linq.Expressions;
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
    protected readonly DbContext _context;
    protected readonly DbSet<TReadModel> _dbSet;

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

        // Build predicate for ID matching
        var parameter = Expression.Parameter(typeof(TReadModel), "x");
        var idProperty = GetIdProperty();
        var idPropertyAccess = Expression.Property(parameter, idProperty);
        
        Expression? combinedExpression = null;
        foreach (var id in ids)
        {
            var idConstant = Expression.Constant(id, typeof(TId));
            var equality = Expression.Equal(idPropertyAccess, idConstant);
            
            combinedExpression = combinedExpression == null 
                ? equality 
                : Expression.OrElse(combinedExpression, equality);
        }

        if (combinedExpression == null)
            return Array.Empty<TReadModel>();

        var predicate = Expression.Lambda<Func<TReadModel, bool>>(combinedExpression, parameter);
        
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
        
        // Apply filtering if request implements IPageQuery
        if (request is IPageQuery<TReadModel> pageQuery && pageQuery.Filter != null)
        {
            query = query.Where(pageQuery.Filter);
        }

        // Apply sorting if request implements ISortablePageQuery
        if (request is ISortablePageQuery<TReadModel> sortableQuery && sortableQuery.SortBy != null)
        {
            query = sortableQuery.SortDirection == SortDirection.Ascending
                ? query.OrderBy(sortableQuery.SortBy)
                : query.OrderByDescending(sortableQuery.SortBy);
        }

        var totalItems = await query.CountAsync(ct);
        
        var items = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        return new PageList<TReadModel>(
            items,
            request.PageNumber,
            request.PageSize,
            totalItems);
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

        return new PageList<TReadModel>(
            items,
            pageNumber,
            pageSize,
            totalItems);
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
    /// <summary>
    /// Gets the ID property of the read model
    /// </summary>
    protected virtual System.Reflection.PropertyInfo GetIdProperty()
    {
        // First try to find "Id" property
        var idProperty = typeof(TReadModel).GetProperty("Id");
        if (idProperty != null && idProperty.PropertyType == typeof(TId))
        {
            return idProperty;
        }
        
        // If not found, look for properties ending with "Id"
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

/// <summary>
/// Convenience implementation for Guid-based read models
/// </summary>
public class EfReadRepository<TReadModel> : EfReadRepository<TReadModel, Guid>, IReadRepository<TReadModel>
    where TReadModel : class
{
    public EfReadRepository(DbContext context) : base(context)
    {
    }
}

/// <summary>
/// Internal PageList implementation for pagination
/// </summary>
internal class PageList<T> : IPageList<T>
{
    private readonly List<T> _items;

    public PageList(IEnumerable<T> items, int pageNumber, int pageSize, long totalItems)
    {
        _items = items?.ToList() ?? new List<T>();
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalItems = totalItems;
        TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
    }

    public IReadOnlyList<T> Items => _items.AsReadOnly();
    public int PageNumber { get; }
    public int PageSize { get; }
    public long TotalItems { get; }
    public int TotalPages { get; }
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
    
    // IReadOnlyList<T> implementation
    public T this[int index] => _items[index];
    public int Count => _items.Count;
    
    // IEnumerable<T> implementation
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}