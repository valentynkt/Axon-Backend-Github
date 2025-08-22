// /Users/valentynkit/Repos/Axon-Backend/src/Api/Contracts/Chat/ChatTurnRequestDto.cs
#nullable enable
using System.ComponentModel.DataAnnotations;
using Axon.BuildingBlocks.Core.Constants;

namespace Axon.Api.Contracts.Chat;

/// <summary>
/// One chat turn request.
/// If <see cref="ConversationId"/> is null, a new conversation is started; otherwise the message is appended.
/// </summary>
public sealed record class ChatTurnRequestDto
{
    /// <summary>User message (required).</summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Message is required.")]
    [MaxLength(ChatPrimitiveConstants.MessageContentDefault.MaxLength,
        ErrorMessage = "Message cannot exceed characters limit.")]
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Conversation to append to. If null, a new one is started.
    /// </summary>
    public Guid? ConversationId { get; init; }
}