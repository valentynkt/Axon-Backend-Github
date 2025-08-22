// /Modules/Chat/Application/Common/ProcessMessageRequest.cs
#nullable enable

namespace Axon.Modules.Chat.Application.Common;

/// <summary>
/// Unified request for sending a chat message from the application layer.
/// - When <see cref="ConversationId"/> is <c>null</c>, a new conversation is started.
/// - When <see cref="ConversationId"/> has a value, the message is appended.
/// </summary>
public sealed record ProcessMessageRequest
{
    /// <summary>
    /// Target conversation. <c>null</c> → start a new conversation.
    /// </summary>
    public Guid? ConversationId { get; init; }

    /// <summary>
    /// User message to process. Must be non-empty after trimming.
    /// </summary>
    public string Message { get; init; } = string.Empty;
}