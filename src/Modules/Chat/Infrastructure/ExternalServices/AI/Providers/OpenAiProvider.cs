using Axon.Modules.Chat.Application.Contracts.AI;
using Axon.Modules.Chat.Application.DTOs.Requests;
using Axon.Modules.Chat.Application.DTOs.Responses;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Infrastructure.ExternalServices.AI.Providers;

/// <summary>
/// OpenAI AI provider implementation
/// Wraps OpenAI-specific client with provider abstraction
/// </summary>
public sealed class OpenAiProvider : IAiProvider
{
    private readonly IAiClient _client;
    private readonly ILogger<OpenAiProvider> _logger;

    public string ProviderName => "OpenAI";

    public AiProviderCapabilities Capabilities => new()
    {
        SupportsMcp = true,
        SupportsStreaming = false, // Not yet implemented
        SupportsToolCalling = true,
        MaxTokens = 128000, // gpt-4o max
        SupportedModels = ["gpt-4o", "gpt-4o-mini", "gpt-4-turbo", "gpt-3.5-turbo"],
        SupportsConversationContinuation = true
    };

    public OpenAiProvider(
        IAiClient client,
        ILogger<OpenAiProvider> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<AiResponse, Error>> ProcessMessageAsync(
        AiRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing message using OpenAI provider");
        return await _client.ProcessMessageAsync(request, cancellationToken);
    }
}
