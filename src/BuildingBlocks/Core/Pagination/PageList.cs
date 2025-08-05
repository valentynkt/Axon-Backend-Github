namespace BuildingBlocks.Core.Pagination;

public record PageList<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount) : IPageList<T>
    where T : class
{
    public int CurrentPageSize => Items.Count;
    public int CurrentStartIndex => TotalCount == 0 ? 0 : ((PageNumber - 1) * PageSize) + 1;
    public int CurrentEndIndex => TotalCount == 0 ? 0 : CurrentStartIndex + CurrentPageSize - 1;
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => PageNumber > 1;
    public bool HasNext => PageNumber < TotalPages;
}

/// <summary>
/// Non-generic helper class for PageList creation
/// </summary>
public static class PageList
{
    public static PageList<T> Empty<T>() where T : class => new(Enumerable.Empty<T>().ToList(), 0, 0, 0);

    public static PageList<T> Create<T>(IReadOnlyList<T> items, int pageNumber, int pageSize, int totalItems) where T : class
    {
        return new PageList<T>(items, pageNumber, pageSize, totalItems);
    }
}