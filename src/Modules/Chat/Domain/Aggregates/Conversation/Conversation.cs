using Axon.BuildingBlocks.Core.Constants;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Diagnostics.Exceptions;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.Events;
using Axon.Modules.Chat.Domain.Rules;
using Axon.Modules.Chat.Domain.Internal.Text;
using Axon.Modules.Chat.Domain.ValueObjects;
using BuildingBlocks.Core.Domain.Entities.Base;
using BuildingBlocks.Primitives.Ids;
using MediatR;

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
    public string? Title { get; private set; }
    
    /// <summary>
    /// The anchor for the next model call (use as 'previous_response_id').
    /// Set only when an assistant message is appended successfully.
    /// </summary>
    public AiResponseId? LastAiResponseId { get; private set; }
    
    
    // Computed properties
    public int MessageCount => _messages.Count;
    public IReadOnlyList<Message> MessagesOrdered => _messages.AsReadOnly();
    
    // Helpers
    public bool IsActive => Status == ConversationStatus.Active;
    public bool HasDefaultTitle => Title is null;

    private Conversation() : base(ConversationId.New())
    {
        // Required for EF Core
        Title = null;
        OwnerId = UserId.New(); // Temporary for EF
    }

    private Conversation(
        ConversationId id,
        UserId ownerId,
        string? title)
        : base(id)
    {
        OwnerId = ownerId;
        Status = ConversationStatus.Active;
        Title = title;
    }

    /// <summary>
    /// Starts a new conversation with the specified owner.
    /// </summary>
    public static Result<Conversation> StartNewConversation(
        UserId ownerId,
        string? titleOrNull,
        TimeProvider timeProvider)
    {
        try
        {
            // Validate inputs with more specific error messages
            CheckRule(new ConversationMustHaveOwnerRule(ownerId));
            CheckRule(new TitleProvidedMustBeValidRule(titleOrNull));

            var conversationId = ConversationId.New();
            var now = timeProvider.GetUtcNow();
            
            // Process title - if provided, validate it; if null/empty, keep as null
            string? processedTitle = null;
            if (!string.IsNullOrEmpty(titleOrNull))
            {
                var titleResult = ConversationTitle.Create(titleOrNull);
                if (titleResult.IsFailure)
                    return Result<Conversation>.Failure(titleResult.Error);
                
                processedTitle = titleResult.Value.Value;
            }

            // Create conversation
            var conversation = new Conversation(conversationId, ownerId, processedTitle);

            // Raise domain event
            conversation.RaiseDomainEvent(new ConversationStartedEvent(
                conversationId, ownerId, processedTitle, now));

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
    public Result<Message> AppendUserMessageToConversation(MessageContent content, TimeProvider timeProvider)
    {
        try
        {
            // Enhanced validation with specific business rule enforcement
            ValidateMessageAppendPreconditions(content.Value, MessageRole.User);
            
            // Additional domain rule: Ensure conversation can accept more messages
            CheckRule(new ConversationCanAcceptMoreMessagesRule(_messages.Count));
            
            // Additional domain rule: Validate message content meets domain standards
            CheckRule(new MessageContentMeetsDomainStandardsRule(content.Value));

            var now = timeProvider.GetUtcNow();
            var message = CreateAndAddUserMessage(content);
            
            RaiseUserMessageEvent(message, content.Value, now);

            #if DEBUG
            // Verify message sequence integrity as additional safety check during development
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
    private Message CreateAndAddUserMessage(MessageContent content)
    {
        var sequence = MessageCount + 1;
        var message = Message.CreateUserMessage(Id, content, sequence);
        
        _messages.Add(message);
        MarkUpdated();
        
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
    /// If a message with the same AiResponseId exists, returns that message without 
    /// mutating timestamps or emitting new events. This guarantees at-least-once 
    /// delivery semantics are safe to retry. Assistant must follow user (enforced by turn-taking rule).
    /// </summary>
    public Result<Message> AppendAssistantResponseToConversation(
        MessageContent content, 
        AiResponseId aiResponseId,
        TimeProvider timeProvider)
    {
        try
        {
            // Enhanced validation with specific business rule enforcement
            ValidateMessageAppendPreconditions(content.Value, MessageRole.Assistant);
            
            // Additional domain rule: Ensure conversation can accept more messages
            CheckRule(new ConversationCanAcceptMoreMessagesRule(_messages.Count));
            
            // Additional domain rule: Validate assistant response content standards  
            CheckRule(new AssistantResponseContentValidRule(content.Value));
            
            // Additional domain rule: AI Response ID must be unique and valid
            CheckRule(new AiResponseIdMustBeUniqueRule(aiResponseId, _messages));
            
            // Check for idempotency (existing behavior preserved)
            var existingMessage = FindExistingMessageByAiResponseId(aiResponseId);
            if (existingMessage != null)
                return Result<Message>.Success(existingMessage);

            var now = timeProvider.GetUtcNow();
            var message = CreateAndAddAssistantMessage(content, aiResponseId);
            
            RaiseAssistantMessageEvent(message, content.Value, aiResponseId, now);

            #if DEBUG
            // Verify message sequence integrity as additional safety check during development
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
    private Message CreateAndAddAssistantMessage(MessageContent content, AiResponseId aiResponseId)
    {
        var sequence = MessageCount + 1;
        var message = Message.CreateAssistantMessage(Id, content, sequence, aiResponseId);
        
        _messages.Add(message);
        LastAiResponseId = aiResponseId;
        MarkUpdated();
        
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
    public Result<Unit> UpdateTitle(string newTitleValue, TimeProvider timeProvider)
    {
        try
        {
            CheckRule(new ConversationMustBeActiveRule(Status));
            CheckRule(new TitleUpdateMustBeValidRule(newTitleValue));
            
            // Create the title value object - this should succeed given our validation
            var titleResult = ConversationTitle.Create(newTitleValue);
            if (titleResult.IsFailure)
                return Result<Unit>.Failure(titleResult.Error);
            
            var now = timeProvider.GetUtcNow();
            Title = titleResult.Value.Value;
            MarkUpdated();

            RaiseDomainEvent(new ConversationTitleUpdatedEvent(
                Id,
                Title,
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
    public Result<Unit> Complete(TimeProvider timeProvider)
    {
        try
        {
            CheckRule(new ConversationMustBeActiveRule(Status));
            CheckRule(new CompletionRequiresAtLeastOneMessageRule(MessageCount));

            var now = timeProvider.GetUtcNow();
            Status = ConversationStatus.Completed;
            MarkUpdated();

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
    /// Gets the last AI response ID for threading anchor in subsequent AI requests.
    /// Use this value as 'previous_response_id' in downstream AI requests.
    /// </summary>
    public string? GetLastAiResponseId() => LastAiResponseId?.ToString();

    /// <summary>
    /// Creates content preview - plain truncation to configured length, no ellipsis.
    /// </summary>
    private static string CreateContentPreview(string content)
    {
        return TextSlices.Preview(content, ChatPrimitiveConstants.Conversation.ContentPreviewLength);
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