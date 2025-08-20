// File: /Users/valentynkit/Repos/Axon-Backend/src/Api/Contracts/Chat/ProcessMessageRequest.cs
using System.ComponentModel.DataAnnotations;
using Axon.BuildingBlocks.Core.Constants;
using Axon.Modules.Chat.Primitives.Constants;

namespace Axon.Api.Contracts.Chat;

/// <summary>
/// One chat turn. If <see cref="ConversationId"/> is null, starts a new conversation;
/// otherwise appends to the existing conversation.
/// </summary>
public sealed class ProcessMessageRequest
{
    /// <summary>User message (required).</summary>
    [Required, MinLength(1), MaxLength(ChatPrimitiveConstants.MessageContent.MaxLength)]
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// The conversation ID to append to. If null, a new conversation starts.
    /// </summary>
    public Guid? ConversationId { get; init; }

    /// <summary>Enable configured MCP servers (optional).</summary>
    public bool UseMcpServers { get; init; }
}