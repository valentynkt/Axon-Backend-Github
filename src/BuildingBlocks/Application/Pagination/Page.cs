#nullable enable

using BuildingBlocks.Core.Diagnostics;
using CSharpFunctionalExtensions;

namespace BuildingBlocks.Application.Pagination;

/// <summary>
/// Represents a page for pagination with number and size.
/// Provides validation and utility methods for safe paging operations.
/// </summary>
public readonly record struct Page(int Number, int Size)
{
    /// <summary>
    /// Default page size when none is specified.
    /// </summary>
    public const int DefaultSize = 20;
    
    /// <summary>
    /// Maximum allowed page size to prevent performance issues.
    /// </summary>
    public const int MaxSize = 100;

    /// <summary>
    /// Calculates the number of items to skip for this page.
    /// </summary>
    public int Skip => (Number - 1) * Size;

    /// <summary>
    /// Sanitizes page parameters and returns a valid Page instance.
    /// Ensures page number is at least 1 and size doesn't exceed maximum.
    /// </summary>
    /// <param name="pageNumber">The requested page number</param>
    /// <param name="pageSize">The requested page size</param>
    /// <param name="maxSize">The maximum allowed page size (defaults to MaxSize)</param>
    /// <returns>A Result containing a valid Page or validation error</returns>
    public static Result<Page, Error> Sanitize(int pageNumber, int pageSize, int maxSize = MaxSize)
    {
        if (pageNumber < 1)
            return Result.Failure<Page, Error>(PaginationErrors.InvalidPageNumber);
        
        if (pageSize < 1)
            return Result.Failure<Page, Error>(PaginationErrors.InvalidPageSize);
        
        if (pageSize > maxSize)
            return Result.Failure<Page, Error>(PaginationErrors.PageSizeExceedsMaximum(maxSize));

        return Result.Success<Page, Error>(new Page(pageNumber, pageSize));
    }

    /// <summary>
    /// Creates a default first page with default size.
    /// </summary>
    public static Page Default => new(1, DefaultSize);

    /// <summary>
    /// Returns a string representation of the page.
    /// </summary>
    public override string ToString() => $"Page {Number} (Size: {Size})";
}