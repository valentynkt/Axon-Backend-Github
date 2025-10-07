using System.Text.Json.Serialization;

namespace Axon.Modules.Chat.Infrastructure.ExternalServices.AI.OpenAI.Models;

/// <summary>
/// Request payload for OpenAI Responses API
/// Spec: https://platform.openai.com/docs/api-reference/responses
/// </summary>
public sealed record OpenAiResponsesRequest
{
    /// <summary>
    /// ID of the model to use (e.g., "gpt-4o", "gpt-4o-mini")
    /// </summary>
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    /// <summary>
    /// The user input text to process
    /// </summary>
    [JsonPropertyName("input")]
    public required string Input { get; init; }

    /// <summary>
    /// Array of tools (including MCP servers) available to the model
    /// </summary>
    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public McpToolDefinition[]? Tools { get; init; }

    /// <summary>
    /// ID of the previous response to continue the conversation
    /// </summary>
    [JsonPropertyName("previous_response_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PreviousResponseId { get; init; }

    /// <summary>
    /// Maximum number of tokens to generate in the response
    /// </summary>
    [JsonPropertyName("max_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxTokens { get; init; }
}
