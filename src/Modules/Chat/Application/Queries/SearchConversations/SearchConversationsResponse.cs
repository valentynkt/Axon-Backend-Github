using Axon.Modules.Chat.Application.DTOs;

namespace Axon.Modules.Chat.Application.Queries.SearchConversations;

/// <summary>
/// Response for search conversations query
/// </summary>
public sealed record SearchConversationsResponse(
    IReadOnlyList<ConversationSummaryDto> Conversations,
    int TotalCount,
    int Skip,
    int Take,
    bool HasNextPage,
    string? SearchTerm);