namespace Axon.Modules.Chat.Application.Queries.GetConversation;

/// <summary>
/// Response containing full conversation with all messages
/// </summary>
public sealed record GetConversationResponse(
    Guid ConversationId,
    string Title,
    string Status,              // "Active" | "Completed"
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt,
    int MessageCount,
    IReadOnlyList<GetConversationMessageDto> Messages);

/// <summary>
/// Individual message within a conversation
/// </summary>
public sealed record GetConversationMessageDto(
    Guid MessageId,
    string Role,                // "user" | "assistant"
    string Content,
    int Sequence,
    DateTimeOffset CreatedAt);