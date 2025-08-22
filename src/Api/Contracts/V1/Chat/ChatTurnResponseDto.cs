namespace Axon.Api.Contracts.V1.Chat;

/// <summary>
/// Response DTO for a processed chat turn (API V1)
/// </summary>
public sealed record ChatTurnResponseDto
{
    /// <summary>
    /// The conversation ID (new or existing)
    /// </summary>
    public required Guid ConversationId { get; init; }

    /// <summary>
    /// The ID of the user's message
    /// </summary>
    public required Guid UserMessageId { get; init; }

    /// <summary>
    /// The ID of the assistant's message
    /// </summary>
    public required Guid AssistantMessageId { get; init; }

    /// <summary>
    /// The assistant's response message
    /// </summary>
    public required string AssistantMessage { get; init; }

    /// <summary>
    /// The timestamp when the response was generated
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}