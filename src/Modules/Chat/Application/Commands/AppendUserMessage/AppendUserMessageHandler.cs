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
using Axon.Modules.Chat.Domain.Rules;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Diagnostics.Exceptions;
using BuildingBlocks.Core.Domain.Rules;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<AppendUserMessageHandler> _logger;

    public AppendUserMessageHandler(
        IConversationRepository repository,
        IUserAuthenticationService authenticationService,
        IMessageProcessingOrchestrator messageOrchestrator,
        TimeProvider timeProvider,
        ILogger<AppendUserMessageHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
        _messageOrchestrator = messageOrchestrator ?? throw new ArgumentNullException(nameof(messageOrchestrator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<ProcessMessageResponse, Error>> Handle(
        AppendUserMessageCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing append user message command for conversation {ConversationId}",
            command.ConversationId.Value);

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
        // Use domain rule for ConversationId validation
        var idValidationResult = Conversation.ValidateConversationId(conversationId);
        if (idValidationResult.IsFailure)
            return Result.Failure<Conversation, Error>(idValidationResult.Error);

        // Load conversation
        var conversation = await _repository.GetByIdAsync(conversationId, cancellationToken);
        
        // Use domain rule for conversation existence check
        try
        {
            CheckRule(new ConversationMustExistRule(conversation, conversationId));
        }
        catch (BusinessRuleException ex)
        {
            return Result.Failure<Conversation, Error>(
                Error.NotFound(ex.Message, ex.Error.Code));
        }

        // Use domain rule for ownership validation
        var accessValidationResult = conversation!.ValidateAccess(ownerId);
        if (accessValidationResult.IsFailure)
            return Result.Failure<Conversation, Error>(accessValidationResult.Error);

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

        _logger.LogInformation(
            "User message {MessageId} appended to conversation {ConversationId}",
            userMessageId.Value, conversation.Id.Value);

        // Delegate to orchestrator for AI processing and assistant response
        return await _messageOrchestrator.ProcessUserMessageAsync(
            conversation, userMessage, userMessageId, cancellationToken);
    }

    
#pragma warning disable CA1859 // Change type of parameter for improved performance
    private static void CheckRule(IBusinessRule rule)
#pragma warning restore CA1859
    {
        if (rule.IsBroken())
            throw new BusinessRuleException(rule);
    }
}
