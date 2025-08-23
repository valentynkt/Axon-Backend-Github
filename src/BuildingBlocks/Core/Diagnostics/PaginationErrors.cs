using BuildingBlocks.Core.Diagnostics.Errors;

namespace BuildingBlocks.Core.Diagnostics;

/// <summary>
/// Static class containing standardized pagination error definitions
/// Follows Clean Architecture error handling patterns
/// </summary>
public static class PaginationErrors
{
    /// <summary>
    /// Error for invalid page number (less than 1)
    /// </summary>
    public static readonly Error InvalidPageNumber = Error.Validation(
        "Page number must be greater than 0",
        "Pagination.InvalidPageNumber");
    
    /// <summary>
    /// Error for invalid page size (less than 1)
    /// </summary>
    public static readonly Error InvalidPageSize = Error.Validation(
        "Page size must be greater than 0",
        "Pagination.InvalidPageSize");
    
    /// <summary>
    /// Error for page size exceeding maximum allowed
    /// </summary>
    public static Error PageSizeExceedsMaximum(int maxSize) => Error.Validation(
        $"Page size cannot exceed {maxSize} items",
        "Pagination.PageSizeExceedsMaximum");
    
    /// <summary>
    /// Error for requesting a page beyond available pages
    /// </summary>
    public static Error PageOutOfRange(int requestedPage, int totalPages) => Error.Validation(
        $"Requested page {requestedPage} exceeds total pages {totalPages}",
        "Pagination.PageOutOfRange");
    
    /// <summary>
    /// Error for invalid filter format
    /// </summary>
    public static Error InvalidFilterFormat(string filter) => Error.Validation(
        $"Invalid filter format: {filter}",
        "Pagination.InvalidFilterFormat");
    
    /// <summary>
    /// Error for invalid sort order format
    /// </summary>
    public static Error InvalidSortFormat(string sortOrder) => Error.Validation(
        $"Invalid sort order format: {sortOrder}",
        "Pagination.InvalidSortFormat");
    
    /// <summary>
    /// Error for unsupported sort field
    /// </summary>
    public static Error UnsupportedSortField(string fieldName) => Error.Validation(
        $"Sort field '{fieldName}' is not supported",
        "Pagination.UnsupportedSortField");
    
    /// <summary>
    /// Error for query timeout during pagination
    /// </summary>
    public static readonly Error QueryTimeout = Error.Timeout(
        "The pagination query exceeded the allowed execution time",
        "Pagination.QueryTimeout");
    
    /// <summary>
    /// Error for database connection issues during pagination
    /// </summary>
    public static readonly Error DatabaseConnectionError = Error.Internal(
        "Database connection failed during pagination query",
        "Pagination.DatabaseConnectionError");
}

