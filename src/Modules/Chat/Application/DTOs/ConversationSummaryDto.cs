using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using Axon.Modules.Chat.Domain.ValueObjects;

namespace Axon.Modules.Chat.Application.DTOs;

/// <summary>
/// Data transfer object for conversation summary without messages
/// </summary>
public sealed record ConversationSummaryDto(
    ConversationId Id,
    string Title,
    string Status,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    int MessageCount,
    string? LastMessageContent = null,
    DateTime? LastMessageAt = null);