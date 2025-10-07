namespace Axon.Modules.Chat.Application.Contracts.AI;

/// <summary>
/// Factory for creating AI provider instances
/// </summary>
public interface IAiProviderFactory
{
    /// <summary>
    /// Gets an AI provider by name
    /// </summary>
    /// <param name="providerName">Name of the provider (e.g., "openai", "claude")</param>
    /// <returns>The AI provider instance</returns>
    /// <exception cref="ArgumentException">Thrown when provider name is unknown</exception>
    IAiProvider GetProvider(string providerName);

    /// <summary>
    /// Gets the default AI provider
    /// </summary>
    IAiProvider GetDefaultProvider();
}
