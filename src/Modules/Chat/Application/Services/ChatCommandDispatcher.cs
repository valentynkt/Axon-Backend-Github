using Axon.Api.Contracts.Chat;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.Commands.AppendUserMessage;
using Axon.Modules.Chat.Application.Commands.StartConversation;
using Axon.Modules.Chat.Application.Common;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Application.Services;

/// <summary>
/// Translates API requests into domain commands and routes via MediatR.
/// Uses Result&lt;T, Error&gt; for clear success/failure without extra status flags.
/// </summary>
public sealed class ChatCommandDispatcher : IChatCommandDispatcher
{
    private readonly IMediator _mediator;
    private readonly ILogger<ChatCommandDispatcher> _logger;

    public ChatCommandDispatcher(IMediator mediator, ILogger<ChatCommandDispatcher> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<ProcessMessageResponse, Error>> ProcessMessageAsync(
        ProcessMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Processing chat message (isNewConversation={IsNew})",
            request.ConversationId is null);

        try
        {
            return request.ConversationId is null
                ? await StartNewConversationAsync(request, cancellationToken)
                : await AppendToExistingConversationAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error dispatching chat command");
            return Result.Failure<ProcessMessageResponse, Error>(
                Error.Internal("An unexpected error occurred while processing the message.", "CHAT_DISPATCH_ERROR"));
        }
    }

    private async Task<Result<ProcessMessageResponse, Error>> StartNewConversationAsync(
        ProcessMessageRequest request,
        CancellationToken ct)
    {
        var msg = MessageContent.Create(request.Message);
        if (msg.IsFailure)
            return Result.Failure<ProcessMessageResponse, Error>(msg.Error);

        var cmd = new StartConversationCommand(Message: msg.Value);
        var result = await _mediator.Send(cmd, ct); // Result<ChatMessageResponse, Error>

        if (result.IsFailure)
            return Result.Failure<ProcessMessageResponse, Error>(result.Error);

        var r = result.Value;
        var response = new ProcessMessageResponse(
            ConversationId: r.ConversationId,
            UserMessageId: r.UserMessageId,
            AssistantMessageId: r.AssistantMessageId,
            AssistantMessage: r.AssistantMessage);

        return Result.Success<ProcessMessageResponse, Error>(response);
    }

    private async Task<Result<ProcessMessageResponse, Error>> AppendToExistingConversationAsync(
        ProcessMessageRequest request,
        CancellationToken ct)
    {
        var guid = request.ConversationId!.Value;
        if (guid == Guid.Empty)
        {
            return Result.Failure<ProcessMessageResponse, Error>(
                Error.Validation("The provided conversation ID is invalid.", "INVALID_CONVERSATION_ID"));
        }

        var conversationId = new ConversationId(guid);

        var msg = MessageContent.Create(request.Message);
        if (msg.IsFailure)
            return Result.Failure<ProcessMessageResponse, Error>(msg.Error);

        var cmd = new AppendUserMessageCommand(
            ConversationId: conversationId,
            Content: msg.Value);

        var result = await _mediator.Send(cmd, ct); // Result<ChatMessageResponse, Error>

        if (result.IsFailure)
            return Result.Failure<ProcessMessageResponse, Error>(result.Error);

        var r = result.Value;
        var response = new ProcessMessageResponse(
            ConversationId: r.ConversationId,
            UserMessageId: r.UserMessageId,
            AssistantMessageId: r.AssistantMessageId,
            AssistantMessage: r.AssistantMessage);

        return Result.Success<ProcessMessageResponse, Error>(response);
    }
}
