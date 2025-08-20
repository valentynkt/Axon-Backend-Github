using Axon.Modules.Chat.Application.Common;
using BuildingBlocks.Core.Abstractions.Idempotency;

namespace Axon.Modules.Chat.Application.Commands.StartConversation;

/// <summary>
/// Starts a new conversation, appends the user's first message, calls the AI,
/// and appends the assistant's reply. Returns a unified ChatMessageResponse.
/// </summary>
public sealed record StartConversationCommand(
    MessageContent Message
) : IdempotentCommandBase<ChatMessageResponse>
{
    /// <summary>
    /// Chat operations require a longer idempotency window due to AI processing time.
    /// </summary>
    public override TimeSpan GetIdempotencyWindow() => TimeSpan.FromMinutes(15);
}