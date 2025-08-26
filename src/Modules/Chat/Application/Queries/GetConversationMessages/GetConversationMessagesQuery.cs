using Axon.Modules.Chat.Application.Common.Pagination;
using BuildingBlocks.Core.Abstractions.CQRS;

namespace Axon.Modules.Chat.Application.Queries.GetConversationMessages;

/// <summary>
/// Query to retrieve a paginated list of messages for a specific conversation.
/// The conversation must be owned by the authenticated user.
/// Messages are ordered chronologically by sequence number.
/// </summary>
public sealed record GetConversationMessagesQuery(
    Guid ConversationId,
    int PageNumber = 1,
    int PageSize = Page.DefaultSize,
    bool IncludeDeleted = false
) : RequestBase, IQuery<Paged<ConversationMessageItem>>, IAuthenticatedRequest, IPaginatedRequest;