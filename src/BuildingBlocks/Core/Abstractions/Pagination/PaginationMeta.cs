
namespace BuildingBlocks.Core.Abstractions.Pagination;

public sealed record PaginationMeta(
    long TotalCount,
    int Page,
    int PageSize,
    long TotalPages,
    bool HasPrevious,
    bool HasNext,
    int CurrentPageSize,
    int CurrentStartIndex,
    int CurrentEndIndex,
    IReadOnlyDictionary<string, object>? Metadata = null)
{
    // Exact totals path (IncludeTotalCount=true)
    public static PaginationMeta CreateWithTotals(
        long totalCount, int page, int pageSize, int currentPageSize, IReadOnlyDictionary<string, object>? metadata = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var totalPages = totalCount == 0 ? 0 : (totalCount + pageSize - 1) / pageSize;
        var hasPrev = page > 1;
        var hasNext = totalPages > 0 && page < totalPages;

        var (start, end) = currentPageSize == 0
            ? (0, 0)
            : (((page - 1) * pageSize + 1), ((page - 1) * pageSize + currentPageSize));

        return new PaginationMeta(totalCount, page, pageSize, totalPages, hasPrev, hasNext,
            currentPageSize, start, end, metadata);
    }

    // Countless path (IncludeTotalCount=false). Caller must provide hasNext (via PageSize+1 read).
    public static PaginationMeta CreateWithoutTotals(
        int page, int pageSize, int currentPageSize, bool hasNext, IReadOnlyDictionary<string, object>? metadata = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var hasPrev = page > 1;
        var (start, end) = currentPageSize == 0
            ? (0, 0)
            : (((page - 1) * pageSize + 1), ((page - 1) * pageSize + currentPageSize));

        return new PaginationMeta(0, page, pageSize, 0, hasPrev, hasNext,
            currentPageSize, start, end, metadata);
    }
}