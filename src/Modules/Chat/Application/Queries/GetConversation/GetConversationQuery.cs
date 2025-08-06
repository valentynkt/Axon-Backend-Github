using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using Axon.Shared.Common;
using Axon.Shared.Common.Abstractions;

namespace Axon.Modules.Chat.Application.Queries.GetConversation;

/// <summary>
/// Query to get a conversation with all its messages
/// </summary>
public sealed record GetConversationQuery(
    ConversationId ConversationId) : IRequest<Result<ConversationDto>>;