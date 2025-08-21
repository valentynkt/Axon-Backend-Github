// /Users/valentynkit/Repos/Axon-Backend/src/Modules/Chat/Application/Commands/AppendUserMessage/AppendUserMessageCommand.cs
#nullable enable
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.Common;
using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Core.Abstractions.Idempotency;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Application.Commands.AppendUserMessage;

/// <summary>
/// Appends a user message to an existing conversation. The conversation aggregate
/// maintains the previous response ID for proper AI threading.
/// This command is idempotent to prevent duplicate processing.
/// </summary>
public sealed record AppendUserMessageCommand(
    ConversationId ConversationId,
    MessageContent Content
) : RequestBase, IIdempotentCommand<ProcessMessageResponse>
{
    /// <summary>Chat operations require a longer idempotency window due to AI processing time.</summary>
    public TimeSpan? GetIdempotencyWindow() => TimeSpan.FromMinutes(15);

    // Optional overrides (keep defaults unless you need them)
    // public string? GetExplicitIdempotencyKey() => null;
    // public bool CacheFailures => false;
}