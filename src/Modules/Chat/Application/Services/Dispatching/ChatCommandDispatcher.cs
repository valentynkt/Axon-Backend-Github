using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.Commands.AppendUserMessage;
using Axon.Modules.Chat.Application.Commands.StartConversation;
using Axon.Modules.Chat.Application.DTOs.Requests;
using Axon.Modules.Chat.Application.DTOs.Responses;
using Axon.Modules.Chat.Application.Contracts.Dispatching;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Application.Services.Dispatching;

/// <summary>
/// Translates application-level requests into domain commands and routes via MediatR.
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
        if (!MessageContent.TryParse(request.Message, provider: null, out var msg))
            return Result.Failure<ProcessMessageResponse, Error>(
                Error.Validation("Message content is invalid.", "CHAT.MESSAGE.INVALID"));

        var cmd = new StartConversationCommand(Message: msg);
        // Handlers already return Result<ProcessMessageResponse, Error>
        return await _mediator.Send(cmd, ct);
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

        if (!MessageContent.TryParse(request.Message, provider: null, out var msg))
            return Result.Failure<ProcessMessageResponse, Error>(
                Error.Validation("Message content is invalid.", "CHAT.MESSAGE.INVALID"));

        var conversationId = new ConversationId(guid);
        var cmd = new AppendUserMessageCommand(ConversationId: conversationId, Content: msg);

        // Handlers already return Result<ProcessMessageResponse, Error>
        return await _mediator.Send(cmd, ct);
    }
}
