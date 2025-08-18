using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.Abstractions.AI;
using Axon.Modules.Chat.Application.Abstractions.Infrastructure.Caching;
using Axon.Modules.Chat.Application.Abstractions.Persistence;
using Axon.Modules.Chat.Application.Abstractions.Security;
using Axon.Modules.Chat.Application.Abstractions.Telemetry;
using Axon.Modules.Chat.Application.Services.Infrastructure.Idempotency;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;

using Axon.Modules.Chat.Domain.Time;
using Axon.Modules.Chat.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Functional.Results;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Application.Commands.AppendUserMessage;

/// <summary>
/// Internal cache record for tracking phase-1 user message commitment
/// </summary>
internal sealed record Phase1Cache(string Type, Guid ConversationId, Guid UserMessageId, string Content);

/// <summary>
/// Handler for appending user message and getting AI response
/// Implements two-phase commit pattern with context linking
/// </summary>
public sealed class AppendUserMessageHandler : ICommandHandler<AppendUserMessageCommand, AppendUserMessageResponse>
{
    private readonly IConversationRepository _repository;
    private readonly IAiClient _aiClient;
    private readonly IIdempotencyCache _idempotencyCache;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;
    private readonly ILogger<AppendUserMessageHandler> _logger;
    private readonly IAppTelemetry? _telemetry;

    public AppendUserMessageHandler(
        IConversationRepository repository,
        IAiClient aiClient,
        IIdempotencyCache idempotencyCache,
        ICurrentUserService currentUser,
        IClock clock,
        ILogger<AppendUserMessageHandler> logger,
        IAppTelemetry? telemetry = null)
    {
        _repository = repository;
        _aiClient = aiClient;
        _idempotencyCache = idempotencyCache;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
        _telemetry = telemetry;
    }

    public async Task<Result<AppendUserMessageResponse>> Handle(
        AppendUserMessageCommand command,
        CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // 0) Require auth & resolve owner
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            _telemetry?.TrackValidationFailure(nameof(AppendUserMessageCommand), "CHAT.AUTH.UNAUTHENTICATED");
            return Result<AppendUserMessageResponse>.Failure(
                Error.Unauthorized("User must be authenticated to send messages.", "CHAT.AUTH.UNAUTHENTICATED"));
        }

        var ownerIdResult = UserId.FromString(_currentUser.UserId!);
        if (ownerIdResult.IsFailure)
        {
            return Result<AppendUserMessageResponse>.Failure(ownerIdResult.Error);
        }

        // 1) Load conversation & check ownership
        if (command.ConversationId == Guid.Empty)
        {
            return Result<AppendUserMessageResponse>.Failure(
                Error.Validation("ConversationId cannot be empty.", "CHAT.ID.EMPTY"));
        }

        var conversationId = ConversationId.From(command.ConversationId);
        var conversation = await _repository.GetByIdAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            return Result<AppendUserMessageResponse>.Failure(
                Error.NotFound($"Conversation {command.ConversationId} not found.", "CHAT.CONVERSATION.NOT_FOUND"));
        }

        if (!conversation.BelongsTo(ownerIdResult.Value))
        {
            return Result<AppendUserMessageResponse>.Failure(
                Error.Forbidden("Conversation does not belong to the current user.", "CHAT.CONVERSATION.ACCESS_DENIED"));
        }

        // 2) Idempotency (app-level) — compute key if header missing
        var suppliedKey = string.IsNullOrWhiteSpace(command.IdempotencyKey) 
            ? null 
            : command.IdempotencyKey!.Trim();
        
        var idempotencyKey = suppliedKey ?? IdempotencyKey.Compute(
            command.Content, 
            conversationId.Value, 
            ownerIdResult.Value.Value);

        // Check for final cached response
        var cachedResult = await _idempotencyCache.GetAsync<AppendUserMessageResponse>(idempotencyKey, cancellationToken);
        if (cachedResult is not null)
        {
            _logger.LogInformation(
                "Idempotency cache hit for conversation {ConversationId}, key {IdempotencyKey}",
                command.ConversationId,
                idempotencyKey);
            return Result<AppendUserMessageResponse>.Success(cachedResult);
        }

        // Check for phase-1 cache (user message already committed)
        var phase1 = await _idempotencyCache.GetAsync<Phase1Cache>(idempotencyKey, cancellationToken);
        bool reusePhase1 = phase1 is { Type: "phase1" }
                           && phase1.ConversationId == command.ConversationId
                           && string.Equals(phase1.Content, command.Content, StringComparison.Ordinal);
        Guid userMessageId;

        // 3) Append user message & commit (phase 1)
        var contentResult = MessageContent.Create(command.Content);
        if (contentResult.IsFailure)
        {
            _telemetry?.TrackValidationFailure(nameof(AppendUserMessageCommand), contentResult.Error.Code);
            return Result<AppendUserMessageResponse>.Failure(contentResult.Error);
        }

        if (reusePhase1)
        {
            // Reuse existing user message from phase-1 cache
            userMessageId = phase1!.UserMessageId;
            
            _logger.LogInformation(
                "Reusing user message {MessageId} from phase-1 cache for conversation {ConversationId}",
                userMessageId,
                conversation.Id.Value);
        }
        else
        {
            // Append new user message
            var userMessageResult = conversation.AppendUserMessage(contentResult.Value, _clock);
            if (userMessageResult.IsFailure)
            {
                return Result<AppendUserMessageResponse>.Failure(userMessageResult.Error);
            }

            var userMessage = userMessageResult.Value;
            userMessageId = userMessage.Id.Value;

            // Commit user message (phase 1)
            await _repository.UpdateAsync(conversation, cancellationToken);
            await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

            // Store phase-1 cache to prevent duplicate user messages on retry
            await _idempotencyCache.SetAsync(
                idempotencyKey,
                new Phase1Cache("phase1", conversation.Id.Value, userMessageId, command.Content),
                IdempotencyDefaults.Window,
                cancellationToken);

            _logger.LogInformation(
                "User message {MessageId} appended to conversation {ConversationId}",
                userMessage.Id.Value,
                conversation.Id.Value);
        }

        // 4) Build AI request with previous_response_id (context link) and MCP servers
        var buildResult = _requestBuilder.BuildAiRequest(
            command.Content, 
            conversation.LastAiResponseId?.Value);
        if (buildResult.IsFailure)
        {
            // User message already committed, return service unavailable
            return Result<AppendUserMessageResponse>.Failure(
                Error.Internal("Failed to build AI request. Please retry.", "CHAT.AI.REQUEST_BUILD_FAILED"));
        }

        var (aiRequest, mcpServerCount) = buildResult.Value;

        // 5) Call AI (non-streaming)
        try
        {
            _logger.LogInformation(
                "Calling AI for conversation {ConversationId} with {McpServerCount} MCP servers, previous_response_id: {PreviousResponseId}",
                conversation.Id.Value,
                mcpServerCount,
                conversation.LastAiResponseId?.Value ?? "none");

            var aiStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var aiResponse = await _aiClient.ProcessMessageAsync(aiRequest, cancellationToken);
            aiStopwatch.Stop();
            
            _telemetry?.TrackAiClientRequest("responses.mcp", aiStopwatch.Elapsed, aiResponse.IsSuccess);
            
            if (aiResponse.IsFailure)
            {
                _logger.LogWarning(
                    "AI call failed for conversation {ConversationId}: {ErrorCode}",
                    conversation.Id.Value,
                    aiResponse.Error.Code);

                // User message committed, AI failed - return service unavailable
                stopwatch.Stop();
                _telemetry?.TrackMessageProcessed(conversation.Id.Value, stopwatch.Elapsed, success: false);
                return Result<AppendUserMessageResponse>.Failure(
                    Error.Internal("AI processing failed. Please retry.", "CHAT.AI.PROCESSING_FAILED"));
            }

            // Validate AI response using centralized service
            var validated = _responseMapper.ValidateAndProcessResponse(aiResponse.Value);
            if (validated.IsFailure)
            {
                _logger.LogWarning(
                    "AI response validation failed for conversation {ConversationId}: {Code}",
                    conversation.Id.Value,
                    validated.Error.Code);

                stopwatch.Stop();
                _telemetry?.TrackMessageProcessed(conversation.Id.Value, stopwatch.Elapsed, success: false);
                return Result<AppendUserMessageResponse>.Failure(
                    Error.Internal("AI returned invalid response. Please retry.", "CHAT.AI.INVALID_RESPONSE"));
            }

            var finalAi = validated.Value;

            // 6) Append assistant with AiResponseId & commit (phase 2)
            var aiResponseIdResult = AiResponseId.Create(finalAi.ResponseId!);
            if (aiResponseIdResult.IsFailure)
            {
                return Result<AppendUserMessageResponse>.Failure(aiResponseIdResult.Error);
            }

            var assistantContentResult = MessageContent.Create(finalAi.Content!);
            if (assistantContentResult.IsFailure)
            {
                return Result<AppendUserMessageResponse>.Failure(assistantContentResult.Error);
            }

            var assistantMessageResult = conversation.AppendAssistantMessage(
                assistantContentResult.Value,
                aiResponseIdResult.Value,
                _clock);

            if (assistantMessageResult.IsFailure)
            {
                return Result<AppendUserMessageResponse>.Failure(assistantMessageResult.Error);
            }

            var assistantMessage = assistantMessageResult.Value;

            // Commit assistant message (phase 2)
            await _repository.UpdateAsync(conversation, cancellationToken);
            await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Assistant message {MessageId} appended to conversation {ConversationId} with response_id {ResponseId}",
                assistantMessage.Id.Value,
                conversation.Id.Value,
                aiResponseIdResult.Value.Value);

            // Build response
            var response = new AppendUserMessageResponse(
                conversation.Id.Value,
                userMessageId,
                assistantMessage.Id.Value,
                assistantMessage.Content.Value);

            // Cache successful response
            await _idempotencyCache.SetAsync(
                idempotencyKey,
                response,
                IdempotencyDefaults.Window,
                cancellationToken);

            stopwatch.Stop();
            _telemetry?.TrackMessageProcessed(conversation.Id.Value, stopwatch.Elapsed, success: true);

            return Result<AppendUserMessageResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error processing message for conversation {ConversationId}",
                conversation.Id.Value);

            // User message already committed, return service unavailable
            return Result<AppendUserMessageResponse>.Failure(
                Error.Internal("An unexpected error occurred. Please retry.", "CHAT.AI.UNEXPECTED_ERROR"));
        }
    }
}