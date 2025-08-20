using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.Abstractions.AI;
using Axon.Modules.Chat.Application.Abstractions.Persistence;
using Axon.Modules.Chat.Application.Abstractions.Telemetry;
using Axon.Modules.Chat.Application.Common;
using Axon.Modules.Chat.Application.DTOs;

using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Abstractions.CQRS;
using BuildingBlocks.Core.Domain.Primitives;

using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using FastEndpoints;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Application.Commands.AppendUserMessage;

/// <summary>
/// Handler to append a user message and persist the assistant reply.
/// This handler focuses purely on business logic - idempotency is handled by the IdempotencyBehavior.
/// Clean Architecture: Domain logic orchestrated through application services.
/// </summary>
public sealed class AppendUserMessageHandler
    : ICommandHandler<AppendUserMessageCommand, ChatMessageResponse>
{
    private readonly IConversationRepository _repository;
    private readonly IAiClient _aiClient;
    private readonly IMcpServerResolver _mcpResolver;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AppendUserMessageHandler> _logger;
    private readonly IAppTelemetry? _telemetry;

    public AppendUserMessageHandler(
        IConversationRepository repository,
        IAiClient aiClient,
        IMcpServerResolver mcpResolver,
        ICurrentUserService currentUser,
        TimeProvider timeProvider,
        ILogger<AppendUserMessageHandler> logger,
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
        AppendUserMessageCommand command,
        CancellationToken cancellationToken)
    {
        var overall = System.Diagnostics.Stopwatch.StartNew();

        // 1) Authentication & Authorization
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            _telemetry?.TrackValidationFailure(nameof(AppendUserMessageCommand), "CHAT.AUTH.UNAUTHENTICATED");
            return Result<ChatMessageResponse>.Failure(
                Error.Unauthorized("User must be authenticated to send messages.", "CHAT.AUTH.UNAUTHENTICATED"));
        }

        var ownerIdResult = UserId.FromString(_currentUser.UserId!);
        if (ownerIdResult.IsFailure)
            return Result<ChatMessageResponse>.Failure(ownerIdResult.Error);

        // 2) Load conversation & validate ownership
        if (command.ConversationId.Value == Guid.Empty)
        {
            return Result<ChatMessageResponse>.Failure(
                Error.Validation("ConversationId cannot be empty.", "CHAT.ID.EMPTY"));
        }

        var conversation = await _repository.GetByIdAsync(command.ConversationId, cancellationToken);
        if (conversation is null)
        {
            return Result<ChatMessageResponse>.Failure(
                Error.NotFound($"Conversation {command.ConversationId.Value} not found.", "CHAT.CONVERSATION.NOT_FOUND"));
        }

        if (!conversation.BelongsTo(ownerIdResult.Value))
        {
            return Result<ChatMessageResponse>.Failure(
                Error.Forbidden("Conversation does not belong to the current user.", "CHAT.CONVERSATION.ACCESS_DENIED"));
        }

        // 3) Append user message - Updated method name with enhanced domain validation
        var userMessageResult = conversation.AppendUserMessageToConversation(command.Content, _timeProvider);
        if (userMessageResult.IsFailure)
            return Result<ChatMessageResponse>.Failure(userMessageResult.Error);

        var userMessageId = userMessageResult.Value.Id.Value;

        await _repository.UpdateAsync(conversation, cancellationToken);
        await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "User message {MessageId} appended to conversation {ConversationId}",
            userMessageId, conversation.Id.Value);

        // 4) Resolve MCP servers (optional, non-failing)
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

        // 5) Build AI request and process
        var previousResponseId = conversation.GetLastAiResponseId();
        _logger.LogInformation(
            "Building AI request for conversation {ConversationId} with previousResponseId: {PreviousResponseId}",
            conversation.Id.Value,
            previousResponseId ?? "none");

        var aiRequest = new AiRequest(
            Message: command.Content.Value,
            McpConfigs: mcpConfigs,
            PreviousResponseId: previousResponseId);

        try
        {
            var aiTimer = System.Diagnostics.Stopwatch.StartNew();
            var ai = await _aiClient.ProcessMessageAsync(aiRequest, cancellationToken);
            aiTimer.Stop();
            _telemetry?.TrackAiClientRequest("responses.mcp", aiTimer.Elapsed, ai.IsSuccess);

            if (ai.IsFailure)
            {
                _logger.LogWarning(
                    "AI call failed for conversation {ConversationId}: {ErrorCode}",
                    conversation.Id.Value, ai.Error.Code);

                overall.Stop();
                _telemetry?.TrackMessageProcessed(conversation.Id.Value, overall.Elapsed, success: false);

                return Result<ChatMessageResponse>.Failure(
                    Error.Internal("AI processing failed. Please retry.", "CHAT.AI.PROCESSING_FAILED"));
            }

            // Validate AI response
            if (string.IsNullOrWhiteSpace(ai.Value.Content) || string.IsNullOrWhiteSpace(ai.Value.ResponseId))
            {
                _logger.LogWarning(
                    "AI returned invalid response for conversation {ConversationId}.",
                    conversation.Id.Value);

                overall.Stop();
                _telemetry?.TrackMessageProcessed(conversation.Id.Value, overall.Elapsed, success: false);

                return Result<ChatMessageResponse>.Failure(
                    Error.Internal("AI returned invalid response. Please retry.", "CHAT.AI.INVALID_RESPONSE"));
            }

            // 6) Append assistant message - Updated method name with enhanced domain validation
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
                "Assistant message {MessageId} appended to conversation {ConversationId}. Previous AI response: {PreviousResponseId} -> New AI response: {NewResponseId}",
                assistantMessageId, 
                conversation.Id.Value, 
                previousResponseId ?? "none",
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
            _logger.LogWarning("AI call cancelled for conversation {ConversationId}", conversation.Id.Value);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error processing message for conversation {ConversationId}",
                conversation.Id.Value);

            return Result<ChatMessageResponse>.Failure(
                Error.Internal("An unexpected error occurred. Please retry.", "CHAT.AI.UNEXPECTED_ERROR"));
        }
    }
}