using Axon.Modules.Chat.Application.Commands.ProcessMessage;
using Axon.Shared.Common;

namespace Axon.Modules.Chat.Application.Contracts;

/// <summary>
/// Central contract for message processing orchestration
/// Coordinated by integration-coordinator to ensure all agents implement consistently
/// </summary>
public interface IMessageProcessor
{
    /// <summary>
    /// Process message through the complete pipeline
    /// </summary>
    /// <param name="command">Message processing command</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing processing response</returns>
    Task<Result<ProcessMessageResponse>> ProcessAsync(
        ProcessMessageCommand command, 
        CancellationToken cancellationToken);
}