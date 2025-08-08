using System;
using System.Collections.Generic;
using static BuildingBlocks.Core.Abstractions.Pagination.SortHelpers;

namespace BuildingBlocks.Core.Abstractions.Pagination;

public abstract record PageQueryBase<TResponse> : IPageQuery<TResponse>, ISortablePageQuery<TResponse>, IPageRequest
{
    // ---- Canonical names ----
    public int Page { get; init; } = 1;            // preferred going forward
    public int PageSize { get; init; } = 25;
    public bool IncludeTotalCount { get; init; }

    // ---- Legacy alias (still required by IPageRequest) ----
    [Obsolete("Use Page instead of PageNumber.")]
    public int PageNumber
    {
        get => Page;
        init => Page = value;
    }

    // Sorting
    public IReadOnlyList<SortCriteria>? SortBy { get; init; }
    public abstract IReadOnlyList<SortCriteria> DefaultSort { get; }

    // Enforce total ordering by appending unique key if absent.
    public IReadOnlyList<SortCriteria> EffectiveSortBy =>
        EnsureUniqueOrder(SortBy is { Count: > 0 } ? SortBy : DefaultSort, "Id");

    // Derived helpers for repositories/EF queries
    public int Skip => (ValidatedPage - 1) * ValidatedPageSize;
    public int Take => ValidatedPageSize;

    // Centralized validation with caps
    public const int MaxPageSize = 100;

    private int ValidatedPage => Page < 1 ? 1 : Page;
    private int ValidatedPageSize => PageSize < 1 ? 1 : (PageSize > MaxPageSize ? MaxPageSize : PageSize);

    // ---- Legacy Sieve props (discouraged) ----
    [Obsolete("Use explicit filter properties + SortBy.")]
    public string? Filters { get; init; }

    [Obsolete("Use SortBy.")]
    public string? SortOrder { get; init; }
}