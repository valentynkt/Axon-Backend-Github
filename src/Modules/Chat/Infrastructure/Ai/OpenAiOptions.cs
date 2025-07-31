namespace Axon.Modules.Chat.Infrastructure.Ai;

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
    /// OpenAI API key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Model to use for chat completions
    /// </summary>
    public string Model { get; set; } = "gpt-4o";

    /// <summary>
    /// Maximum tokens in response
    /// </summary>
    public int MaxTokens { get; set; } = 4000;

    /// <summary>
    /// Temperature for response randomness (0.0 to 2.0)
    /// </summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>
    /// Whether MCP integration is enabled
    /// </summary>
    public bool McpEnabled { get; set; } = true;
}