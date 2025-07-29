namespace Axon.Modules.Chat.Application.DTOs;

/// <summary>
/// MCP server configuration for direct tool access
/// </summary>
public sealed record McpServerConfig(
    string ServerUrl,
    string ServerLabel,
    Dictionary<string, string>? Headers = null,
    string[]? AllowedTools = null,
    bool RequireApproval = false);