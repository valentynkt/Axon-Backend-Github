using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.DTOs;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Chat.Application.Abstractions;

/// <summary>
/// Result of AI processing operation containing the processed response and metadata.
/// </summary>
public sealed record AiProcessingResult(
    MessageContent AssistantContent,
    AiResponseId ResponseId,
    TimeSpan ProcessingDuration
);

/// <summary>
/// Service for processing AI requests with proper error handling and telemetry.
/// </summary>
public interface IAiProcessingService
{
    /// <summary>
    /// Processes a user message through the AI client with proper error handling and telemetry tracking.
    /// </summary>
    /// <param name="userMessage">The user's message content</param>
    /// <param name="conversationId">The conversation ID for context</param>
    /// <param name="previousResponseId">The previous AI response ID for continuity</param>
    /// <param name="mcpConfigs">MCP server configurations</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success with AI processing result or failure with error</returns>
    Task<Result<AiProcessingResult, Error>> ProcessMessageAsync(
        MessageContent userMessage,
        ConversationId conversationId,
        AiResponseId? previousResponseId,
        McpServerConfig[]? mcpConfigs,
        CancellationToken cancellationToken);
}