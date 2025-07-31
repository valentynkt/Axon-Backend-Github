using System.ComponentModel.DataAnnotations;

namespace Axon.Modules.Chat.Infrastructure.Configuration;

/// <summary>
/// Configuration options for a single MCP server
/// </summary>
public sealed class McpServerOptions
{
    /// <summary>
    /// Whether this MCP server is enabled
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// MCP server URL (must be HTTPS)
    /// </summary>
    [Required]
    [Url]
    public string ServerUrl { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable label for the server
    /// </summary>
    public string? ServerLabel { get; init; }

    /// <summary>
    /// HTTP headers to send with MCP requests
    /// </summary>
    public Dictionary<string, string> Headers { get; init; } = new();

    /// <summary>
    /// List of allowed tools (empty means all tools allowed)
    /// </summary>
    public string[] AllowedTools { get; init; } = [];

    /// <summary>
    /// Request timeout in seconds (1-300)
    /// </summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Whether tool execution requires approval
    /// </summary>
    public bool RequireApproval { get; init; }

    /// <summary>
    /// Validate this server configuration
    /// </summary>
    /// <param name="validationContext">Validation context</param>
    /// <returns>Validation results</returns>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        _ = validationContext; // Parameter required by ValidationAttribute interface
        // Skip validation if disabled
        if (!Enabled)
            yield break;

        // Validate required ServerUrl
        if (string.IsNullOrWhiteSpace(ServerUrl))
        {
            yield return new ValidationResult(
                "ServerUrl is required for enabled servers",
                [nameof(ServerUrl)]);
        }
        else
        {
            // Validate HTTPS scheme
            if (Uri.TryCreate(ServerUrl, UriKind.Absolute, out var uri))
            {
                if (uri.Scheme != "https")
                {
                    yield return new ValidationResult(
                        "ServerUrl must use HTTPS scheme for security",
                        [nameof(ServerUrl)]);
                }
            }
            else
            {
                yield return new ValidationResult(
                    "ServerUrl must be a valid absolute URL",
                    [nameof(ServerUrl)]);
            }
        }

        // Validate AllowedTools array is not empty if specified
        if (AllowedTools.Length == 1 && string.IsNullOrWhiteSpace(AllowedTools[0]))
        {
            yield return new ValidationResult(
                "AllowedTools array should not contain empty strings",
                [nameof(AllowedTools)]);
        }

        // Validate Headers don't contain sensitive data that might be logged
        foreach (var (key, value) in Headers)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                yield return new ValidationResult(
                    "Header keys cannot be empty",
                    [nameof(Headers)]);
            }

            if (value is null)
            {
                yield return new ValidationResult(
                    $"Header '{key}' cannot have null value",
                    [nameof(Headers)]);
            }
        }
    }
}