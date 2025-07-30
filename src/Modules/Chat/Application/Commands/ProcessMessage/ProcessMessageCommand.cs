using Axon.Shared.Common;
using MediatR;

namespace Axon.Modules.Chat.Application.Commands.ProcessMessage;

/// <summary>
/// Command for processing chat messages with automatic MCP support from configuration
/// </summary>
public sealed record ProcessMessageCommand(
    string Message,
    string? PreviousResponseId = null) : IRequest<Result<ProcessMessageResponse>>;