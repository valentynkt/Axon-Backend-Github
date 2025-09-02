using BuildingBlocks.Application.Pagination;
using Axon.Modules.Chat.Application.Common.Queries;
using Axon.Modules.Chat.Application.Common.Sorting;
using BuildingBlocks.Core.Abstractions.CQRS;

namespace Axon.Modules.Chat.Application.Queries.GetConversations;

/// <summary>
/// Query to retrieve a paginated list of conversations for the authenticated user.
/// Supports sorting and optional title filtering.
/// </summary>
public sealed record GetConversationsQuery(
    int PageNumber = 1,
    int PageSize = Page.DefaultSize,
    ConversationSortBy SortBy = ConversationSortBy.UpdatedAt,
    SortDirection SortDirection = SortDirection.Desc,
    string? TitleContains = null
) : ChatBaseQuery<Paged<ConversationListItem>>, IPaginatedRequest;