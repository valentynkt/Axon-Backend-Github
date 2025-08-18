using BuildingBlocks.Core.Abstractions.CQRS;

namespace Axon.Modules.Chat.Application.Commands.ProcessMessage;

/// <summary>
/// Command to process a chat message with MCP support
/// </summary>
public sealed record ProcessMessageCommand(
    string Message,
    Guid? ConversationId,
    Guid? UserId,
    string? PreviousResponseId) : CommandBase<ProcessMessageResponse>;