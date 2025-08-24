namespace Axon.Modules.Chat.Application.Common.Sorting;

/// <summary>
/// Defines the direction of sorting for query results.
/// </summary>
public enum SortDirection
{
    /// <summary>
    /// Sort in ascending order (A-Z, 0-9, oldest first).
    /// </summary>
    Asc,

    /// <summary>
    /// Sort in descending order (Z-A, 9-0, newest first).
    /// </summary>
    Desc
}