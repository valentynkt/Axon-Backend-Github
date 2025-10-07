using System.Text.Json.Serialization;

namespace Axon.Modules.Chat.Infrastructure.ExternalServices.AI.OpenAI.Models;

/// <summary>
/// MCP tool definition for OpenAI Responses API
/// Specifies a remote MCP server that OpenAI can call directly
/// </summary>
public sealed record McpToolDefinition
{
    /// <summary>
    /// Tool type - must be "mcp" for Model Context Protocol servers
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "mcp";

    /// <summary>
    /// Base URL of the MCP server (OpenAI appends /tools/list and /tools/call)
    /// </summary>
    [JsonPropertyName("server_url")]
    public required string ServerUrl { get; init; }

    /// <summary>
    /// Human-readable label for the server
    /// </summary>
    [JsonPropertyName("server_label")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ServerLabel { get; init; }

    /// <summary>
    /// Approval policy: "always", "never", or policy object
    /// </summary>
    [JsonPropertyName("require_approval")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RequireApproval { get; init; }

    /// <summary>
    /// HTTP headers to forward to the MCP server (e.g., Authorization)
    /// </summary>
    [JsonPropertyName("headers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Headers { get; init; }

    /// <summary>
    /// List of allowed tools (empty/null means all tools allowed)
    /// </summary>
    [JsonPropertyName("allowed_tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? AllowedTools { get; init; }
}
