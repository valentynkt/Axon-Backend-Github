using BuildingBlocks.Core.Abstractions.CQRS;

namespace Axon.Modules.Chat.Application.Queries.GetAllConversationIds;

/// <summary>
/// Query to retrieve all conversation IDs for the current user
/// </summary>
public sealed record GetAllConversationIdsQuery() : QueryBase<GetAllConversationIdsResponse>;