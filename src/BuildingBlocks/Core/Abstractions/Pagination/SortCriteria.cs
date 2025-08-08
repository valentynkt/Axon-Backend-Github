using System;
using System.Collections.Generic;
using System.Linq;

namespace BuildingBlocks.Core.Abstractions.Pagination;

public enum SortDirection
{
    Asc = 0,
    Desc = 1
}

public sealed record SortCriteria(string PropertyName, SortDirection Direction)
{
    public static SortCriteria Ascending(string property)  => new(property, SortDirection.Asc);
    public static SortCriteria Descending(string property) => new(property, SortDirection.Desc);
}

public static class SortHelpers
{
    /// Ensures the sort is total by appending a unique key if missing.
    public static IReadOnlyList<SortCriteria> EnsureUniqueOrder(
        IReadOnlyList<SortCriteria>? sort, string uniqueKey = "Id", SortDirection dir = SortDirection.Asc)
    {
        var list = (sort ?? Array.Empty<SortCriteria>()).ToList();
        if (!list.Any(s => s.PropertyName.Equals(uniqueKey, StringComparison.OrdinalIgnoreCase)))
            list.Add(new SortCriteria(uniqueKey, dir));
        return list;
    }
}