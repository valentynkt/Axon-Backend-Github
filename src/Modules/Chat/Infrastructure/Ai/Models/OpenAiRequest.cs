using System.Text.Json.Serialization;

namespace Axon.Modules.Chat.Infrastructure.Ai.Models;

/// <summary>
/// OpenAI Responses API request with MCP support
/// </summary>
public sealed record OpenAiRequest
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = "gpt-4o";

    [JsonPropertyName("messages")]
    public OpenAiMessage[] Messages { get; init; } = [];

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; init; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; init; }

    [JsonPropertyName("tools")]
    public McpTool? Tools { get; init; }

    [JsonPropertyName("previous_response_id")]
    public string? PreviousResponseId { get; init; }
}

/// <summary>
/// OpenAI message in conversation
/// </summary>
public sealed record OpenAiMessage
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = "user";

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    public static OpenAiMessage User(string content) => new() { Role = "user", Content = content };
    public static OpenAiMessage Assistant(string content) => new() { Role = "assistant", Content = content };
    public static OpenAiMessage System(string content) => new() { Role = "system", Content = content };
}