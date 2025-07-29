namespace Axon.Modules.Chat.Application.DTOs;

/// <summary>
/// AI processing request with MCP server configuration
/// </summary>
public sealed record AiRequest(
    string Message,
    McpServerConfig? McpConfig = null,
    string? PreviousResponseId = null);