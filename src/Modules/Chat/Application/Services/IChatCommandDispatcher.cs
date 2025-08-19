using BuildingBlocks.Core.Functional.Results;
using Axon.Api.Contracts.Chat;

namespace Axon.Modules.Chat.Application.Services;

/// <summary>
/// Dispatches chat requests to appropriate commands based on business rules.
/// </summary>
public interface IChatCommandDispatcher
{
    /// <summary>
    /// Processes a chat message request by dispatching to the appropriate command.
    /// </summary>
    /// <param name="request">The chat message request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the chat response</returns>
    Task<Result<ProcessMessageResponse>> ProcessMessageAsync(
        ProcessMessageRequest request, 
        CancellationToken cancellationToken = default);
}