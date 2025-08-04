using Axon.Modules.Chat.Application.DTOs;
using Axon.Shared.Common;
using Axon.Shared.Common.Abstractions;

namespace Axon.Modules.Chat.Application.Queries.GetConversationHistory;

/// <summary>
/// Query to get conversation history with pagination
/// </summary>
public sealed record GetConversationHistoryQuery(
    int Skip = 0,
    int Take = 50,
    string? Status = null) : IRequest<Result<GetConversationHistoryResponse>>;