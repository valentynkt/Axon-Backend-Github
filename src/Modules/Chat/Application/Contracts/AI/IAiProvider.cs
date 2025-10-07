using Axon.Modules.Chat.Application.DTOs.Requests;
using Axon.Modules.Chat.Application.DTOs.Responses;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Chat.Application.Contracts.AI;

/// <summary>
/// Abstraction for AI providers (OpenAI, Claude, Gemini, etc.)
/// Provides a unified interface for message processing regardless of underlying provider
/// </summary>
public interface IAiProvider
{
    /// <summary>
    /// Name of the AI provider (e.g., "OpenAI", "Claude", "Gemini")
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Capabilities of this AI provider
    /// </summary>
    AiProviderCapabilities Capabilities { get; }

    /// <summary>
    /// Process a message using this AI provider
    /// </summary>
    /// <param name="request">The AI request containing message and configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing AI response or error</returns>
    Task<Result<AiResponse, Error>> ProcessMessageAsync(AiRequest request, CancellationToken cancellationToken);
}
