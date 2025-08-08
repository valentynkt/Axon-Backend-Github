using Axon.Modules.Chat.Domain.Conversation.Entities;
using Axon.Modules.Chat.Domain.Conversation.Events;
using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using Axon.Modules.Chat.Domain.Errors;
using Axon.Modules.Chat.Domain.Rules;
using Axon.Shared.Common;
using BuildingBlocks.Core.Domain.Model;
using BuildingBlocks.Core.Domain.Rules;
using BuildingBlocks.Core.Functional;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Functional.Validation;
using BuildingBlocks.Core.Domain.Migration;

namespace Axon.Modules.Chat.Domain.Conversation;

/// <summary>
/// Epic 2 enhanced Conversation aggregate root demonstrating comprehensive domain modeling patterns.
/// Implements rich business rules, domain events, and validation using Epic 2 patterns.
/// Fully integrated with Epic 5 pipeline behaviors for transaction management and event dispatching.
/// </summary>
[Epic2Migrated("Migrated from BaseAggregate to AggregateRoot with Epic 2 patterns")]
public sealed partial class Conversation : AggregateRoot<ConversationId>
{
    private readonly List<Message> _messages = new();

    /// <summary>Conversation owner.</summary>
    public UserId OwnerId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    /// <summary>Lifecycle status.</summary>
    public ConversationStatus Status { get; private set; } = ConversationStatus.Active;

    /// <summary>Completion timestamp (if completed).</summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>True when conversation accepts new messages.</summary>
    public bool IsActive => Status == ConversationStatus.Active;

    /// <summary>Current number of messages.</summary>
    public int MessageCount => _messages.Count;

    /// <summary>Read-only view of messages (in insertion/sequence order).</summary>
    public IReadOnlyList<Message> Messages => _messages.AsReadOnly();

    // EF Core / serialization
    private Conversation() { OwnerId = default!; }

    private Conversation(ConversationId id, UserId ownerId, string title)
    {
        Id = id;
        OwnerId = ownerId;
        Title = title;
        Status = ConversationStatus.Active;

        RaiseDomainEvent(new ConversationStartedDomainEvent(Id, OwnerId, Title));
    }

    /// <summary>
    /// Epic 2 factory method with comprehensive business rule validation.
    /// Demonstrates proper use of RuleBuilder pattern and domain event raising.
    /// </summary>
    public static Result<Conversation> Start(string title, string userId)
    {
        // 1. Validate inputs using Epic 2 business rules pattern
        var rules = new RuleBuilder()
            .NotEmpty(userId, nameof(userId))
            .NotEmpty(title, nameof(title))
            .AddRule(new ConversationTitleValidationRule(title))
            .Build();
            
        if (rules.IsFailure)
            return Result<Conversation>.Failure(rules.Error);

        // 2. Create value objects with validation
        var ownerIdResult = UserId.Create(userId);
        if (ownerIdResult.IsFailure)
            return Result<Conversation>.Failure(ownerIdResult.Error);

        // 3. Create aggregate with validated inputs
        var conversation = new Conversation(ConversationId.New(), ownerIdResult.Value, title.Trim());
        
        // 4. Validate aggregate state
        var validation = conversation.Validate();
        if (validation.IsInvalid)
            return validation.ToResultWithAggregatedError();
        
        return Result<Conversation>.Success(conversation);
    }

    /// <summary>
    /// Epic 2 enhanced message appending with comprehensive business rule validation.
    /// Demonstrates proper use of Epic 2 patterns: rules first, then state changes, then events.
    /// </summary>
    public Result<Message> AppendUserMessage(string content, string requestingUserId)
    {
        // 1. Business rules validation FIRST (Epic 2 corrected pattern)
        var requestingUserIdResult = UserId.Create(requestingUserId);
        if (requestingUserIdResult.IsFailure)
            return Result<Message>.Failure(requestingUserIdResult.Error);
            
        var rules = new RuleBuilder()
            .AddRule(new ConversationOwnershipRule(OwnerId, requestingUserIdResult.Value))
            .AddRule(new ConversationMustBeActiveRule(Status))
            .AddRule(new MessageContentValidationRule(content))
            .AddRule(new ConversationMessageLimitRule(_messages.Count))
            .AddRule(new DuplicateMessagePreventionRule(_messages, content))
            .AddRule(new MessageRateLimitRule(_messages.Where(m => m.Role == MessageRole.User).ToList()))
            .Build();
            
        if (rules.IsFailure)
            return Result<Message>.Failure(rules.Error);

        // 2. Create value objects with validation
        var messageContentResult = MessageContent.Create(content);
        if (messageContentResult.IsFailure)
            return Result<Message>.Failure(messageContentResult.Error);

        var nextSequence = _messages.Count + 1;

        // 3. Apply state change (all preconditions validated)
        return ApplyChange(() =>
        {
            var messageResult = Message.Create(
                messageContentResult.Value,
                Id,
                MessageRole.User,
                nextSequence);
                
            if (messageResult.IsFailure)
                throw new InvalidOperationException($"Message creation failed after validation: {messageResult.Error.Message}");

            var message = messageResult.Value;
            _messages.Add(message);

            // 4. Raise domain events after successful state change
            RaiseDomainEvent(new UserMessageAppendedDomainEvent(
                Id,
                message.Id,
                nextSequence,
                requestingUserId));
                
            return message;
        });
    }

    /// <summary>
    /// Epic 2 title update with business rule validation.
    /// </summary>
    public Result<Unit> UpdateTitle(string newTitle)
    {
        // Business rules validation
        var rules = new RuleBuilder()
            .AddRule(new ConversationTitleValidationRule(newTitle))
            .Build();
            
        if (rules.IsFailure)
            return rules;

        return ApplyChange(() =>
        {
            Title = newTitle.Trim();
        });
    }

    /// <summary>
    /// Epic 2 conversation completion with business rules and domain events.
    /// </summary>
    public Result<Unit> Complete()
    {
        // Business rules validation
        var rules = new RuleBuilder()
            .Must(IsActive, "CONVERSATION_NOT_ACTIVE", "Only active conversations can be completed")
            .Must(_messages.Count > 0, "CONVERSATION_EMPTY", "Cannot complete empty conversation")
            .Build();
            
        if (rules.IsFailure)
            return rules;

        return ApplyChange(() =>
        {
            Status = ConversationStatus.Completed;
            CompletedAt = DateTime.UtcNow;
            
            // Raise domain event for completion
            RaiseDomainEvent(new ConversationCompletedDomainEvent(Id, OwnerId, _messages.Count));
        });
    }

    /// <summary>Invariant: does this conversation belong to the given user?</summary>
    public bool BelongsToUser(string userId) =>
        !string.IsNullOrWhiteSpace(userId) &&
        OwnerId.Value.Equals(userId, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Epic 2 aggregate invariants implementation.
    /// Defines rules that must always be true for the aggregate.
    /// </summary>
    protected override IEnumerable<IBusinessRule> GetInvariants()
    {
        yield return new ConversationMustHaveOwnerRule(OwnerId);
        yield return new ConversationTitleValidationRule(Title);
        yield return new MessageSequenceIntegrityRule(_messages);
        
        // Conditional invariants
        if (Status == ConversationStatus.Completed)
        {
            yield return new PredicateRule(
                "COMPLETED_CONVERSATION_MUST_HAVE_COMPLETION_DATE",
                "Completed conversations must have a completion date",
                () => CompletedAt == null);
        }
    }

    /// <summary>
    /// Optional probe for sequence validation - now uses business rules.
    /// </summary>
    [Obsolete("Use GetInvariants() and Validate() instead")]
    public Result ValidateSequenceIntegrity()
    {
        var rule = new MessageSequenceIntegrityRule(_messages);
        return rule.IsBroken() 
            ? Result.Failure(Error.BusinessRule(rule.Message, rule.Code))
            : Result.Success();
    }
}

public enum ConversationStatus
{
    Active,
    Completed,
    Archived
}
