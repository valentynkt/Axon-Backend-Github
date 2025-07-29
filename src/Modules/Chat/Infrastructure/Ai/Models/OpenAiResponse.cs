using System.Text.Json.Serialization;

namespace Axon.Modules.Chat.Infrastructure.Ai.Models;

/// <summary>
/// OpenAI Responses API response with MCP tool results
/// </summary>
public sealed record OpenAiResponse
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("object")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "OpenAI API field name")]
    public string Object { get; init; } = string.Empty;

    [JsonPropertyName("created")]
    public long Created { get; init; }

    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("choices")]
    public OpenAiChoice[] Choices { get; init; } = [];

    [JsonPropertyName("usage")]
    public OpenAiUsage? Usage { get; init; }

    [JsonPropertyName("mcp_calls")]
    public McpCall[]? McpCalls { get; init; }
}

/// <summary>
/// OpenAI response choice
/// </summary>
public sealed record OpenAiChoice
{
    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("message")]
    public OpenAiMessage Message { get; init; } = new();

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }
}

/// <summary>
/// OpenAI token usage information
/// </summary>
public sealed record OpenAiUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }
}

/// <summary>
/// MCP tool call result
/// </summary>
public sealed record McpCall
{
    [JsonPropertyName("tool_name")]
    public string ToolName { get; init; } = string.Empty;

    [JsonPropertyName("arguments")]
    public string Arguments { get; init; } = string.Empty;

    [JsonPropertyName("result")]
    public string Result { get; init; } = string.Empty;

    [JsonPropertyName("duration_ms")]
    public int DurationMs { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}