// /Axon.Api.Contracts/Chat/ProcessMessageRequest.cs
#nullable enable
using System.ComponentModel.DataAnnotations;

namespace Axon.Api.Contracts.Chat;

/// <summary>
/// Unified request for sending a chat message.
/// - When <see cref="ConversationId"/> is <c>null</c>, a new conversation is started.
/// - When <see cref="ConversationId"/> has a value, the message is appended to that conversation.
/// The domain layer (VOs/rules) performs authoritative validation.
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
    [Required(AllowEmptyStrings = false, ErrorMessage = "Message is required.")]
    public string Message { get; init; } = string.Empty;
}