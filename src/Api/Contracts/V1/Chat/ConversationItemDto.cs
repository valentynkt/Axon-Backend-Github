namespace Axon.Api.Contracts.V1.Chat;

/// <summary>
/// Represents a conversation item in the paginated list response.
/// Contains essential conversation metadata for list display.
/// </summary>
public sealed record ConversationItemDto(
    /// <summary>
    /// The unique identifier of the conversation.
    /// </summary>
    Guid ConversationId,
    
    /// <summary>
    /// The title/subject of the conversation.
    /// </summary>
    string Title,
    
    /// <summary>
    /// When the conversation was originally created (UTC).
    /// </summary>
    DateTime CreatedAtUtc,
    
    /// <summary>
    /// When the conversation was last updated/modified (UTC).
    /// </summary>
    DateTime UpdatedAtUtc,
    
    /// <summary>
    /// The ID of the last assistant response message, if any.
    /// Used for navigation and conversation state tracking.
    /// </summary>
    string? LastAssistantResponseId
);