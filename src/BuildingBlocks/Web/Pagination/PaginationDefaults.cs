namespace Axon.BuildingBlocks.Web.Pagination;

/// <summary>
/// Defines default values and constraints for pagination in API endpoints.
/// Centralizes pagination configuration to ensure consistency across the application.
/// </summary>
public static class PaginationDefaults
{
    /// <summary>
    /// Default page number when none is specified (1-based).
    /// </summary>
    public const int DefaultPageNumber = 1;

    /// <summary>
    /// Default number of items per page when none is specified.
    /// </summary>
    public const int DefaultPageSize = 20;

    /// <summary>
    /// Maximum allowed page size to prevent performance issues.
    /// </summary>
    public const int MaxPageSize = 100;

    /// <summary>
    /// Minimum allowed page number.
    /// </summary>
    public const int MinPageNumber = 1;

    /// <summary>
    /// Minimum allowed page size.
    /// </summary>
    public const int MinPageSize = 1;

    /// <summary>
    /// Applies default values to nullable pagination parameters.
    /// </summary>
    /// <param name="pageNumber">The page number to default</param>
    /// <param name="pageSize">The page size to default</param>
    /// <returns>A tuple with defaulted values</returns>
    public static (int PageNumber, int PageSize) ApplyDefaults(int? pageNumber, int? pageSize)
    {
        return (
            pageNumber ?? DefaultPageNumber,
            pageSize ?? DefaultPageSize
        );
    }
}