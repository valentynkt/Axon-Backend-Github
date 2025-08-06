using Axon.Modules.Chat.Domain.Conversation.ValueObjects;

namespace Axon.Modules.Chat.Application.Commands.StartConversation;

/// <summary>
/// Response for starting a new conversation
/// </summary>
public sealed record StartConversationResponse(
    ConversationId ConversationId,
    string Title,
    DateTime CreatedAt);