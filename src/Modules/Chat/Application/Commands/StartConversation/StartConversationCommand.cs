// /Users/valentynkit/Repos/Axon-Backend/src/Modules/Chat/Application/Commands/StartConversation/StartConversationCommand.cs
#nullable enable
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.DTOs.Responses;
using BuildingBlocks.Core.Abstractions.CQRS;           // RequestBase
using BuildingBlocks.Core.Abstractions.Idempotency;    // IIdempotentCommand<T>

namespace Axon.Modules.Chat.Application.Commands.StartConversation;

/// <summary>
/// Starts a new conversation, appends the user's first message, calls the AI,
/// and appends the assistant's reply. Returns a unified ChatMessageResponse.
/// </summary>
public sealed record StartConversationCommand(
    MessageContent Message
) : RequestBase, IIdempotentCommand<ProcessMessageResponse>
{
    /// <summary>
    /// Chat operations require a longer idempotency window due to AI processing time.
    /// </summary>
    public TimeSpan? GetIdempotencyWindow() => TimeSpan.FromMinutes(15);

    // Optional interface members (keep defaults unless needed)
    // public string? GetExplicitIdempotencyKey() => null;
    // public bool CacheFailures => false;
}