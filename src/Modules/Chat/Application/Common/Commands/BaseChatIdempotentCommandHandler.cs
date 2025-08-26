using System;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Diagnostics.Exceptions;
using BuildingBlocks.Core.Domain.Rules;
using BuildingBlocks.Primitives.Ids;
using Axon.Modules.Chat.Application.Contracts.AI;
using Axon.Modules.Chat.Application.Contracts.Persistence;
using Axon.Modules.Chat.Application.DTOs.Responses;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Rules;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Application.Common.Commands;

/// <summary>
/// Base handler for idempotent Chat module commands.
/// Provides common infrastructure including dependencies, validation, logging, and business rule checking.
/// </summary>
/// <typeparam name="TCommand">The idempotent command type that inherits from ChatIdempotentCommand</typeparam>
/// <typeparam name="TResponse">The response type returned by the command</typeparam>
public abstract class BaseChatIdempotentCommandHandler<TCommand, TResponse> : BaseChatCommandHandler<TCommand, TResponse>
    where TCommand : ChatIdempotentCommand<TResponse>
    where TResponse : notnull
{
    protected IConversationRepository Repository { get; }
    protected IMessageProcessingOrchestrator MessageOrchestrator { get; }
    protected TimeProvider TimeProvider { get; }
    private readonly ILogger _logger;

    protected BaseChatIdempotentCommandHandler(
        ICurrentUserService currentUserService,
        IConversationRepository repository,
        IMessageProcessingOrchestrator messageOrchestrator,
        TimeProvider timeProvider,
        ILogger logger)
        : base(currentUserService)
    {
        Repository = repository ?? throw new ArgumentNullException(nameof(repository));
        MessageOrchestrator = messageOrchestrator ?? throw new ArgumentNullException(nameof(messageOrchestrator));
        TimeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Checks a business rule and throws BusinessRuleException if it's broken.
    /// </summary>
    /// <param name="rule">The business rule to check</param>
    protected static void CheckRule(IBusinessRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        
        if (rule.IsBroken())
            throw new BusinessRuleException(rule);
    }

    /// <summary>
    /// Checks a business rule and returns a Result with error if it's broken.
    /// </summary>
    /// <typeparam name="T">The success type</typeparam>
    /// <param name="rule">The business rule to check</param>
    /// <param name="successValue">Value to return if rule passes</param>
    /// <returns>Success with value or failure with error</returns>
    protected static Result<T, Error> CheckRuleWithResult<T>(IBusinessRule rule, T successValue)
    {
        ArgumentNullException.ThrowIfNull(rule);
        
        return rule.IsBroken() 
            ? Result.Failure<T, Error>(Error.BusinessRule(rule.Message, rule.Code, rule.Metadata))
            : Result.Success<T, Error>(successValue);
    }

    /// <summary>
    /// Loads and validates a conversation for the current user.
    /// </summary>
    /// <param name="conversationId">The conversation ID to load</param>
    /// <param name="ownerId">The owner's user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The validated conversation or an error</returns>
    protected async Task<Result<Conversation, Error>> LoadAndValidateConversationAsync(
        ConversationId conversationId,
        UserId ownerId,
        CancellationToken cancellationToken)
    {
        // Validate conversation ID using domain rules
        var idValidationResult = Conversation.ValidateConversationId(conversationId);
        if (idValidationResult.IsFailure)
            return Result.Failure<Conversation, Error>(idValidationResult.Error);

        // Load conversation
        var conversation = await Repository.GetByIdAsync(conversationId, cancellationToken);
        
        // Check existence using business rule
        try
        {
            CheckRule(new ConversationMustExistRule(conversation, conversationId));
        }
        catch (BusinessRuleException ex)
        {
            return Result.Failure<Conversation, Error>(
                Error.NotFound(ex.Message, ex.Error.Code));
        }

        // Validate access
        var accessResult = conversation!.ValidateAccess(ownerId);
        if (accessResult.IsFailure)
            return Result.Failure<Conversation, Error>(accessResult.Error);

        return Result.Success<Conversation, Error>(conversation);
    }

    /// <summary>
    /// Creates a new conversation for the specified owner.
    /// </summary>
    /// <param name="ownerId">The user ID of the conversation owner</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created conversation or an error</returns>
    protected async Task<Result<Conversation, Error>> CreateConversationAsync(
        UserId ownerId,
        CancellationToken cancellationToken)
    {
        // Create conversation using domain factory
        var conversationResult = Conversation.StartNewConversation(ownerId, titleOrNull: null, TimeProvider);
        if (conversationResult.IsFailure)
            return Result.Failure<Conversation, Error>(conversationResult.Error);

        var conversation = conversationResult.Value;

        // Persist the new conversation (don't save yet - will be saved after message processing)
        await Repository.AddAsync(conversation, cancellationToken);

        _logger.LogInformation("New conversation {ConversationId} created for user {UserId}",
            conversation.Id.Value, ownerId.Value);

        return Result.Success<Conversation, Error>(conversation);
    }

    /// <summary>
    /// Saves changes to the repository with unified error handling.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success or failure result</returns>
    protected async Task<Result<Unit, Error>> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Repository.UnitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success<Unit, Error>(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save changes");
            return Result.Failure<Unit, Error>(Error.Failure("Failed to save changes", ex.Message));
        }
    }

    /// <summary>
    /// Processes a user message through the complete workflow.
    /// </summary>
    /// <param name="conversation">The conversation to process</param>
    /// <param name="userMessage">The user message content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The processing result</returns>
    protected async Task<Result<ProcessMessageResponse, Error>> ProcessUserMessageAsync(
        Conversation conversation,
        MessageContent userMessage,
        CancellationToken cancellationToken)
    {
        // Append user message to conversation
        var userMessageResult = conversation.AppendUserMessageToConversation(userMessage, TimeProvider);
        if (userMessageResult.IsFailure)
            return Result.Failure<ProcessMessageResponse, Error>(userMessageResult.Error);

        var userMessageId = userMessageResult.Value.Id;

        // Persist the message
        await Repository.UpdateAsync(conversation, cancellationToken);
        var saveResult = await SaveChangesAsync(cancellationToken);
        if (saveResult.IsFailure)
            return Result.Failure<ProcessMessageResponse, Error>(saveResult.Error);

        _logger.LogInformation("User message {MessageId} added to conversation {ConversationId}",
            userMessageId.Value, conversation.Id.Value);

        // Delegate to orchestrator for AI processing
        return await MessageOrchestrator.ProcessUserMessageAsync(
            conversation, userMessage, userMessageId, cancellationToken);
    }

    /// <summary>
    /// Gets the logger for derived classes.
    /// </summary>
    protected ILogger Logger => _logger;

    /// <summary>
    /// Optional: Override to add idempotent command-specific pre-processing logic.
    /// </summary>
    protected virtual Task PreProcessIdempotentCommandAsync(TCommand command, CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Optional: Override to add idempotent command-specific post-processing logic.
    /// </summary>
    protected virtual Task PostProcessIdempotentCommandAsync(TCommand command, TResponse response, CancellationToken cancellationToken)
        => Task.CompletedTask;
}