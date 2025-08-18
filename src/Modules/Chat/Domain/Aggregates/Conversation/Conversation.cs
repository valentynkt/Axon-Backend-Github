using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Diagnostics.Exceptions;
using BuildingBlocks.Core.Domain.Primitives;
using BuildingBlocks.Core.Domain.Rules;
using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Results;
using Axon.Modules.Chat.Domain.Constants;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.Events;
using Axon.Modules.Chat.Domain.Rules;
using Axon.Modules.Chat.Domain.Time;
using Axon.Modules.Chat.Domain.Internal.Text;
using Axon.Modules.Chat.Domain.ValueObjects;
using BuildingBlocks.Core.Domain.Entities.Base;

namespace Axon.Modules.Chat.Domain.Aggregates.Conversation;

/// <summary>
/// Conversation aggregate root representing a chat conversation.
/// Enforces business rules and maintains conversation integrity.
/// </summary>
public sealed class Conversation : AggregateRoot<ConversationId>
{
    private readonly List<Message> _messages = new();
    
    // Core state
    public UserId OwnerId { get; private set; }
    public ConversationStatus Status { get; private set; }
    public string Title { get; private set; }
    public bool IsDefaultTitle { get; private set; }
    
    // AI context tracking
    public AiResponseId? LastAiResponseId { get; private set; }
    
    
    // Computed properties
    public int MessageCount => _messages.Count;
    public IReadOnlyList<Message> MessagesOrdered => _messages.AsReadOnly();
    
    // Helpers
    public bool IsActive => Status == ConversationStatus.Active;

    private Conversation() : base(ConversationId.New())
    {
        // Required for EF Core
        Title = string.Empty;
        OwnerId = UserId.New(); // Temporary for EF
    }

    private Conversation(
        ConversationId id,
        UserId ownerId,
        string title,
        bool isDefaultTitle)
        : base(id)
    {
        OwnerId = ownerId;
        Status = ConversationStatus.Active;
        Title = title;
        IsDefaultTitle = isDefaultTitle;
    }

    /// <summary>
    /// Starts a new conversation with the specified owner.
    /// </summary>
    public static Result<Conversation> Start(
        UserId ownerId,
        string? titleOrNull,
        IClock clock)
    {
        try
        {
            // Validate inputs
            CheckRule(new ConversationMustHaveOwnerRule(ownerId));
            CheckRule(new TitleProvidedMustBeValidRule(titleOrNull));

            var conversationId = ConversationId.New();
            var now = clock.UtcNow;
            
            // Process title
            var titleResult = ProcessStartTitle(titleOrNull);
            if (titleResult.IsFailure)
                return Result<Conversation>.Failure(titleResult.Error);

            var (actualTitle, isDefault) = titleResult.Value;

            // Create conversation
            var conversation = new Conversation(conversationId, ownerId, actualTitle, isDefault);

            // Raise domain event
            conversation.RaiseDomainEvent(new ConversationStartedEvent(
                conversationId, ownerId, actualTitle, isDefault, now));

            return Result<Conversation>.Success(conversation);
        }
        catch (BusinessRuleException ex)
        {
            return Result<Conversation>.Failure(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
    }

    /// <summary>
    /// Processes title for conversation start, handling default vs user-provided scenarios.
    /// </summary>
    private static Result<(string actualTitle, bool isDefault)> ProcessStartTitle(string? titleOrNull)
    {
        if (string.IsNullOrEmpty(titleOrNull))
        {
            // Default title path
            return Result<(string, bool)>.Success((string.Empty, true));
        }

        // User-provided title path - validate with VO
        var titleResult = ConversationTitle.Create(titleOrNull);
        if (titleResult.IsFailure)
            return Result<(string, bool)>.Failure(titleResult.Error);

        return Result<(string, bool)>.Success((titleResult.Value.Value, false));
    }

    /// <summary>
    /// Appends a user message to the conversation.
    /// </summary>
    public Result<Message> AppendUserMessage(MessageContent content, IClock clock)
    {
        try
        {
            // Validate preconditions (APP-01: block user→user)
            ValidateMessageAppendPreconditions(content.Value, MessageRole.User);

            var now = clock.UtcNow;
            var message = CreateAndAddUserMessage(content, now);
            
            RaiseUserMessageEvent(message, content.Value, now);

            #if DEBUG
            CheckRule(new MessageSequenceIntegrityRule(_messages));
            #endif
            return Result<Message>.Success(message);
        }
        catch (BusinessRuleException ex)
        {
            return Result<Message>.Failure(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
    }

    /// <summary>
    /// Creates user message and adds to conversation.
    /// </summary>
    private Message CreateAndAddUserMessage(MessageContent content, DateTimeOffset now)
    {
        var sequence = MessageCount + 1;
        var message = Message.CreateUserMessage(Id, content, sequence);
        
        _messages.Add(message);
        UpdatedAt = now;
        
        return message;
    }

    /// <summary>
    /// Raises the user message appended domain event.
    /// </summary>
    private void RaiseUserMessageEvent(Message message, string content, DateTimeOffset now)
    {
        var contentPreview = CreateContentPreview(content);
        RaiseDomainEvent(new UserMessageAppendedEvent(
            Id, message.Id, message.Sequence, contentPreview, now));
    }



    /// <summary>
    /// Appends an assistant message with AI response tracking.
    /// Ensures idempotency and maintains conversation context.
    /// </summary>
    public Result<Message> AppendAssistantMessage(
        MessageContent content, 
        AiResponseId aiResponseId,
        IClock clock)
    {
        try
        {
            // Validate preconditions
            ValidateMessageAppendPreconditions(content.Value, MessageRole.Assistant);
            
            // Check for idempotency
            var existingMessage = FindExistingMessageByAiResponseId(aiResponseId);
            if (existingMessage != null)
                return Result<Message>.Success(existingMessage);

            var now = clock.UtcNow;
            var message = CreateAndAddAssistantMessage(content, aiResponseId, now);
            
            RaiseAssistantMessageEvent(message, content.Value, aiResponseId, now);

            #if DEBUG
            CheckRule(new MessageSequenceIntegrityRule(_messages));
            #endif
            return Result<Message>.Success(message);
        }
        catch (BusinessRuleException ex)
        {
            return Result<Message>.Failure(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
    }

    /// <summary>
    /// Validates common preconditions for appending messages.
    /// </summary>
    private void ValidateMessageAppendPreconditions(string content, MessageRole role)
    {
        CheckRule(new ConversationMustBeActiveRule(Status));
        CheckRule(new MessageContentWithinLimitsRule(content));
        CheckRule(new ConversationMessageLimitRule(MessageCount));
        CheckRule(new MessageTurnTakingRule(_messages, role));
    }

    /// <summary>
    /// Finds existing message by AI response ID for idempotency check.
    /// </summary>
    private Message? FindExistingMessageByAiResponseId(AiResponseId aiResponseId)
    {
        return _messages.FirstOrDefault(m => m.AiResponseId?.Equals(aiResponseId) == true);
    }

    /// <summary>
    /// Creates assistant message and adds to conversation.
    /// </summary>
    private Message CreateAndAddAssistantMessage(MessageContent content, AiResponseId aiResponseId, DateTimeOffset now)
    {
        var sequence = MessageCount + 1;
        var message = Message.CreateAssistantMessage(Id, content, sequence, aiResponseId);
        
        _messages.Add(message);
        LastAiResponseId = aiResponseId;
        UpdatedAt = now;
        
        return message;
    }

    /// <summary>
    /// Raises the assistant message appended domain event.
    /// </summary>
    private void RaiseAssistantMessageEvent(Message message, string content, AiResponseId aiResponseId, DateTimeOffset now)
    {
        var contentPreview = CreateContentPreview(content);
        RaiseDomainEvent(new AssistantMessageAppendedEvent(
            Id, message.Id, message.Sequence, contentPreview, aiResponseId, now));
    }

    /// <summary>
    /// Updates the conversation title with a user-provided value.
    /// For title updates, empty titles are not allowed (unlike creation).
    /// </summary>
    public Result<Unit> UpdateTitle(string newTitleValue, IClock clock)
    {
        try
        {
            CheckRule(new ConversationMustBeActiveRule(Status));
            CheckRule(new TitleUpdateMustBeValidRule(newTitleValue));
            
            // Create the title value object - this should succeed given our validation
            var titleResult = ConversationTitle.Create(newTitleValue);
            if (titleResult.IsFailure)
                return Result<Unit>.Failure(titleResult.Error);
            
            var now = clock.UtcNow;
            Title = titleResult.Value.Value;
            IsDefaultTitle = false;
            UpdatedAt = now;

            RaiseDomainEvent(new ConversationTitleUpdatedEvent(
                Id,
                Title,
                IsDefaultTitle,
                now));

            return Result<Unit>.Success(Unit.Value);
        }
        catch (BusinessRuleException ex)
        {
            return Result<Unit>.Failure(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
    }

    /// <summary>
    /// Completes the conversation, preventing further messages.
    /// </summary>
    public Result<Unit> Complete(IClock clock)
    {
        try
        {
            CheckRule(new ConversationMustBeActiveRule(Status));
            CheckRule(new CompletionRequiresAtLeastOneMessageRule(MessageCount));

            var now = clock.UtcNow;
            Status = ConversationStatus.Completed;
            UpdatedAt = now;

            RaiseDomainEvent(new ConversationCompletedEvent(
                Id,
                MessageCount,
                now));

            return Result<Unit>.Success(Unit.Value);
        }
        catch (BusinessRuleException ex)
        {
            return Result<Unit>.Failure(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
    }

    /// <summary>
    /// Checks if the conversation belongs to a specific user.
    /// </summary>
    public bool BelongsTo(UserId userId) => OwnerId == userId;

    /// <summary>
    /// Gets the previous AI response ID for context linking.
    /// Used by application layer to maintain conversation continuity with OpenAI.
    /// </summary>
    public string? GetPreviousResponseId() => LastAiResponseId?.Value;

    /// <summary>
    /// Creates content preview - plain truncation to configured length, no ellipsis.
    /// </summary>
    private static string CreateContentPreview(string content)
    {
        return TextSlices.Preview(content, ChatDomainConstants.Conversation.ContentPreviewLength);
    }

    /// <summary>
    /// Checks a business rule and throws if violated.
    /// </summary>
    private static void CheckRule(IBusinessRule rule)
    {
        if (rule.IsBroken())
            throw new BusinessRuleException(rule);
    }
}