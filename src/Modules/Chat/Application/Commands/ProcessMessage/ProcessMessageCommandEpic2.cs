using Axon.Modules.Chat.Domain.Conversation;
using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using BuildingBlocks.Core.Domain.CQRS;
using BuildingBlocks.Core.Domain.Rules;
using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Validation;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Chat.Application.Commands.ProcessMessage;

/// <summary>
/// Epic 2 enhanced ProcessMessage command demonstrating full integration with Epic 2 domain patterns
/// and Epic 5 pipeline behaviors. This command will go through the complete pipeline:
/// 
/// 1. ObservabilityBehavior - telemetry and tracing
/// 2. LoggingBehavior - structured logging
/// 3. ValidationBehavior - FluentValidation structural validation
/// 4. DomainValidationBehavior - Epic 2 domain business rules validation
/// 5. CachingBehavior - skipped for commands
/// 6. RetryBehavior - retry on transient failures
/// 7. TransactionBehavior - database transaction + domain event dispatch
/// 8. Handler execution with Epic 2 domain patterns
/// </summary>
public sealed record ProcessMessageCommandEpic2 : DomainCommandBase<ProcessMessageResponse>, 
    IRetryableOperation, 
    ICacheInvalidatingCommand
{
    public string Message { get; init; } = string.Empty;
    public ConversationId? ConversationId { get; init; }
    public UserId UserId { get; init; } = default!;
    public string? PreviousResponseId { get; init; }

    public override Type GetAggregateType() => typeof(Conversation);

    public override Validation<Unit> ValidateDomainRules()
    {
        var rules = new RuleBuilder()
            .NotEmpty(Message, nameof(Message))
            .Must(Message.Length <= MessageContent.MaxLength, 
                "MESSAGE_TOO_LONG", 
                $"Message cannot exceed {MessageContent.MaxLength} characters")
            .NotNull(UserId, nameof(UserId))
            // Add more domain-specific rules as needed
            .Build();

        return rules.IsSuccess 
            ? Validation<Unit>.Valid(Unit.Value)
            : Validation<Unit>.Invalid(rules.Error);
    }

    // Epic 5 RetryBehavior integration
    public string? GetRetryPolicyName() => "StandardRetry";

    // Epic 5 Cache invalidation integration
    public IEnumerable<string> GetCacheTagsToInvalidate() 
    {
        if (ConversationId.HasValue)
            yield return $"Conversation:{ConversationId.Value}";
            
        yield return $"User:{UserId}";
        yield return "Conversations";
        yield return "Messages";
    }
}

/// <summary>
/// FluentValidation validator for structural validation (Epic 5 ValidationBehavior).
/// Works alongside Epic 2 domain rules validation in DomainValidationBehavior.
/// </summary>
public sealed class ProcessMessageCommandEpic2Validator : AbstractValidator<ProcessMessageCommandEpic2>
{
    public ProcessMessageCommandEpic2Validator()
    {
        RuleFor(x => x.Message)
            .NotEmpty()
            .WithMessage("Message is required")
            .MaximumLength(MessageContent.MaxLength)
            .WithMessage($"Message cannot exceed {MessageContent.MaxLength} characters");

        RuleFor(x => x.UserId)
            .NotNull()
            .WithMessage("User ID is required");

        RuleFor(x => x.ConversationId)
            .Must(BeValidConversationId)
            .WithMessage("Conversation ID must be valid")
            .When(x => x.ConversationId.HasValue);
    }

    private static bool BeValidConversationId(ConversationId? conversationId)
    {
        return conversationId?.Value != Guid.Empty;
    }
}

/// <summary>
/// Epic 2 + Epic 5 integrated command handler demonstrating best practices.
/// Shows proper use of domain patterns with pipeline behavior integration.
/// </summary>
public sealed class ProcessMessageCommandEpic2Handler : IRequestHandler<ProcessMessageCommandEpic2, Result<ProcessMessageResponse>>
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IAiClient _aiClient;
    private readonly ILogger<ProcessMessageCommandEpic2Handler> _logger;

    public ProcessMessageCommandEpic2Handler(
        IConversationRepository conversationRepository,
        IAiClient aiClient,
        ILogger<ProcessMessageCommandEpic2Handler> logger)
    {
        _conversationRepository = conversationRepository;
        _aiClient = aiClient;
        _logger = logger;
    }

    public async Task<Result<ProcessMessageResponse>> Handle(
        ProcessMessageCommandEpic2 request, 
        CancellationToken cancellationToken)
    {
        // ValidationBehavior has already validated structure
        // DomainValidationBehavior has already validated business rules
        // TransactionBehavior will wrap this in a transaction

        _logger.LogInformation(
            "Processing message for user {UserId}, conversation {ConversationId}",
            request.UserId, 
            request.ConversationId);

        try
        {
            // 1. Get or create conversation (Epic 2 domain patterns)
            var conversationResult = await GetOrCreateConversation(request, cancellationToken);
            if (conversationResult.IsFailure)
                return conversationResult.Error;

            var conversation = conversationResult.Value;

            // 2. Add user message using Epic 2 aggregate patterns
            var messageResult = conversation.AppendUserMessage(request.Message, request.UserId.Value);
            if (messageResult.IsFailure)
                return messageResult.Error;

            var userMessage = messageResult.Value;
            _logger.LogDebug("User message added: {MessageId}", userMessage.Id);

            // 3. Save conversation (domain events will be dispatched by TransactionBehavior)
            await _conversationRepository.SaveAsync(conversation, cancellationToken);

            // 4. Generate AI response
            var aiResponseResult = await GenerateAiResponse(conversation, cancellationToken);
            if (aiResponseResult.IsFailure)
                return aiResponseResult.Error;

            var response = aiResponseResult.Value;

            _logger.LogInformation(
                "Message processed successfully. Conversation: {ConversationId}, User Message: {UserMessageId}",
                conversation.Id, 
                userMessage.Id);

            return Result<ProcessMessageResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error processing message for user {UserId}, conversation {ConversationId}",
                request.UserId, 
                request.ConversationId);

            return Result<ProcessMessageResponse>.Failure(
                Error.Unexpected("An error occurred while processing the message", "MESSAGE_PROCESSING_ERROR"));
        }
    }

    private async Task<Result<Conversation>> GetOrCreateConversation(
        ProcessMessageCommandEpic2 request, 
        CancellationToken cancellationToken)
    {
        if (request.ConversationId.HasValue)
        {
            // Get existing conversation
            var existingConversation = await _conversationRepository.GetByIdAsync(
                request.ConversationId.Value, 
                cancellationToken);

            if (existingConversation == null)
                return Result<Conversation>.Failure(
                    Error.NotFound("Conversation not found", "CONVERSATION_NOT_FOUND"));

            // Validate ownership using Epic 2 domain rules
            if (!existingConversation.BelongsToUser(request.UserId.Value))
                return Result<Conversation>.Failure(
                    Error.Forbidden("User does not have access to this conversation", "CONVERSATION_ACCESS_DENIED"));

            return Result<Conversation>.Success(existingConversation);
        }
        else
        {
            // Create new conversation using Epic 2 factory pattern
            var title = GenerateConversationTitle(request.Message);
            var conversationResult = Conversation.Start(title, request.UserId.Value);
            
            if (conversationResult.IsFailure)
                return conversationResult;

            var conversation = conversationResult.Value;
            await _conversationRepository.AddAsync(conversation, cancellationToken);

            return Result<Conversation>.Success(conversation);
        }
    }

    private async Task<Result<ProcessMessageResponse>> GenerateAiResponse(
        Conversation conversation, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Build AI request with conversation context
            var aiRequest = BuildAiRequest(conversation);
            var aiResponse = await _aiClient.ProcessAsync(aiRequest, cancellationToken);

            return Result<ProcessMessageResponse>.Success(new ProcessMessageResponse
            {
                ConversationId = conversation.Id.Value,
                Response = aiResponse.Content,
                MessageId = Guid.NewGuid(), // This would be the AI message ID in a full implementation
                Metadata = aiResponse.Metadata
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate AI response for conversation {ConversationId}", conversation.Id);
            
            return Result<ProcessMessageResponse>.Failure(
                Error.External("Failed to generate AI response", "AI_RESPONSE_ERROR"));
        }
    }

    private string GenerateConversationTitle(string firstMessage)
    {
        // Generate a smart title from the first message
        var words = firstMessage.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var titleWords = words.Take(5).ToArray();
        var title = string.Join(" ", titleWords);
        
        if (title.Length > 50)
            title = title.Substring(0, 47) + "...";
            
        return string.IsNullOrWhiteSpace(title) ? "New Conversation" : title;
    }

    private AiRequest BuildAiRequest(Conversation conversation)
    {
        var messages = conversation.Messages
            .Select(m => new AiMessage
            {
                Role = m.Role.ToString().ToLowerInvariant(),
                Content = m.Content.Value
            })
            .ToList();

        return new AiRequest
        {
            Messages = messages,
            ConversationId = conversation.Id.Value.ToString(),
            UserId = conversation.OwnerId.Value
        };
    }
}

// Supporting types (would normally be in separate files)

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(ConversationId id, CancellationToken cancellationToken = default);
    Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default);
    Task SaveAsync(Conversation conversation, CancellationToken cancellationToken = default);
}

public interface IAiClient
{
    Task<AiResponse> ProcessAsync(AiRequest request, CancellationToken cancellationToken = default);
}

public sealed class AiRequest
{
    public List<AiMessage> Messages { get; set; } = new();
    public string ConversationId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
}

public sealed class AiMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public sealed class AiResponse
{
    public string Content { get; set; } = string.Empty;
    public Dictionary<string, object>? Metadata { get; set; }
}