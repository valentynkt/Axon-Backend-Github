// /Users/valentynkit/Repos/Axon-Backend/src/Modules/Chat/Application/Commands/StartConversation/StartConversationHandler.cs
#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.Abstractions;
using Axon.Modules.Chat.Application.Abstractions.Persistence;
using Axon.Modules.Chat.Application.Common;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Application.Commands.StartConversation;

/// <summary>
/// Starts a new conversation for the current user, appends the user's first message,
/// and orchestrates the AI processing and assistant reply. Returns a unified response.
/// Idempotency is handled by pipeline behavior.
/// </summary>
public sealed class StartConversationHandler
    : IRequestHandler<StartConversationCommand, Result<ProcessMessageResponse, Error>>
{
    private readonly IConversationRepository _repository;
    private readonly IUserAuthenticationService _authenticationService;
    private readonly IMessageProcessingOrchestrator _messageOrchestrator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StartConversationHandler> _logger;

    public StartConversationHandler(
        IConversationRepository repository,
        IUserAuthenticationService authenticationService,
        IMessageProcessingOrchestrator messageOrchestrator,
        TimeProvider timeProvider,
        ILogger<StartConversationHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
        _messageOrchestrator = messageOrchestrator ?? throw new ArgumentNullException(nameof(messageOrchestrator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<ProcessMessageResponse, Error>> Handle(
        StartConversationCommand command,
        CancellationToken cancellationToken)
    {
        // Authenticate user and create conversation using CFE chaining
        return await _authenticationService.GetAuthenticatedUserId()
            .Bind(async ownerId => await CreateConversationWithUserMessage(ownerId, command.Message, cancellationToken));
    }

    private async Task<Result<ProcessMessageResponse, Error>> CreateConversationWithUserMessage(
        UserId ownerId,
        MessageContent userMessage,
        CancellationToken cancellationToken)
    {
        // Start new conversation (title is optional -> null)
        var startResult = Conversation.StartNewConversation(ownerId, titleOrNull: null, _timeProvider);
        if (startResult.IsFailure)
            return Result.Failure<ProcessMessageResponse, Error>(startResult.Error);

        var conversation = startResult.Value;

        // Append user's first message (domain enforces turn-taking & limits)
        var userMessageResult = conversation.AppendUserMessageToConversation(userMessage, _timeProvider);
        if (userMessageResult.IsFailure)
            return Result.Failure<ProcessMessageResponse, Error>(userMessageResult.Error);

        var userMessageId = userMessageResult.Value.Id;

        // Persist the new conversation with the first user message
        await _repository.AddAsync(conversation, cancellationToken);
        await _repository.UnitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Conversation {ConversationId} started by user {OwnerId}. First message {MessageId} saved.",
            conversation.Id.Value, ownerId.Value, userMessageId.Value);

        // Delegate to orchestrator for AI processing and assistant response
        return await _messageOrchestrator.ProcessUserMessageAsync(
            conversation, userMessage, userMessageId, cancellationToken);
    }
}
