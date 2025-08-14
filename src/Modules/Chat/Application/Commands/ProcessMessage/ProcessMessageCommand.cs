 
using BuildingBlocks.Core.Functional.Results;
using MediatR;

namespace Axon.Modules.Chat.Application.Commands.ProcessMessage;

/// <summary>
/// Command for processing chat messages with automatic MCP support from configuration
/// </summary>
public sealed record ProcessMessageCommand(
    string Message,
    Guid? ConversationId = null,
    string? UserId = null,
    string? PreviousResponseId = null) : IRequest<Result<ProcessMessageResponse>>;