using Axon.Modules.Chat.Application.DTOs;
using Axon.Shared.Common;

namespace Axon.Modules.Chat.Application.Abstractions;

/// <summary>
/// Port for AI client that supports direct MCP tool integration
/// </summary>
public interface IAiClient
{
    /// <summary>
    /// Process message with direct MCP tool support
    /// </summary>
    /// <param name="request">Message processing request with MCP configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing AI response with tool results</returns>
    Task<Result<AiResponse>> ProcessMessageAsync(AiRequest request, CancellationToken cancellationToken);
}