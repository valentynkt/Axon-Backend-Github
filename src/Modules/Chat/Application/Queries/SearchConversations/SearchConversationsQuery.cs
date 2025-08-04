using Axon.Modules.Chat.Application.DTOs;
using Axon.Shared.Common;
using Axon.Shared.Common.Abstractions;

namespace Axon.Modules.Chat.Application.Queries.SearchConversations;

/// <summary>
/// Query to search conversations with filtering and pagination
/// </summary>
public sealed record SearchConversationsQuery(
    string? SearchTerm = null,
    string? Status = null,
    DateTime? CreatedAfter = null,
    DateTime? CreatedBefore = null,
    int Skip = 0,
    int Take = 50) : IRequest<Result<SearchConversationsResponse>>;