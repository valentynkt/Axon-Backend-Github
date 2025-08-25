namespace Axon.Api.Contracts.V1.Chat;

/// <summary>
/// Request DTO for retrieving paginated messages from a conversation.
/// </summary>
public sealed record GetConversationMessagesRequestDto
{
    /// <summary>
    /// The unique identifier of the conversation.
    /// This parameter is required and bound from the route parameter.
    /// </summary>
    public Guid ConversationId { get; init; }
    
    /// <summary>
    /// The page number (1-based). Defaults to 1 if not specified.
    /// </summary>
    public int? PageNumber { get; init; }
    
    /// <summary>
    /// The number of messages per page. Defaults to system default if not specified.
    /// </summary>
    public int? PageSize { get; init; }
    
    /// <summary>
    /// Whether to include soft-deleted messages in the results.
    /// Defaults to false if not specified.
    /// </summary>
    public bool? IncludeDeleted { get; init; }
}