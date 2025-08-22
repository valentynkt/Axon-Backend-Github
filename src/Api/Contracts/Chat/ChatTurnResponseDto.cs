// File: /Users/valentynkit/Repos/Axon-Backend/src/Api/Contracts/Chat/ProcessMessageResponse.cs
namespace Axon.Api.Contracts.Chat;

/// <summary>
/// Assistant reply for a processed chat turn.
/// Pure transport DTO (primitives only).
/// </summary>
public sealed record ChatTurnResponseDto(
    Guid ConversationId,
    Guid UserMessageId,
    Guid AssistantMessageId,
    string AssistantMessage
);