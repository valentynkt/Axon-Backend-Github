using BuildingBlocks.Core.Functional.Results;
using Axon.Api.Contracts.Chat;
using Axon.Modules.Chat.Application.Commands.StartConversation;
using Axon.Modules.Chat.Application.Commands.AppendUserMessage;
using Axon.Modules.Chat.Primitives.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Application.Services;

/// <summary>
/// Default implementation of chat command dispatcher.
/// </summary>
public sealed class ChatCommandDispatcher : IChatCommandDispatcher
{
    private readonly IMediator _mediator;
    private readonly ILogger<ChatCommandDispatcher> _logger;

    public ChatCommandDispatcher(
        IMediator mediator,
        ILogger<ChatCommandDispatcher> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<ProcessMessageResponse>> ProcessMessageAsync(
        ProcessMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Processing message request (isNewConversation={IsNewConversation})",
            request.ConversationId == null);

        try
        {
            if (request.ConversationId == null)
            {
                return await StartNewConversationAsync(request, cancellationToken);
            }

            return await AppendToExistingConversationAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error dispatching chat command");
            return Result<ProcessMessageResponse>.Failure(
                BuildingBlocks.Core.Diagnostics.Errors.Error.Unexpected(
                    "CHAT_DISPATCH_ERROR",
                    "An unexpected error occurred while processing the message"));
        }
    }

    private async Task<Result<ProcessMessageResponse>> StartNewConversationAsync(
        ProcessMessageRequest request,
        CancellationToken cancellationToken)
    {
        var messageResult = MessageContent.Create(request.Message);
        if (messageResult.IsFailure)
            return Result<ProcessMessageResponse>.Failure(messageResult.Error);

        var command = new StartConversationCommand(Message: messageResult.Value);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
            return Result<ProcessMessageResponse>.Failure(result.Error);

        var response = new ProcessMessageResponse
        {
            Success = true,
            Content = result.Value.AssistantMessage,
            ConversationId = result.Value.ConversationId.Value,
            ResponseId = result.Value.AssistantMessageId.Value.ToString(),
            Timestamp = DateTime.UtcNow
        };

        return Result<ProcessMessageResponse>.Success(response);
    }

    private async Task<Result<ProcessMessageResponse>> AppendToExistingConversationAsync(
        ProcessMessageRequest request,
        CancellationToken cancellationToken)
    {
        // Validate ConversationId
        ConversationId conversationId;
        try
        {
            conversationId = ConversationId.From(request.ConversationId!.Value);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid ConversationId provided: {ConversationId}", request.ConversationId);
            return Result<ProcessMessageResponse>.Failure(
                BuildingBlocks.Core.Diagnostics.Errors.Error.Validation(
                    "INVALID_CONVERSATION_ID",
                    "The provided conversation ID is invalid"));
        }

        var messageResult = MessageContent.Create(request.Message);
        if (messageResult.IsFailure)
            return Result<ProcessMessageResponse>.Failure(messageResult.Error);

        var command = new AppendUserMessageCommand(
            ConversationId: conversationId,
            Content: messageResult.Value);

        var result = await _mediator.Send(command, cancellationToken);
        if (result.IsFailure)
            return Result<ProcessMessageResponse>.Failure(result.Error);

        var response = new ProcessMessageResponse
        {
            Success = true,
            Content = result.Value.AssistantMessage,
            ConversationId = result.Value.ConversationId.Value,
            ResponseId = result.Value.AssistantMessageId.Value.ToString(),
            Timestamp = DateTime.UtcNow
        };

        return Result<ProcessMessageResponse>.Success(response);
    }
}