using System.Text.Json.Serialization;

namespace Axon.Modules.Chat.Infrastructure.ExternalServices.AI.OpenAI.Models;

/// <summary>
/// Response from OpenAI Responses API
/// </summary>
public sealed record OpenAiResponsesResponse
{
    /// <summary>
    /// Unique identifier for this response
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// The complete output text (preferred field for simple text responses)
    /// </summary>
    [JsonPropertyName("output_text")]
    public string? OutputText { get; init; }

    /// <summary>
    /// Structured output array (fallback if output_text is not present)
    /// </summary>
    [JsonPropertyName("output")]
    public OutputMessage[]? Output { get; init; }

    /// <summary>
    /// Model that generated the response
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    /// <summary>
    /// Token usage statistics
    /// </summary>
    [JsonPropertyName("usage")]
    public TokenUsage? Usage { get; init; }
}

/// <summary>
/// Output message in structured format
/// </summary>
public sealed record OutputMessage
{
    /// <summary>
    /// Message type (e.g., "message", "tool_call")
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>
    /// Content blocks within the message
    /// </summary>
    [JsonPropertyName("content")]
    public ContentBlock[]? Content { get; init; }

    /// <summary>
    /// Role of the message sender (e.g., "assistant")
    /// </summary>
    [JsonPropertyName("role")]
    public string? Role { get; init; }
}

/// <summary>
/// Content block within an output message
/// </summary>
public sealed record ContentBlock
{
    /// <summary>
    /// Content type (e.g., "text")
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>
    /// Text content
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }
}

/// <summary>
/// Token usage statistics
/// </summary>
public sealed record TokenUsage
{
    /// <summary>
    /// Number of tokens in the prompt
    /// </summary>
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }

    /// <summary>
    /// Number of tokens in the completion
    /// </summary>
    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; init; }

    /// <summary>
    /// Total tokens used (prompt + completion)
    /// </summary>
    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }
}
