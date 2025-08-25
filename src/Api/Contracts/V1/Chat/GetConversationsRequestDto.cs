namespace Axon.Api.Contracts.V1.Chat;

/// <summary>
/// Request DTO for retrieving paginated conversation list with filtering and sorting options.
/// All parameters are optional with sensible defaults applied by the mapping layer.
/// </summary>
public sealed record GetConversationsRequestDto
{
    /// <summary>
    /// The page number (1-based). Defaults to 1 if not specified.
    /// </summary>
    public int? PageNumber { get; init; }

    /// <summary>
    /// The number of conversations per page. Defaults to system default if not specified.
    /// </summary>
    public int? PageSize { get; init; }

    /// <summary>
    /// The field to sort by. Valid values: "UpdatedAt", "CreatedAt", "Title".
    /// Defaults to "UpdatedAt" if not specified or invalid.
    /// </summary>
    public string? SortBy { get; init; }

    /// <summary>
    /// The sort direction. Valid values: "Asc", "Desc".
    /// Defaults to "Desc" if not specified or invalid.
    /// </summary>
    public string? SortDirection { get; init; }

    /// <summary>
    /// Filter conversations by title containing this text (case-insensitive).
    /// If null or empty, no title filtering is applied.
    /// </summary>
    public string? TitleContains { get; init; }
}