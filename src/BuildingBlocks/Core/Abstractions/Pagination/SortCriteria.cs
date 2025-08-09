namespace BuildingBlocks.Core.Abstractions.Pagination;

/// <summary>
/// Enumeration for sort direction values.
/// </summary>
public enum SortDirection
{
    /// <summary>Ascending sort direction</summary>
    Asc = 0,
    /// <summary>Descending sort direction</summary>
    Desc = 1
}

/// <summary>
/// Immutable record representing sorting criteria for a specific property.
/// </summary>
/// <param name="PropertyName">The name of the property to sort by</param>
/// <param name="Direction">The sort direction (ascending or descending)</param>
public sealed record SortCriteria(string PropertyName, SortDirection Direction)
{
    /// <summary>
    /// Creates a new ascending sort criteria for the specified property.
    /// </summary>
    /// <param name="property">The property name to sort by</param>
    /// <returns>A new SortCriteria with ascending direction</returns>
    public static SortCriteria Ascending(string property) => new(property, SortDirection.Asc);
    
    /// <summary>
    /// Creates a new descending sort criteria for the specified property.
    /// </summary>
    /// <param name="property">The property name to sort by</param>
    /// <returns>A new SortCriteria with descending direction</returns>
    public static SortCriteria Descending(string property) => new(property, SortDirection.Desc);
}

/// <summary>
/// Static helper class for working with sort criteria collections.
/// </summary>
public static class SortHelpers
{
    /// <summary>
    /// Ensures the sort is total by appending a unique key if missing.
    /// This guarantees consistent, deterministic sorting results.
    /// </summary>
    /// <param name="sort">The existing sort criteria collection</param>
    /// <param name="uniqueKey">The unique key to append (defaults to "Id")</param>
    /// <param name="dir">The direction for the unique key sort (defaults to Ascending)</param>
    /// <returns>A new list with the unique key appended if not already present</returns>
    public static IReadOnlyList<SortCriteria> EnsureUniqueOrder(
        IReadOnlyList<SortCriteria>? sort, string uniqueKey = "Id", SortDirection dir = SortDirection.Asc)
    {
        var list = (sort ?? []).ToList();
        if (!list.Any(s => s.PropertyName.Equals(uniqueKey, StringComparison.OrdinalIgnoreCase)))
            list.Add(new(uniqueKey, dir));
        return list;
    }
}