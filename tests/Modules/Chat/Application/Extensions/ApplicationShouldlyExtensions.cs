namespace Axon.Modules.Chat.Application.Tests.Extensions;

/// <summary>
/// Shouldly extensions specifically for Chat Application layer testing.
/// Provides fluent assertion methods for common application-level scenarios.
/// Extends the domain extensions with application-specific concerns.
/// </summary>
public static class ApplicationShouldlyExtensions
{
    #region Pagination Extensions

    /// <summary>
    /// Asserts that a paged result has the expected pagination properties
    /// </summary>
    public static Paged<T> ShouldHavePagination<T>(this Paged<T> pagedResult, int expectedPageNumber, int expectedPageSize, string? customMessage = null)
    {
        pagedResult.PageNumber.ShouldBe(expectedPageNumber, customMessage ?? $"Page number should be {expectedPageNumber}");
        pagedResult.PageSize.ShouldBe(expectedPageSize, customMessage ?? $"Page size should be {expectedPageSize}");
        return pagedResult;
    }

    /// <summary>
    /// Asserts that a paged result has the expected total count
    /// </summary>
    public static Paged<T> ShouldHaveTotalCount<T>(this Paged<T> pagedResult, int expectedTotalCount, string? customMessage = null)
    {
        pagedResult.TotalCount.ShouldBe(expectedTotalCount, customMessage ?? $"Total count should be {expectedTotalCount}");
        return pagedResult;
    }

    /// <summary>
    /// Asserts that a paged result has items within the page size limit
    /// </summary>
    public static Paged<T> ShouldHaveItemsWithinPageSize<T>(this Paged<T> pagedResult, string? customMessage = null)
    {
        pagedResult.Items.Count.ShouldBeLessThanOrEqualTo(pagedResult.PageSize, 
            customMessage ?? $"Items count should not exceed page size of {pagedResult.PageSize}");
        return pagedResult;
    }

    /// <summary>
    /// Asserts that a paged result is properly sorted
    /// </summary>
    public static Paged<T> ShouldBeSortedBy<T, TKey>(this Paged<T> pagedResult, Func<T, TKey> keySelector, bool ascending = true, string? customMessage = null)
        where TKey : IComparable<TKey>
    {
        if (pagedResult.Items.Count <= 1) return pagedResult;

        var items = pagedResult.Items.ToList();
        var expectedOrder = ascending ? items.OrderBy(keySelector) : items.OrderByDescending(keySelector);
        
        items.ShouldBe(expectedOrder.ToList(), customMessage ?? $"Items should be sorted by the specified key in {(ascending ? "ascending" : "descending")} order");
        return pagedResult;
    }

    #endregion

    #region Performance Extensions

    /// <summary>
    /// Asserts that an operation completes within the specified time
    /// </summary>
    public static async Task<T> ShouldCompleteWithin<T>(this Task<T> task, TimeSpan maxExecutionTime, string? customMessage = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await task;
        stopwatch.Stop();

        stopwatch.Elapsed.ShouldBeLessThan(maxExecutionTime,
            customMessage ?? $"Operation should complete within {maxExecutionTime.TotalMilliseconds}ms but took {stopwatch.Elapsed.TotalMilliseconds}ms");
        
        return result;
    }

    /// <summary>
    /// Asserts that an operation takes at least the specified time (useful for testing delays/timeouts)
    /// </summary>
    public static async Task<T> ShouldTakeAtLeast<T>(this Task<T> task, TimeSpan minExecutionTime, string? customMessage = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await task;
        stopwatch.Stop();

        stopwatch.Elapsed.ShouldBeGreaterThanOrEqualTo(minExecutionTime,
            customMessage ?? $"Operation should take at least {minExecutionTime.TotalMilliseconds}ms but took only {stopwatch.Elapsed.TotalMilliseconds}ms");
        
        return result;
    }

    #endregion

    #region Collection Extensions

    /// <summary>
    /// Asserts that all items match a condition
    /// </summary>
    public static IEnumerable<T> ShouldAllMatch<T>(this IEnumerable<T> items, Predicate<T> condition, string? customMessage = null)
    {
        var itemsList = items.ToList();
        var allItemsMatch = itemsList.All(item => condition(item));
        
        allItemsMatch.ShouldBeTrue(customMessage ?? "All items should match the specified condition");
        return itemsList;
    }

    /// <summary>
    /// Asserts that items are in chronological order
    /// </summary>
    public static IEnumerable<T> ShouldBeInChronologicalOrder<T>(this IEnumerable<T> items, Func<T, DateTime> dateSelector, string? customMessage = null)
    {
        var itemsList = items.ToList();
        if (itemsList.Count <= 1) return itemsList;

        for (int i = 1; i < itemsList.Count; i++)
        {
            dateSelector(itemsList[i]).ShouldBeGreaterThanOrEqualTo(dateSelector(itemsList[i - 1]),
                customMessage ?? $"Item at index {i} should have date >= previous item");
        }

        return itemsList;
    }

    #endregion

    #region Helper Extensions

    /// <summary>
    /// Verifies that filtering was applied correctly
    /// </summary>
    public static IEnumerable<T> ShouldHaveAppliedFilter<T>(this IEnumerable<T> items, Predicate<T> filter, string filterDescription, string? customMessage = null)
    {
        var itemsList = items.ToList();
        var allItemsMatchFilter = itemsList.All(item => filter(item));
        
        allItemsMatchFilter.ShouldBeTrue(
            customMessage ?? $"All items should match the filter criteria: {filterDescription}");
        
        return itemsList;
    }

    #endregion
}