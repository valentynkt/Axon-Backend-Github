using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Diagnostics.Exceptions;
using BuildingBlocks.Core.Domain.Primitives;
using BuildingBlocks.Core.Domain.Rules;
using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Results;
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
            // Validate owner and optional title
            CheckRule(new ConversationMustHaveOwnerRule(ownerId));
            CheckRule(new TitleProvidedMustBeValidRule(titleOrNull));

            var conversationId = ConversationId.New();
            var now = clock.UtcNow;
            
            string actualTitle;
            bool isDefault;

            if (string.IsNullOrEmpty(titleOrNull))
            {
                // Default title path
                actualTitle = string.Empty;
                isDefault = true;
            }
            else
            {
                // User-provided title path - validate with VO
                var titleResult = ConversationTitle.Create(titleOrNull);
                if (titleResult.IsFailure)
                    return Result<Conversation>.Failure(titleResult.Error);
                
                actualTitle = titleResult.Value.Value;
                isDefault = false;
            }

            var conversation = new Conversation(
                conversationId,
                ownerId,
                actualTitle,
                isDefault);

            // Raise domain event
            conversation.RaiseDomainEvent(new ConversationStartedEvent(
                conversationId,
                ownerId,
                actualTitle,
                isDefault,
                now));

            return Result<Conversation>.Success(conversation);
        }
        catch (BusinessRuleException ex)
        {
            return Result<Conversation>.Failure(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
    }

    /// <summary>
    /// Appends a user message to the conversation.
    /// </summary>
    public Result<Message> AppendUserMessage(MessageContent content, IClock clock)
    {
        try
        {
            // Validate state and limits
            CheckRule(new ConversationMustBeActiveRule(Status));
            CheckRule(new MessageContentWithinLimitsRule(content.Value));
            CheckRule(new ConversationMessageLimitRule(MessageCount));

            var now = clock.UtcNow;
            var sequence = MessageCount + 1;
            
            var message = Message.Create(
                Id,
                MessageRole.User,
                content,
                sequence);
            
            _messages.Add(message);
            UpdatedAt = now;

            // Raise event with content preview
            var contentPreview = CreateContentPreview(content.Value);
            RaiseDomainEvent(new UserMessageAppendedEvent(
                Id,
                message.Id,
                sequence,
                contentPreview,
                now));

            return Result<Message>.Success(message);
        }
        catch (BusinessRuleException ex)
        {
            return Result<Message>.Failure(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
    }

    /// <summary>
    /// Appends an assistant message to the conversation.
    /// </summary>
    public Result<Message> AppendAssistantMessage(MessageContent content, IClock clock)
    {
        try
        {
            // Validate state and limits
            CheckRule(new ConversationMustBeActiveRule(Status));
            CheckRule(new MessageContentWithinLimitsRule(content.Value));
            CheckRule(new ConversationMessageLimitRule(MessageCount));
            
            // Turn-taking rule: no consecutive assistant messages
            CheckRule(new MessageTurnTakingRule(_messages, MessageRole.Assistant));

            var now = clock.UtcNow;
            var sequence = MessageCount + 1;
            
            var message = Message.Create(
                Id,
                MessageRole.Assistant,
                content,
                sequence);
            
            _messages.Add(message);
            UpdatedAt = now;

            // Raise event with content preview
            var contentPreview = CreateContentPreview(content.Value);
            RaiseDomainEvent(new AssistantMessageAppendedEvent(
                Id,
                message.Id,
                sequence,
                contentPreview,
                now));

            return Result<Message>.Success(message);
        }
        catch (BusinessRuleException ex)
        {
            return Result<Message>.Failure(
                Error.BusinessRule(ex.Message, ex.Error.Code));
        }
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
    /// Creates content preview - plain truncation to 100 chars, no ellipsis.
    /// </summary>
    private static string CreateContentPreview(string content)
    {
        return TextSlices.Preview(content, 100);
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