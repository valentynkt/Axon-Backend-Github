using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Application.Commands.AddMessage;

/// <summary>
/// Response for adding a message to a conversation
/// </summary>
public sealed record AddMessageResponse(
    MessageId MessageId,
    ConversationId ConversationId,
    string Content,
    string Role,
    int Sequence,
    DateTime CreatedAt);