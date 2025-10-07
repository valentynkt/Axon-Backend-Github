using System.ComponentModel.DataAnnotations;

namespace Axon.Modules.Chat.Infrastructure.ExternalServices.AI.OpenAI;

/// <summary>
/// Configuration options for OpenAI client
/// </summary>
public sealed class OpenAiOptions
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "Chat:OpenAi";

    /// <summary>
    /// OpenAI API base URL (default: https://api.openai.com)
    /// For Azure OpenAI, use: https://{your-resource-name}.openai.azure.com
    /// Note: Should NOT include trailing slash
    /// </summary>
    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://api.openai.com";

    /// <summary>
    /// Responses API path (default: /v1/responses)
    /// Note: Should start with forward slash
    /// </summary>
    [Required]
    public string ResponsesApiPath { get; set; } = "/v1/responses";

    /// <summary>
    /// OpenAI API key
    /// </summary>
    [Required]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model to use for chat completions (e.g., "gpt-4o", "gpt-4o-mini")
    /// </summary>
    [Required]
    public string Model { get; set; } = "gpt-4o";

    /// <summary>
    /// Maximum tokens in response (null = use model default)
    /// </summary>
    [Range(1, 128000)]
    public int? MaxTokens { get; set; } = 4000;

    /// <summary>
    /// HTTP client timeout in seconds
    /// </summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Validates the configuration and normalizes URL paths
    /// </summary>
    public IEnumerable<ValidationResult> Validate()
    {
        // Validate BaseUrl format
        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            yield return new ValidationResult("BaseUrl is required", [nameof(BaseUrl)]);
        }
        else
        {
            if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri))
            {
                yield return new ValidationResult("BaseUrl must be a valid absolute URL", [nameof(BaseUrl)]);
            }
            else if (uri.Scheme != "https")
            {
                yield return new ValidationResult("BaseUrl must use HTTPS for security", [nameof(BaseUrl)]);
            }
        }

        // Validate ResponsesApiPath format
        if (string.IsNullOrWhiteSpace(ResponsesApiPath))
        {
            yield return new ValidationResult("ResponsesApiPath is required", [nameof(ResponsesApiPath)]);
        }
        else if (!ResponsesApiPath.StartsWith('/'))
        {
            yield return new ValidationResult("ResponsesApiPath must start with '/'", [nameof(ResponsesApiPath)]);
        }

        // Validate ApiKey
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            yield return new ValidationResult("ApiKey is required", [nameof(ApiKey)]);
        }

        // Validate Model
        if (string.IsNullOrWhiteSpace(Model))
        {
            yield return new ValidationResult("Model is required", [nameof(Model)]);
        }
    }

    /// <summary>
    /// Normalizes BaseUrl by removing trailing slash if present
    /// </summary>
    public void Normalize()
    {
        if (!string.IsNullOrWhiteSpace(BaseUrl))
        {
            BaseUrl = BaseUrl.TrimEnd('/');
        }

        if (!string.IsNullOrWhiteSpace(ResponsesApiPath) && !ResponsesApiPath.StartsWith('/'))
        {
            ResponsesApiPath = "/" + ResponsesApiPath;
        }
    }
}