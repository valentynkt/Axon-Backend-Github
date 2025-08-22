using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.Abstractions.Persistence;
using Axon.Modules.Chat.Application.Abstractions.Telemetry;
using Axon.Modules.Chat.Application.Common;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Application.Services;

/// <summary>
/// Orchestrates the complete message processing flow after user message has been added to conversation.
/// </summary>
public sealed class MessageProcessingOrchestrator : IMessageProcessingOrchestrator
{
    private readonly IConversationRepository _repository;
    private readonly IMcpServerResolutionService _mcpResolutionService;
    private readonly IAiProcessingService _aiProcessingService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MessageProcessingOrchestrator> _logger;
    private readonly IAppTelemetry? _telemetry;

    public MessageProcessingOrchestrator(
        IConversationRepository repository,
        IMcpServerResolutionService mcpResolutionService,
        IAiProcessingService aiProcessingService,
        TimeProvider timeProvider,
        ILogger<MessageProcessingOrchestrator> logger,
        IAppTelemetry? telemetry = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _mcpResolutionService = mcpResolutionService ?? throw new ArgumentNullException(nameof(mcpResolutionService));
        _aiProcessingService = aiProcessingService ?? throw new ArgumentNullException(nameof(aiProcessingService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _telemetry = telemetry;
    }

    public async Task<Result<ProcessMessageResponse, Error>> ProcessUserMessageAsync(
        Conversation conversation,
        MessageContent userMessage,
        MessageId userMessageId,
        CancellationToken cancellationToken)
    {
        var overall = Stopwatch.StartNew();

        try
        {
            // Step 1: Resolve MCP servers
            var mcpConfigs = await _mcpResolutionService.ResolveServersAsync(conversation.Id, cancellationToken);

            // Step 2: Process AI request and get response
            var previousResponseId = conversation.LastAiResponseId;
            var aiProcessingResult = await _aiProcessingService.ProcessMessageAsync(
                userMessage, conversation.Id, previousResponseId, mcpConfigs, cancellationToken);

            if (aiProcessingResult.IsFailure)
            {
                overall.Stop();
                _telemetry?.TrackMessageProcessed(conversation.Id.Value, overall.Elapsed, success: false);
                return Result.Failure<ProcessMessageResponse, Error>(aiProcessingResult.Error);
            }

            var aiResult = aiProcessingResult.Value;

            // Step 3: Append assistant message to conversation
            var assistantMessageResult = conversation.AppendAssistantResponseToConversation(
                aiResult.AssistantContent, aiResult.ResponseId, _timeProvider);

            if (assistantMessageResult.IsFailure)
            {
                overall.Stop();
                _telemetry?.TrackMessageProcessed(conversation.Id.Value, overall.Elapsed, success: false);
                return Result.Failure<ProcessMessageResponse, Error>(assistantMessageResult.Error);
            }

            var assistantMessage = assistantMessageResult.Value;

            // Step 4: Persist changes
            await _repository.UpdateAsync(conversation, cancellationToken);
            await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Assistant message {MessageId} appended to conversation {ConversationId}. Prev AI: {Prev} -> New AI: {New}",
                assistantMessage.Id.Value,
                conversation.Id.Value,
                previousResponseId?.Value ?? "none",
                aiResult.ResponseId.Value);

            // Step 5: Build final response
            var response = new ProcessMessageResponse(
                ConversationId: conversation.Id,
                UserMessageId: userMessageId,
                AssistantMessageId: assistantMessage.Id,
                AssistantMessage: aiResult.AssistantContent);

            overall.Stop();
            _telemetry?.TrackMessageProcessed(conversation.Id.Value, overall.Elapsed, success: true);

            return Result.Success<ProcessMessageResponse, Error>(response);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Message processing cancelled for conversation {ConversationId}", conversation.Id.Value);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error during message processing for conversation {ConversationId}",
                conversation.Id.Value);

            overall.Stop();
            _telemetry?.TrackMessageProcessed(conversation.Id.Value, overall.Elapsed, success: false);

            return Result.Failure<ProcessMessageResponse, Error>(
                Error.Internal("An unexpected error occurred during message processing. Please retry.", "CHAT.MESSAGE_PROCESSING.UNEXPECTED_ERROR"));
        }
    }
}