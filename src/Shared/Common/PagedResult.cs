namespace Axon.Shared.Common;

/// <summary>
/// Represents a paginated result set following SPARC patterns
/// </summary>
/// <typeparam name="T">The type of items in the result</typeparam>
public sealed class PagedResult<T>
{
    /// <summary>
    /// The items in the current page
    /// </summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>
    /// The current page number (1-based)
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// The number of items per page
    /// </summary>
    public int PageSize { get; }

    /// <summary>
    /// The total number of items across all pages
    /// </summary>
    public int TotalCount { get; }

    /// <summary>
    /// The total number of pages
    /// </summary>
    public int TotalPages { get; }

    /// <summary>
    /// Whether there is a previous page
    /// </summary>
    public bool HasPreviousPage => PageNumber > 1;

    /// <summary>
    /// Whether there is a next page
    /// </summary>
    public bool HasNextPage => PageNumber < TotalPages;

    /// <summary>
    /// Whether this is the first page
    /// </summary>
    public bool IsFirstPage => PageNumber == 1;

    /// <summary>
    /// Whether this is the last page
    /// </summary>
    public bool IsLastPage => PageNumber == TotalPages;

    private PagedResult(
        IReadOnlyList<T> items,
        int pageNumber,
        int pageSize,
        int totalCount)
    {
        Items = items;
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalCount = totalCount;
        TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
    }

    /// <summary>
    /// Creates a new paged result
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "PagedResult pattern requires static factory methods")]
    public static PagedResult<T> Create(
        IReadOnlyList<T> items,
        int pageNumber,
        int pageSize,
        int totalCount)
    {
        return new PagedResult<T>(items, pageNumber, pageSize, totalCount);
    }

    /// <summary>
    /// Creates an empty paged result
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "PagedResult pattern requires static factory methods")]
    public static PagedResult<T> Empty(int pageNumber = 1, int pageSize = 20)
    {
        return new PagedResult<T>(Array.Empty<T>(), pageNumber, pageSize, 0);
    }
}