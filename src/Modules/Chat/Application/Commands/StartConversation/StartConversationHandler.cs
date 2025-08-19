using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.Abstractions.AI;
using Axon.Modules.Chat.Application.Abstractions.Telemetry;
using Axon.Modules.Chat.Application.Common;
using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;

using Axon.Modules.Chat.Primitives.ValueObjects;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Core.Domain.Primitives;
using BuildingBlocks.Core.Functional.Results;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Application.Commands.StartConversation;

/// <summary>
/// Handler that performs a full first turn:
/// 1) Start conversation
/// 2) Append user's first message (persist)
/// 3) Resolve MCP (best effort)
/// 4) Call AI (no previous response id)
/// 5) Append assistant reply (persist)
/// </summary>
public sealed class StartConversationHandler
    : ICommandHandler<StartConversationCommand, ChatMessageResponse>
{
    private readonly IConversationRepository _repository;
    private readonly IAiClient _aiClient;
    private readonly IMcpServerResolver _mcpResolver;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StartConversationHandler> _logger;
    private readonly IAppTelemetry? _telemetry;

    public StartConversationHandler(
        IConversationRepository repository,
        IAiClient aiClient,
        IMcpServerResolver mcpResolver,
        ICurrentUserService currentUser,
        TimeProvider timeProvider,
        ILogger<StartConversationHandler> logger,
        IAppTelemetry? telemetry = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _aiClient = aiClient ?? throw new ArgumentNullException(nameof(aiClient));
        _mcpResolver = mcpResolver ?? throw new ArgumentNullException(nameof(mcpResolver));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _telemetry = telemetry; // optional
    }

    public async Task<Result<ChatMessageResponse>> Handle(
        StartConversationCommand command,
        CancellationToken cancellationToken)
    {
        var overall = System.Diagnostics.Stopwatch.StartNew();

        // 1) Authentication
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            _telemetry?.TrackValidationFailure(nameof(StartConversationCommand), "CHAT.AUTH.UNAUTHENTICATED");
            return Result<ChatMessageResponse>.Failure(
                Error.Unauthorized("User must be authenticated to start a conversation.", "CHAT.AUTH.UNAUTHENTICATED"));
        }

        var ownerIdResult = UserId.FromString(_currentUser.UserId!);
        if (ownerIdResult.IsFailure)
            return Result<ChatMessageResponse>.Failure(ownerIdResult.Error);

        // 2) Start conversation (no title in MVP)
        var startResult = Conversation.StartNewConversation(ownerIdResult.Value, titleOrNull: null, _timeProvider);
        if (startResult.IsFailure)
            return Result<ChatMessageResponse>.Failure(startResult.Error);

        var conversation = startResult.Value;

        // 3) Append the first user message (Phase 1) and persist
        var userMessageResult = conversation.AppendUserMessageToConversation(command.Message, _timeProvider);
        if (userMessageResult.IsFailure)
            return Result<ChatMessageResponse>.Failure(userMessageResult.Error);

        var userMessageId = userMessageResult.Value.Id.Value;

        // New aggregate → add to repository
        await _repository.AddAsync(conversation, cancellationToken);
        await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Conversation {ConversationId} started by user {UserId}. First user message {MessageId} persisted.",
            conversation.Id.Value,
            ownerIdResult.Value.Value,
            userMessageId);

        // 4) Resolve MCP (optional, non-failing)
        McpServerConfig[]? mcpConfigs = null;
        try
        {
            var resolved = await _mcpResolver.ResolveServersAsync(cancellationToken);
            if (resolved is { Length: > 0 })
                mcpConfigs = resolved;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to resolve MCP servers for conversation {ConversationId}. Continuing without MCP.",
                conversation.Id.Value);
        }

        // 5) AI request (no previous response id on first turn)
        var aiRequest = new AiRequest(
            Message: command.Message.Value,
            McpConfigs: mcpConfigs,
            PreviousResponseId: null);

        try
        {
            var aiTimer = System.Diagnostics.Stopwatch.StartNew();
            var ai = await _aiClient.ProcessMessageAsync(aiRequest, cancellationToken);
            aiTimer.Stop();
            _telemetry?.TrackAiClientRequest("responses.mcp", aiTimer.Elapsed, ai.IsSuccess);

            if (ai.IsFailure)
            {
                _logger.LogWarning(
                    "AI call failed for new conversation {ConversationId}: {ErrorCode}",
                    conversation.Id.Value, ai.Error.Code);

                overall.Stop();
                _telemetry?.TrackMessageProcessed(conversation.Id.Value, overall.Elapsed, success: false);

                // Phase-1 (user message + conversation) already persisted; surface retryable error
                return Result<ChatMessageResponse>.Failure(
                    Error.Internal("AI processing failed. Please retry.", "CHAT.AI.PROCESSING_FAILED"));
            }

            if (string.IsNullOrWhiteSpace(ai.Value.Content) || string.IsNullOrWhiteSpace(ai.Value.ResponseId))
            {
                _logger.LogWarning(
                    "AI returned invalid response for new conversation {ConversationId}.",
                    conversation.Id.Value);

                overall.Stop();
                _telemetry?.TrackMessageProcessed(conversation.Id.Value, overall.Elapsed, success: false);

                return Result<ChatMessageResponse>.Failure(
                    Error.Internal("AI returned invalid response. Please retry.", "CHAT.AI.INVALID_RESPONSE"));
            }

            // 6) Append assistant reply (Phase 2) and persist
            var aiResponseIdResult = AiResponseId.Create(ai.Value.ResponseId);
            if (aiResponseIdResult.IsFailure)
                return Result<ChatMessageResponse>.Failure(aiResponseIdResult.Error);

            var assistantContentResult = MessageContent.Create(ai.Value.Content);
            if (assistantContentResult.IsFailure)
                return Result<ChatMessageResponse>.Failure(assistantContentResult.Error);

            var assistantMessageResult = conversation.AppendAssistantResponseToConversation(
                assistantContentResult.Value, aiResponseIdResult.Value, _timeProvider);

            if (assistantMessageResult.IsFailure)
                return Result<ChatMessageResponse>.Failure(assistantMessageResult.Error);

            await _repository.UpdateAsync(conversation, cancellationToken);
            await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

            var assistantMessageId = assistantMessageResult.Value.Id.Value;

            _logger.LogInformation(
                "Assistant message {MessageId} appended to new conversation {ConversationId} with response_id {ResponseId}",
                assistantMessageId,
                conversation.Id.Value,
                aiResponseIdResult.Value.Value);

            var response = new ChatMessageResponse(
                ConversationId: conversation.Id,
                UserMessageId: MessageId.From(userMessageId),
                AssistantMessageId: MessageId.From(assistantMessageId),
                AssistantMessage: assistantMessageResult.Value.Content.Value);

            overall.Stop();
            _telemetry?.TrackMessageProcessed(conversation.Id.Value, overall.Elapsed, success: true);

            return Result<ChatMessageResponse>.Success(response);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("AI call cancelled for new conversation {ConversationId}", conversation.Id.Value);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error starting conversation {ConversationId}",
                conversation.Id.Value);

            return Result<ChatMessageResponse>.Failure(
                Error.Internal("An unexpected error occurred. Please retry.", "CHAT.AI.UNEXPECTED_ERROR"));
        }
    }
}
