// /Users/valentynkit/Repos/Axon-Backend/src/Modules/Chat/Application/Commands/AppendUserMessage/AppendUserMessageHandler.cs
#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.Contracts.Authentication;
using Axon.Modules.Chat.Application.Contracts.AI;
using Axon.Modules.Chat.Application.Contracts.Persistence;
using Axon.Modules.Chat.Application.DTOs.Responses;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Modules.Chat.Application.Commands.AppendUserMessage;

/// <summary>
/// Appends a user message and orchestrates the assistant reply processing.
/// Idempotency is handled by pipeline behavior, not here.
/// </summary>
public sealed class AppendUserMessageHandler
    : IRequestHandler<AppendUserMessageCommand, Result<ProcessMessageResponse, Error>>
{
    private readonly IConversationRepository _repository;
    private readonly IUserAuthenticationService _authenticationService;
    private readonly IMessageProcessingOrchestrator _messageOrchestrator;
    private readonly TimeProvider _timeProvider;

    public AppendUserMessageHandler(
        IConversationRepository repository,
        IUserAuthenticationService authenticationService,
        IMessageProcessingOrchestrator messageOrchestrator,
        TimeProvider timeProvider)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
        _messageOrchestrator = messageOrchestrator ?? throw new ArgumentNullException(nameof(messageOrchestrator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<Result<ProcessMessageResponse, Error>> Handle(
        AppendUserMessageCommand command,
        CancellationToken cancellationToken)
    {
        // Authenticate user using CFE chaining
        return await _authenticationService.GetAuthenticatedUserId()
            .Bind(async ownerId => await ValidateAndLoadConversation(command.ConversationId, ownerId, cancellationToken))
            .Bind(async conversation => await AppendUserMessageAndProcess(conversation, command.Content, cancellationToken));
    }

    private async Task<Result<Conversation, Error>> ValidateAndLoadConversation(
        ConversationId conversationId,
        UserId ownerId,
        CancellationToken cancellationToken)
    {
        // Validate conversation id
        if (conversationId.Value == Guid.Empty)
        {
            return Result.Failure<Conversation, Error>(
                Error.Validation("ConversationId cannot be empty.", "CHAT.ID.EMPTY"));
        }

        // Load conversation
        var conversation = await _repository.GetByIdAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            return Result.Failure<Conversation, Error>(
                Error.NotFound($"Conversation {conversationId.Value} not found.", "CHAT.CONVERSATION.NOT_FOUND"));
        }

        // Validate ownership
        if (!conversation.BelongsTo(ownerId))
        {
            return Result.Failure<Conversation, Error>(
                Error.Forbidden("Conversation does not belong to the current user.", "CHAT.CONVERSATION.ACCESS_DENIED"));
        }

        return Result.Success<Conversation, Error>(conversation);
    }

    private async Task<Result<ProcessMessageResponse, Error>> AppendUserMessageAndProcess(
        Conversation conversation,
        MessageContent userMessage,
        CancellationToken cancellationToken)
    {
        // Append user message (domain handles validation and rules)
        var userMessageResult = conversation.AppendUserMessageToConversation(userMessage, _timeProvider);
        if (userMessageResult.IsFailure)
            return Result.Failure<ProcessMessageResponse, Error>(userMessageResult.Error);

        var userMessageId = userMessageResult.Value.Id;

        // Persist user message
        await _repository.UpdateAsync(conversation, cancellationToken);
        await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

        // Delegate to orchestrator for AI processing and assistant response
        return await _messageOrchestrator.ProcessUserMessageAsync(
            conversation, userMessage, userMessageId, cancellationToken);
    }
}
