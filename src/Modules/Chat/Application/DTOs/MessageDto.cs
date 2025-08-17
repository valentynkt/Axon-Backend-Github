namespace Axon.Modules.Chat.Application.DTOs;

/// <summary>
/// Data transfer object for message details
/// </summary>
public sealed record MessageDto(
    MessageId Id,
    ConversationId ConversationId,
    string Content,
    string Role,
    int Sequence,
    DateTime CreatedAt,
    Dictionary<string, object>? Metadata = null);