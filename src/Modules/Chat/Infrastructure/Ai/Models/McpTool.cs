using System.Text.Json.Serialization;

namespace Axon.Modules.Chat.Infrastructure.Ai.Models;

/// <summary>
/// MCP tool configuration for OpenAI Responses API
/// </summary>
public sealed record McpTool
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "mcp";

    [JsonPropertyName("server_url")]
    public string ServerUrl { get; init; } = string.Empty;

    [JsonPropertyName("server_label")]
    public string? ServerLabel { get; init; }

    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Headers { get; init; }

    [JsonPropertyName("allowed_tools")]
    public string[]? AllowedTools { get; init; }

    [JsonPropertyName("require_approval")]
    public bool RequireApproval { get; init; } = false;

    public static McpTool FromConfig(string serverUrl, string? serverLabel = null, Dictionary<string, string>? headers = null, string[]? allowedTools = null, bool requireApproval = false) =>
        new()
        {
            ServerUrl = serverUrl,
            ServerLabel = serverLabel,
            Headers = headers,
            AllowedTools = allowedTools,
            RequireApproval = requireApproval
        };
}