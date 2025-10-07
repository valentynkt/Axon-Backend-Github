using System.Text.Json.Serialization;

namespace Axon.Modules.Chat.Infrastructure.ExternalServices.AI.OpenAI.Models;

/// <summary>
/// Error response from OpenAI API
/// </summary>
public sealed record OpenAiErrorResponse
{
    /// <summary>
    /// Error details
    /// </summary>
    [JsonPropertyName("error")]
    public ErrorDetail? Error { get; init; }
}

/// <summary>
/// Detailed error information
/// </summary>
public sealed record ErrorDetail
{
    /// <summary>
    /// Error code (e.g., "invalid_request_error", "rate_limit_exceeded")
    /// </summary>
    [JsonPropertyName("code")]
    public string? Code { get; init; }

    /// <summary>
    /// Human-readable error message
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>
    /// Error type
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>
    /// Parameter that caused the error
    /// </summary>
    [JsonPropertyName("param")]
    public string? Param { get; init; }

    /// <summary>
    /// Creates a fallback error detail for unknown errors
    /// </summary>
    public static ErrorDetail Unknown() => new() { Code = "unknown", Message = "Unknown error" };

    /// <summary>
    /// Creates a fallback error detail for parse failures
    /// </summary>
    public static ErrorDetail ParseFailure() => new() { Code = "parse_error", Message = "Failed to parse error response" };
}
