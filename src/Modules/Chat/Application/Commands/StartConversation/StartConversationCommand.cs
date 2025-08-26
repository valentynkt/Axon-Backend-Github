// /Users/valentynkit/Repos/Axon-Backend/src/Modules/Chat/Application/Commands/StartConversation/StartConversationCommand.cs
#nullable enable
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.Common.Commands;
using Axon.Modules.Chat.Application.DTOs.Responses;

namespace Axon.Modules.Chat.Application.Commands.StartConversation;

/// <summary>
/// Starts a new conversation, appends the user's first message, calls the AI,
/// and appends the assistant's reply. Returns a unified ChatMessageResponse.
/// </summary>
public sealed record StartConversationCommand(
    MessageContent Message
) : ChatIdempotentCommand<ProcessMessageResponse>;