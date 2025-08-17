using BuildingBlocks.Core.Functional.Results;

namespace Axon.Modules.Chat.Application.Queries.GetConversation;

/// <summary>
/// Query to retrieve a full conversation with all messages
/// </summary>
public sealed record GetConversationQuery(Guid ConversationId) : IRequest<Result<GetConversationResponse>>;