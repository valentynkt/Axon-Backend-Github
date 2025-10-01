using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.Contracts.AI;
using Axon.Modules.Chat.Application.Contracts.Telemetry;
using Axon.Modules.Chat.Application.Contracts.Persistence;
using Axon.Modules.Chat.Application.DTOs.Responses;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using Axon.Modules.Chat.Domain.Errors;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Chat.Application.Services.Orchestration;

/// <summary>
/// Orchestrates the complete message processing flow after user message has been added to conversation.
/// </summary>
public sealed partial class MessageProcessingOrchestrator : IMessageProcessingOrchestrator
{
    [GeneratedRegex(@"\b([1-5]\d{2})\b", RegexOptions.Compiled)]
    private static partial Regex HttpStatusCodeRegex();

    private readonly IConversationRepository _repository;
    private readonly IMcpServerResolutionService _mcpResolutionService;
    private readonly IAiProcessingService _aiProcessingService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MessageProcessingOrchestrator> _logger;
    private readonly IChatTelemetry? _telemetry;

    public MessageProcessingOrchestrator(
        IConversationRepository repository,
        IMcpServerResolutionService mcpResolutionService,
        IAiProcessingService aiProcessingService,
        TimeProvider timeProvider,
        ILogger<MessageProcessingOrchestrator> logger,
        IChatTelemetry? telemetry = null)
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
            _logger.LogInformation("Starting message processing for conversation {ConversationId}. UserMessageId: {UserMessageId}",
                conversation.Id.Value, userMessageId.Value);

            // Step 1: Resolve MCP servers
            _logger.LogDebug("Resolving MCP servers for conversation {ConversationId}", conversation.Id.Value);
            var mcpConfigs = await _mcpResolutionService.ResolveServersAsync(conversation.Id, cancellationToken);
            _logger.LogDebug("Resolved {McpServerCount} MCP servers for conversation {ConversationId}",
                mcpConfigs?.Length ?? 0, conversation.Id.Value);

            // Step 2: Process AI request and get response
            var previousResponseId = conversation.LastAiResponseId;
            _logger.LogDebug("Processing AI request for conversation {ConversationId}. PreviousResponseId: {PreviousResponseId}",
                conversation.Id.Value, previousResponseId?.Value ?? "none");

            var aiProcessingResult = await _aiProcessingService.ProcessMessageAsync(
                userMessage, conversation.Id, previousResponseId, mcpConfigs, cancellationToken);

            if (aiProcessingResult.IsFailure)
            {
                _logger.LogWarning("AI processing failed for conversation {ConversationId}. Error: {ErrorCode} - {ErrorMessage}",
                    conversation.Id.Value, aiProcessingResult.Error.Code, aiProcessingResult.Error.Message);

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
            // No need to call UpdateAsync - the conversation is already tracked by EF Core
            // Calling UpdateAsync would update the entity's concurrency token incorrectly
            _logger.LogDebug("About to save conversation {ConversationId}. Version before save: {Version}",
                conversation.Id.Value, conversation.Version);
            await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Saved conversation {ConversationId}. Version after save: {Version}",
                conversation.Id.Value, conversation.Version);

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
        catch (DbUpdateConcurrencyException concurrencyEx)
        {
            _logger.LogWarning(concurrencyEx,
                "Concurrency conflict during message processing for conversation {ConversationId}. Another process may have modified the conversation.",
                conversation.Id.Value);

            overall.Stop();
            _telemetry?.TrackMessageProcessed(conversation.Id.Value, overall.Elapsed, success: false);

            return Result.Failure<ProcessMessageResponse, Error>(
                Error.Concurrency(
                    ChatDomainErrors.Processing.ConcurrencyConflictMessage,
                    ChatDomainErrors.Processing.ConcurrencyConflictCode,
                    metadata: new Dictionary<string, object>
                    {
                        ["ConversationId"] = conversation.Id.Value,
                        ["Operation"] = "MessageProcessing",
                        ["ExceptionType"] = concurrencyEx.GetType().Name,
                        ["ExceptionMessage"] = concurrencyEx.Message
                    }));
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx,
                "Database persistence error during message processing for conversation {ConversationId}",
                conversation.Id.Value);

            overall.Stop();
            _telemetry?.TrackMessageProcessed(conversation.Id.Value, overall.Elapsed, success: false);

            return Result.Failure<ProcessMessageResponse, Error>(
                Error.Persistence(
                    ChatDomainErrors.Processing.PersistenceFailedMessage,
                    ChatDomainErrors.Processing.PersistenceFailedCode,
                    dbEx,
                    metadata: new Dictionary<string, object>
                    {
                        ["ConversationId"] = conversation.Id.Value,
                        ["Operation"] = "MessageProcessing",
                        ["InnerExceptionType"] = dbEx.InnerException?.GetType().Name ?? "Unknown"
                    }));
        }
        catch (TimeoutException timeoutEx)
        {
            _logger.LogWarning(timeoutEx,
                "Timeout during message processing for conversation {ConversationId}",
                conversation.Id.Value);

            overall.Stop();
            _telemetry?.TrackMessageProcessed(conversation.Id.Value, overall.Elapsed, success: false);

            return Result.Failure<ProcessMessageResponse, Error>(
                Error.Timeout(
                    ChatDomainErrors.Processing.TimeoutMessage,
                    ChatDomainErrors.Processing.TimeoutCode,
                    timeout: overall.Elapsed,
                    metadata: new Dictionary<string, object>
                    {
                        ["ConversationId"] = conversation.Id.Value,
                        ["ElapsedTime"] = overall.Elapsed.ToString()
                    }));
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx,
                "Network error during message processing for conversation {ConversationId}",
                conversation.Id.Value);

            overall.Stop();
            _telemetry?.TrackMessageProcessed(conversation.Id.Value, overall.Elapsed, success: false);

            var isTransient = IsTransientHttpError(httpEx);
            return Result.Failure<ProcessMessageResponse, Error>(
                Error.Network(
                    ChatDomainErrors.AiProcessing.NetworkErrorMessage,
                    ChatDomainErrors.AiProcessing.NetworkErrorCode,
                    httpEx,
                    metadata: new Dictionary<string, object>
                    {
                        ["ConversationId"] = conversation.Id.Value,
                        ["IsTransient"] = isTransient,
                        ["HttpStatusCode"] = ExtractHttpStatusCode(httpEx)?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "Unknown"
                    }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error during message processing for conversation {ConversationId}. ExceptionType: {ExceptionType}",
                conversation.Id.Value, ex.GetType().Name);

            overall.Stop();
            _telemetry?.TrackMessageProcessed(conversation.Id.Value, overall.Elapsed, success: false);

            return Result.Failure<ProcessMessageResponse, Error>(
                Error.Internal(
                    ChatDomainErrors.Processing.UnexpectedErrorMessage,
                    ChatDomainErrors.Processing.UnexpectedErrorCode,
                    ex,
                    metadata: new Dictionary<string, object>
                    {
                        ["ConversationId"] = conversation.Id.Value,
                        ["ExceptionType"] = ex.GetType().Name,
                        ["StackTrace"] = ex.StackTrace ?? "No stack trace available"
                    }));
        }
    }

    /// <summary>
    /// Determines if an HTTP exception represents a transient error that should be retried.
    /// </summary>
    private static bool IsTransientHttpError(HttpRequestException httpEx)
    {
        // Check for common transient HTTP errors
        var message = httpEx.Message.ToLowerInvariant();
        return message.Contains("timeout", StringComparison.Ordinal) ||
               message.Contains("connection reset", StringComparison.Ordinal) ||
               message.Contains("connection closed", StringComparison.Ordinal) ||
               message.Contains("network is unreachable", StringComparison.Ordinal) ||
               message.Contains("temporary failure in name resolution", StringComparison.Ordinal);
    }

    /// <summary>
    /// Extracts HTTP status code from HttpRequestException if available.
    /// </summary>
    private static int? ExtractHttpStatusCode(HttpRequestException httpEx)
    {
        // Try to get status code from HttpRequestException.Data
        if (httpEx.Data.Contains("StatusCode") && httpEx.Data["StatusCode"] is int statusCode)
            return statusCode;

        // Parse from message if it contains status code pattern
        var message = httpEx.Message;
        var patterns = new[] { "HTTP", "status code", "response status" };

        foreach (var pattern in patterns)
        {
            var index = message.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                // Try to find a number after the pattern
                var substring = message.Substring(index);
                var match = HttpStatusCodeRegex().Match(substring);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var code))
                    return code;
            }
        }

        return null;
    }
}