// /Users/valentynkit/Repos/Axon-Backend/src/Modules/Chat/Application/Commands/AppendUserMessage/AppendUserMessageCommand.cs
#nullable enable
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.Common.Commands;
using Axon.Modules.Chat.Application.DTOs.Responses;
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
) : ChatIdempotentCommand<ProcessMessageResponse>;