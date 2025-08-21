using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.Abstractions.AI;
using Axon.Modules.Chat.Application.Abstractions.Telemetry;
using Axon.Modules.Chat.Application.DTOs;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Application.Services;

/// <summary>
/// Service for processing AI requests with proper error handling and telemetry.
/// </summary>
public sealed class AiProcessingService : IAiProcessingService
{
    private readonly IAiClient _aiClient;
    private readonly ILogger<AiProcessingService> _logger;
    private readonly IAppTelemetry? _telemetry;

    public AiProcessingService(
        IAiClient aiClient,
        ILogger<AiProcessingService> logger,
        IAppTelemetry? telemetry = null)
    {
        _aiClient = aiClient ?? throw new ArgumentNullException(nameof(aiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _telemetry = telemetry;
    }

    public async Task<Result<AiProcessingResult, Error>> ProcessMessageAsync(
        MessageContent userMessage,
        ConversationId conversationId,
        AiResponseId? previousResponseId,
        McpServerConfig[]? mcpConfigs,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Building AI request for conversation {ConversationId} with previousResponseId: {PreviousResponseId}",
            conversationId.Value, previousResponseId?.Value ?? "none");

        var aiRequest = new AiRequest(
            Message: userMessage,
            McpConfigs: mcpConfigs,
            PreviousResponseId: previousResponseId?.Value);

        try
        {
            var aiTimer = Stopwatch.StartNew();
            var aiResult = await _aiClient.ProcessMessageAsync(aiRequest, cancellationToken);
            aiTimer.Stop();

            _telemetry?.TrackAiClientRequest("responses.mcp", aiTimer.Elapsed, aiResult.IsSuccess);

            if (aiResult.IsFailure)
            {
                _logger.LogWarning(
                    "AI call failed for conversation {ConversationId}: {ErrorCode}",
                    conversationId.Value, aiResult.Error.Code);

                return Result.Failure<AiProcessingResult, Error>(
                    Error.Internal("AI processing failed. Please retry.", "CHAT.AI.PROCESSING_FAILED"));
            }

            var aiResponse = aiResult.Value;
            var result = new AiProcessingResult(
                AssistantContent: aiResponse.Content,
                ResponseId: aiResponse.ResponseId,
                ProcessingDuration: aiTimer.Elapsed);

            _logger.LogInformation(
                "AI processing completed for conversation {ConversationId}. Duration: {Duration}ms, ResponseId: {ResponseId}",
                conversationId.Value, aiTimer.Elapsed.TotalMilliseconds, aiResponse.ResponseId.Value);

            return Result.Success<AiProcessingResult, Error>(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("AI call cancelled for conversation {ConversationId}", conversationId.Value);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error during AI processing for conversation {ConversationId}",
                conversationId.Value);

            return Result.Failure<AiProcessingResult, Error>(
                Error.Internal("An unexpected error occurred during AI processing. Please retry.", "CHAT.AI.UNEXPECTED_ERROR"));
        }
    }
}