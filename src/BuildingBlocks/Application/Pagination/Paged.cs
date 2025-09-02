#nullable enable

namespace BuildingBlocks.Application.Pagination;

/// <summary>
/// Represents a paginated collection of items with metadata about pagination state.
/// Provides computed properties for navigation and status information.
/// </summary>
/// <typeparam name="T">The type of items in the collection</typeparam>
public sealed record Paged<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    long TotalCount)
{
    /// <summary>
    /// Calculates the total number of pages based on total count and page size.
    /// </summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    /// <summary>
    /// Indicates whether there is a previous page available.
    /// </summary>
    public bool HasPrevious => PageNumber > 1;

    /// <summary>
    /// Indicates whether there is a next page available.
    /// </summary>
    public bool HasNext => PageNumber < TotalPages;

    /// <summary>
    /// Gets the number of items in the current page.
    /// </summary>
    public int Count => Items.Count;

    /// <summary>
    /// Indicates whether the current page is empty.
    /// </summary>
    public bool IsEmpty => Count == 0;

    /// <summary>
    /// Gets the 1-based index of the first item on the current page.
    /// </summary>
    public long FirstItemIndex => TotalCount == 0 ? 0 : ((PageNumber - 1) * PageSize) + 1;

    /// <summary>
    /// Gets the 1-based index of the last item on the current page.
    /// </summary>
    public long LastItemIndex => FirstItemIndex + Count - 1;
}

/// <summary>
/// Non-generic factory class for creating Paged instances.
/// Avoids CA1000 analyzer warning about static members on generic types.
/// </summary>
public static class Paged
{
    /// <summary>
    /// Creates an empty paged result.
    /// </summary>
    /// <typeparam name="T">The type of items</typeparam>
    /// <returns>An empty Paged instance with no items</returns>
    public static Paged<T> Empty<T>() => new(
        Items: Array.Empty<T>(),
        PageNumber: 1,
        PageSize: Page.DefaultSize,
        TotalCount: 0);

    /// <summary>
    /// Creates a paged result from a collection and page information.
    /// </summary>
    /// <typeparam name="T">The type of items</typeparam>
    /// <param name="items">The items for this page</param>
    /// <param name="page">The page information</param>
    /// <param name="totalCount">The total number of items across all pages</param>
    /// <returns>A new Paged instance</returns>
    public static Paged<T> Create<T>(IReadOnlyList<T> items, Page page, long totalCount) =>
        new(items, page.Number, page.Size, totalCount);
}