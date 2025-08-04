using Axon.Modules.Chat.Application.DTOs;

namespace Axon.Modules.Chat.Application.Queries.GetConversationHistory;

/// <summary>
/// Response for conversation history query
/// </summary>
public sealed record GetConversationHistoryResponse(
    IReadOnlyList<ConversationSummaryDto> Conversations,
    int TotalCount,
    int Skip,
    int Take,
    bool HasNextPage);