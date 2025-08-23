using System.Threading;
using System.Threading.Tasks;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.Common;
using Axon.Modules.Chat.Application.DTOs.Responses;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Chat.Application.Contracts.AI;

/// <summary>
/// Orchestrates the complete message processing flow after user message has been added to conversation.
/// Handles MCP resolution, AI processing, assistant response appending, and response building.
/// </summary>
public interface IMessageProcessingOrchestrator
{
    /// <summary>
    /// Processes a user message through the complete AI workflow and returns the final response.
    /// This includes MCP resolution, AI processing, assistant message appending, and persistence.
    /// </summary>
    /// <param name="conversation">The conversation to process (must already contain the user message)</param>
    /// <param name="userMessage">The user's message content for AI context</param>
    /// <param name="userMessageId">The ID of the user message that was added</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success with complete process message response or failure with error</returns>
    Task<Result<ProcessMessageResponse, Error>> ProcessUserMessageAsync(
        Conversation conversation,
        MessageContent userMessage,
        MessageId userMessageId,
        CancellationToken cancellationToken);
}