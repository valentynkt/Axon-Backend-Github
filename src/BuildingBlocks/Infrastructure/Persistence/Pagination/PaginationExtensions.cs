using System.Linq.Expressions;
using System.Reflection;
using BuildingBlocks.Core.Abstractions.Pagination;
using BuildingBlocks.Core.Diagnostics;
using Microsoft.EntityFrameworkCore;
using static BuildingBlocks.Core.Abstractions.Pagination.SortHelpers;

namespace BuildingBlocks.Infrastructure.Persistence.Pagination;

/// <summary>
/// Single, Core-aligned pagination extensions:
/// - Works with <see cref="IPageRequest"/> and optional <see cref="ISortablePageQuery{TResponse}"/>
/// - Supports IncludeTotalCount (exact totals) and countless mode (PageSize+1)
/// - Deterministic, nested-path sorting via <see cref="SortCriteria"/> (e.g., "User.Name")
/// - No external (Sieve) dependency
/// </summary>
public static class PaginationExtensions
{
    // Keep in sync with Core/PageQueryBase<TResponse>.MaxPageSize
    private const int MaxPageSize = 100;

    // =====================================================================
    // EF Core IQueryable overloads
    // =====================================================================

    /// <summary>
    /// Paginate with Core request + optional sort criteria.
    /// If <paramref name="sortBy"/> is null and <paramref name="request"/> implements
    /// <see cref="ISortablePageQuery{TResponse}"/>, EffectiveSortBy is used automatically.
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> source,
        IPageRequest request,
        IReadOnlyList<SortCriteria>? sortBy = null,
        CancellationToken ct = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);

        var (page, pageSize) = ValidateAndNormalize(request);

        // Determine sorting (prefer ISortablePageQuery.EffectiveSortBy when available)
        if (sortBy is null && request is ISortablePageQuery<PagedResult<T>> sortable)
            sortBy = sortable.EffectiveSortBy;

        if (sortBy is { Count: > 0 })
            source = ApplySorting(source, EnsureUniqueOrder(sortBy, "Id"));

        if (request.IncludeTotalCount)
        {
            // Exact totals path
            var total = await source.CountAsync(ct);

            var items = total == 0
                ? Array.Empty<T>()
                : await source
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(ct);

            var meta = PaginationMeta.CreateWithTotals(
                totalCount: total,
                page: page,
                pageSize: pageSize,
                currentPageSize: items.Count);

            return new PagedResult<T>(items.AsReadOnly(), meta);
        }
        else
        {
            // Countless path (PageSize+1 trick to detect HasNext)
            var itemsPlusOne = await source
                .Skip((page - 1) * pageSize)
                .Take(pageSize + 1)
                .ToListAsync(ct);

            var hasNext = itemsPlusOne.Count > pageSize;
            var items = hasNext ? itemsPlusOne.Take(pageSize).ToList() : itemsPlusOne;

            var meta = PaginationMeta.CreateWithoutTotals(
                page: page,
                pageSize: pageSize,
                currentPageSize: items.Count,
                hasNext: hasNext);

            return new PagedResult<T>(items.AsReadOnly(), meta);
        }
    }

    /// <summary>
    /// Ideal path when using PageQueryBase&lt;T&gt;: request implements both IPageRequest and ISortablePageQuery.
    /// </summary>
    public static Task<PagedResult<T>> ToPagedResultAsync<T, TQuery>(
        this IQueryable<T> source,
        TQuery query,
        CancellationToken ct = default)
        where T : class
        where TQuery : IPageRequest, ISortablePageQuery<PagedResult<T>>
        => source.ToPagedResultAsync(query, query.EffectiveSortBy, ct);

    // =====================================================================
    // In-memory IEnumerable overload (use sparingly; prefer IQueryable)
    // =====================================================================

    public static PagedResult<T> ToPagedResult<T>(
        this IEnumerable<T> source,
        int pageNumber,
        int pageSize)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(source);

        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), PaginationErrors.InvalidPageNumber.Message);
        if (pageSize < 1)
            throw new ArgumentOutOfRangeException(nameof(pageSize), PaginationErrors.InvalidPageSize.Message);
        if (pageSize > MaxPageSize)
            throw new ArgumentOutOfRangeException(nameof(pageSize), PaginationErrors.PageSizeExceedsMaximum(MaxPageSize).Message);

        var list = source as IReadOnlyList<T> ?? (IReadOnlyList<T>)source.ToList().AsReadOnly();

        if (list.Count == 0)
        {
            var emptyMeta = PaginationMeta.CreateWithTotals(0, pageNumber, pageSize, 0);
            return new PagedResult<T>(Array.Empty<T>(), emptyMeta);
        }

        var items = list.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        var meta = PaginationMeta.CreateWithTotals(list.Count, pageNumber, pageSize, items.Count);

        return new PagedResult<T>(items.AsReadOnly(), meta);
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private static (int page, int pageSize) ValidateAndNormalize(IPageRequest request)
    {
        var page = request.PageNumber;
        var size = request.PageSize;

        if (page < 1)
            throw new ArgumentOutOfRangeException(nameof(request.PageNumber), PaginationErrors.InvalidPageNumber.Message);
        if (size < 1)
            throw new ArgumentOutOfRangeException(nameof(request.PageSize), PaginationErrors.InvalidPageSize.Message);
        if (size > MaxPageSize)
            throw new ArgumentOutOfRangeException(nameof(request.PageSize), PaginationErrors.PageSizeExceedsMaximum(MaxPageSize).Message);

        return (page, size);
    }

    /// <summary>
    /// Apply dynamic OrderBy/ThenBy based on SortCriteria (supports nested paths like "Foo.Bar.Baz").
    /// </summary>
    private static IQueryable<T> ApplySorting<T>(IQueryable<T> query, IReadOnlyList<SortCriteria> sort)
    {
        if (sort.Count == 0) return query;

        IOrderedQueryable<T>? ordered = null;

        foreach (var s in sort)
        {
            var parameter = Expression.Parameter(typeof(T), "x");

            // Allow nested property paths ("Foo.Bar.Baz")
            Expression property = parameter;
            foreach (var part in s.PropertyName.Split('.'))
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
                    ? (s.Direction == SortDirection.Asc ? "OrderBy" : "OrderByDescending")
                    : (s.Direction == SortDirection.Asc ? "ThenBy" : "ThenByDescending");

            var method = typeof(Queryable).GetMethods()
                .First(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(T), property.Type);

            ordered = (IOrderedQueryable<T>)method.Invoke(null, new object[] { ordered ?? query, lambda })!;
        }

        return ordered ?? query;
    }
}
