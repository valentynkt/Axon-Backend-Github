// /Users/valentynkit/Repos/Axon-Backend/src/Api/Contracts/Chat/ChatTurnRequestDto.cs

/// <summary>
/// One chat turn request.
/// If <see cref="ConversationId"/> is null, a new conversation is started; otherwise the message is appended.
/// </summary>
namespace Axon.Api.Contracts.V1.Chat;

/// <summary>
/// Request DTO for processing a chat turn (API V1)
/// </summary>
public sealed record ChatTurnRequestDto
{
    /// <summary>
    /// The conversation ID to continue, or null to start a new conversation
    /// </summary>
    public Guid? ConversationId { get; init; }

    /// <summary>
    /// The user's message content
    /// </summary>
    public required string Message { get; init; }
}