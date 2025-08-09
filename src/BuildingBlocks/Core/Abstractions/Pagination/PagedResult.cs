namespace BuildingBlocks.Core.Abstractions.Pagination;

/// <summary>
/// Immutable record representing a paginated result with items and metadata.
/// Implements both modern IPageList&lt;T&gt; and legacy IPagedResult&lt;T&gt; interfaces for compatibility.
/// </summary>
/// <typeparam name="T">The type of items in the paginated result</typeparam>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    PaginationMeta Meta
) : IPageList<T>, IPagedResult<T>
{
    // ---- Legacy flattened properties (delegate to Meta). Keep for compatibility. ----
    [Obsolete("Use Meta.TotalCount.")]
    public long TotalCount => Meta.TotalCount;

    [Obsolete("Use Meta.Page.")]
    public int PageNumber => Meta.Page;

    [Obsolete("Use Meta.PageSize.")]
    public int PageSize => Meta.PageSize;

    [Obsolete("Use Meta.TotalPages.")]
    public long TotalPages => Meta.TotalPages;

    [Obsolete("Use Meta.HasPrevious.")]
    public bool HasPrevious => Meta.HasPrevious;

    [Obsolete("Use Meta.HasNext.")]
    public bool HasNext => Meta.HasNext;

    [Obsolete("Use Meta.CurrentPageSize.")]
    public int CurrentPageSize => Meta.CurrentPageSize;

    [Obsolete("Use Meta.CurrentStartIndex.")]
    public int CurrentStartIndex => Meta.CurrentStartIndex;

    [Obsolete("Use Meta.CurrentEndIndex.")]
    public int CurrentEndIndex => Meta.CurrentEndIndex;
}
