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
        "Pagination.InvalidPageNumber",
        "Page number must be greater than 0");
    
    /// <summary>
    /// Error for invalid page size (less than 1)
    /// </summary>
    public static readonly Error InvalidPageSize = Error.Validation(
        "Pagination.InvalidPageSize",
        "Page size must be greater than 0");
    
    /// <summary>
    /// Error for page size exceeding maximum allowed
    /// </summary>
    public static Error PageSizeExceedsMaximum(int maxSize) => Error.Validation(
        "Pagination.PageSizeExceedsMaximum",
        $"Page size cannot exceed {maxSize} items");
    
    /// <summary>
    /// Error for requesting a page beyond available pages
    /// </summary>
    public static Error PageOutOfRange(int requestedPage, int totalPages) => Error.Validation(
        "Pagination.PageOutOfRange",
        $"Requested page {requestedPage} exceeds total pages {totalPages}");
    
    /// <summary>
    /// Error for invalid filter format
    /// </summary>
    public static Error InvalidFilterFormat(string filter) => Error.Validation(
        "Pagination.InvalidFilterFormat",
        $"Invalid filter format: {filter}");
    
    /// <summary>
    /// Error for invalid sort order format
    /// </summary>
    public static Error InvalidSortFormat(string sortOrder) => Error.Validation(
        "Pagination.InvalidSortFormat",
        $"Invalid sort order format: {sortOrder}");
    
    /// <summary>
    /// Error for unsupported sort field
    /// </summary>
    public static Error UnsupportedSortField(string fieldName) => Error.Validation(
        "Pagination.UnsupportedSortField",
        $"Sort field '{fieldName}' is not supported");
    
    /// <summary>
    /// Error for query timeout during pagination
    /// </summary>
    public static readonly Error QueryTimeout = Error.Timeout(
        "Pagination.QueryTimeout",
        "The pagination query exceeded the allowed execution time");
    
    /// <summary>
    /// Error for database connection issues during pagination
    /// </summary>
    public static readonly Error DatabaseConnectionError = Error.Internal(
        "Pagination.DatabaseConnectionError",
        "Database connection failed during pagination query");
}

