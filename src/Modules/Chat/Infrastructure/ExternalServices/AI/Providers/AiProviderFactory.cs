using Axon.Modules.Chat.Application.Contracts.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Infrastructure.ExternalServices.AI.Providers;

/// <summary>
/// Factory for creating AI provider instances
/// </summary>
public sealed class AiProviderFactory : IAiProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AiProviderFactory> _logger;
    private const string DefaultProviderName = "openai";

    public AiProviderFactory(
        IServiceProvider serviceProvider,
        ILogger<AiProviderFactory> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IAiProvider GetProvider(string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        var normalizedName = providerName.ToLowerInvariant();
        _logger.LogDebug("Resolving AI provider: {ProviderName}", normalizedName);

        return normalizedName switch
        {
            "openai" => _serviceProvider.GetRequiredService<OpenAiProvider>(),
            "claude" => throw new NotImplementedException("Claude provider not yet implemented"),
            "gemini" => throw new NotImplementedException("Gemini provider not yet implemented"),
            _ => throw new ArgumentException($"Unknown AI provider: {providerName}", nameof(providerName))
        };
    }

    public IAiProvider GetDefaultProvider()
    {
        _logger.LogDebug("Resolving default AI provider: {DefaultProvider}", DefaultProviderName);
        return GetProvider(DefaultProviderName);
    }
}
