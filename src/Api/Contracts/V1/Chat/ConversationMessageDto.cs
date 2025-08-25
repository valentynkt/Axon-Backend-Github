namespace Axon.Api.Contracts.V1.Chat;

/// <summary>
/// Represents a single message within a conversation.
/// </summary>
public sealed record ConversationMessageDto(
    /// <summary>
    /// The unique identifier of the message.
    /// </summary>
    Guid MessageId,
    
    /// <summary>
    /// The role of the message sender. Valid values: "user", "assistant".
    /// </summary>
    string Role,
    
    /// <summary>
    /// The content of the message.
    /// </summary>
    string Content,
    
    /// <summary>
    /// The UTC timestamp when the message was created.
    /// </summary>
    DateTime CreatedAtUtc,
    
    /// <summary>
    /// The sequence number of the message within the conversation.
    /// Messages are ordered by sequence in ascending order.
    /// </summary>
    int Sequence,
    
    /// <summary>
    /// The optional identifier of the AI response associated with this message.
    /// Only present for assistant messages that have AI response tracking.
    /// </summary>
    string? AiResponseId
);