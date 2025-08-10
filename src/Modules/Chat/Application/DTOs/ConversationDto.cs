using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Application.DTOs;

/// <summary>
/// Data transfer object for conversation details
/// </summary>
public sealed record ConversationDto(
    ConversationId Id,
    string Title,
    string Status,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    int MessageCount,
    IReadOnlyList<MessageDto> Messages);