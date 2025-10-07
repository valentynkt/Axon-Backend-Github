namespace Axon.Modules.Chat.Application.Contracts.AI;

/// <summary>
/// Describes the capabilities of an AI provider
/// </summary>
public sealed record AiProviderCapabilities
{
    /// <summary>
    /// Whether this provider supports Model Context Protocol (MCP) for tool calling
    /// </summary>
    public bool SupportsMcp { get; init; }

    /// <summary>
    /// Whether this provider supports streaming responses
    /// </summary>
    public bool SupportsStreaming { get; init; }

    /// <summary>
    /// Whether this provider supports function/tool calling
    /// </summary>
    public bool SupportsToolCalling { get; init; }

    /// <summary>
    /// Maximum tokens supported by this provider
    /// </summary>
    public int MaxTokens { get; init; }

    /// <summary>
    /// List of available models for this provider
    /// </summary>
    public string[] SupportedModels { get; init; } = [];

    /// <summary>
    /// Whether this provider supports conversation continuation via response IDs
    /// </summary>
    public bool SupportsConversationContinuation { get; init; }
}
