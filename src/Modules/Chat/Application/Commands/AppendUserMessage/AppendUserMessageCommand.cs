// File: /Users/valentynkit/Repos/Axon-Backend/src/Modules/Chat/Application/Commands/AppendUserMessage/AppendUserMessageCommand.cs
using Axon.Modules.Chat.Application.Common;
using Axon.Modules.Chat.Primitives.ValueObjects;
using BuildingBlocks.Core.Abstractions.Idempotency;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Application.Commands.AppendUserMessage;

/// <summary>
/// Appends a user message to an existing conversation. The conversation aggregate
/// maintains the previous response ID for proper AI threading.
/// MCP servers (if any) are resolved by the handler from configuration.
/// This command supports idempotency to prevent duplicate message processing.
/// </summary>
public sealed record AppendUserMessageCommand(
    ConversationId ConversationId,
    MessageContent Content
) : IdempotentCommandBase<ChatMessageResponse>
{
    /// <summary>
    /// Chat operations require a longer idempotency window due to AI processing time.
    /// </summary>
    public override TimeSpan GetIdempotencyWindow() => TimeSpan.FromMinutes(15);
}