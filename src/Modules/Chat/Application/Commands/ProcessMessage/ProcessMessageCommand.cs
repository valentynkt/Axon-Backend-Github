using Axon.Shared.Common;
using MediatR;

namespace Axon.Modules.Chat.Application.Commands.ProcessMessage;

/// <summary>
/// Command for processing chat messages with direct MCP support
/// </summary>
public sealed record ProcessMessageCommand(
    string Message,
    string? McpServerUrl = null,
    Dictionary<string, string>? McpHeaders = null,
    string[]? AllowedTools = null,
    string? PreviousResponseId = null) : IRequest<Result<ProcessMessageResponse>>;