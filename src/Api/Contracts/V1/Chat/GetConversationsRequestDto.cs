using BuildingBlocks.Web.Contracts;
using FastEndpoints;

namespace Axon.Api.Contracts.V1.Chat;

/// <summary>
/// Request DTO for retrieving paginated conversation list with filtering and sorting options.
/// All parameters are optional with sensible defaults applied by the mapping layer.
/// </summary>

public sealed record GetConversationsRequestDto : BasePagedRequest
{
    /// <summary>
    /// The field to sort by. Valid values: "UpdatedAt", "CreatedAt", "Title".
    /// Defaults to "UpdatedAt" if not specified or invalid.
    /// </summary>
    [QueryParam]
    public string? SortBy { get; init; }

    /// <summary>
    /// The sort direction. Valid values: "Asc", "Desc".
    /// Defaults to "Desc" if not specified or invalid.
    /// </summary>
    [QueryParam]
    public string? SortDirection { get; init; }

    /// <summary>
    /// Filter conversations by title containing this text (case-insensitive).
    /// If null or empty, no title filtering is applied.
    /// </summary>
    [QueryParam]
    public string? TitleContains { get; init; }
}