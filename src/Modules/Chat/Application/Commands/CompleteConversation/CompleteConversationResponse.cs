using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Application.Commands.CompleteConversation;

/// <summary>
/// Response for completing a conversation
/// </summary>
public sealed record CompleteConversationResponse(
    ConversationId ConversationId,
    string Status,
    DateTime CompletedAt,
    int TotalMessages);