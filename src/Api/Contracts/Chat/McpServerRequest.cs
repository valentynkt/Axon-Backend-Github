namespace Axon.Api.Contracts.Chat;

/// <summary>
/// MCP server configuration in API request
/// </summary>
public sealed record McpServerRequest(
    string ServerUrl,
    string? ServerLabel = null,
    Dictionary<string, string>? Headers = null,
    string[]? AllowedTools = null);