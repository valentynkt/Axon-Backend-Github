using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Core.Pagination;
using BuildingBlocks.Persistence.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Persistence.Read;

/// <summary>
/// Generic Entity Framework read repository implementation
/// Optimized for read models with no tracking and query performance
/// </summary>
public class EfReadRepository<TReadModel, TId> : IReadRepository<TReadModel, TId>
    where TReadModel : class
    where TId : notnull
{
    protected DbContext Context { get; }
    protected DbSet<TReadModel> DbSet { get; }

    public EfReadRepository(DbContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        DbSet = Context.Set<TReadModel>();
    }

    public virtual async Task<TReadModel?> FindByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        return await DbSet.AsNoTracking().FirstOrDefaultAsync(CreateIdPredicate(id), cancellationToken);
    }

    public virtual async Task<TReadModel?> FindOneAsync(
        Expression<Func<TReadModel, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await DbSet.AsNoTracking().FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public virtual async Task<IReadOnlyList<TReadModel>> FindAsync(
        Expression<Func<TReadModel, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await DbSet.AsNoTracking().Where(predicate).ToListAsync(cancellationToken);
    }

    public virtual async Task<IPagedResult<TReadModel>> GetPagedAsync<TPageRequest>(
        TPageRequest request,
        CancellationToken cancellationToken = default)
        where TPageRequest : IPageRequest
    {
        ArgumentNullException.ThrowIfNull(request);
        
        var query = DbSet.AsNoTracking();
        
        // Apply filtering if filters are provided
        if (!string.IsNullOrWhiteSpace(request.Filters))
        {
            // Note: This is a basic implementation - in real scenarios you'd parse the filters
            // and build appropriate expressions based on your filtering strategy
        }
        
        // Apply sorting if sort order is provided
        if (!string.IsNullOrWhiteSpace(request.SortOrder))
        {
            // Note: This is a basic implementation - in real scenarios you'd parse the sort order
            // and apply appropriate ordering based on your sorting strategy
        }
        
        var totalCount = await query.CountAsync(cancellationToken);
        
        var items = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);
        
        return PagedResult.Create(items, request.PageNumber, request.PageSize, totalCount);
    }

    public virtual async Task<IPagedResult<TReadModel>> GetPagedAsync(
        Expression<Func<TReadModel, bool>>? predicate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking();
        
        if (predicate != null)
        {
            query = query.Where(predicate);
        }
        
        var totalCount = await query.CountAsync(cancellationToken);
        
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        
        return PagedResult.Create(items, pageNumber, pageSize, totalCount);
    }

    public virtual async Task<IReadOnlyList<TReadModel>> RawQueryAsync(
        string sql,
        CancellationToken cancellationToken = default,
        params object[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        
        return await DbSet
            .FromSqlRaw(sql, parameters)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public virtual async Task<long> CountAsync(
        Expression<Func<TReadModel, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        return predicate == null
            ? await DbSet.AsNoTracking().LongCountAsync(cancellationToken)
            : await DbSet.AsNoTracking().LongCountAsync(predicate, cancellationToken);
    }

    public virtual async Task<bool> AnyAsync(
        Expression<Func<TReadModel, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        return predicate == null
            ? await DbSet.AsNoTracking().AnyAsync(cancellationToken)
            : await DbSet.AsNoTracking().AnyAsync(predicate, cancellationToken);
    }

    public virtual async Task<IReadOnlyList<TReadModel>> GetByIdsAsync(
        IReadOnlyList<TId> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        
        if (!ids.Any())
        {
            return Array.Empty<TReadModel>();
        }
        
        // Build predicate for multiple IDs
        var parameter = Expression.Parameter(typeof(TReadModel), "x");
        var idProperty = GetIdProperty();
        var idPropertyAccess = Expression.Property(parameter, idProperty);
        
        var containsMethod = typeof(IReadOnlyList<TId>).GetMethod("Contains");
        var idsConstant = Expression.Constant(ids);
        var containsCall = Expression.Call(idsConstant, containsMethod!, idPropertyAccess);
        
        var lambda = Expression.Lambda<Func<TReadModel, bool>>(containsCall, parameter);
        
        return await DbSet.AsNoTracking().Where(lambda).ToListAsync(cancellationToken);
    }

    public virtual IQueryable<TReadModel> Query()
    {
        return DbSet.AsNoTracking();
    }

    public virtual async Task<TResult> ExecuteCompiledQueryAsync<TResult>(
        Func<IQueryable<TReadModel>, Task<TResult>> compiledQuery,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compiledQuery);
        
        var query = DbSet.AsNoTracking();
        return await compiledQuery(query);
    }

    /// <summary>
    /// Creates a predicate expression for finding entity by ID
    /// </summary>
    protected virtual Expression<Func<TReadModel, bool>> CreateIdPredicate(TId id)
    {
        var parameter = Expression.Parameter(typeof(TReadModel), "x");
        var idProperty = GetIdProperty();
        var idPropertyAccess = Expression.Property(parameter, idProperty);
        var idConstant = Expression.Constant(id, typeof(TId));
        var equality = Expression.Equal(idPropertyAccess, idConstant);
        
        return Expression.Lambda<Func<TReadModel, bool>>(equality, parameter);
    }

    /// <summary>
    /// Gets the ID property of the entity
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
            "Please ensure the entity has a property named 'Id' or override CreateIdPredicate method.");
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Context?.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}