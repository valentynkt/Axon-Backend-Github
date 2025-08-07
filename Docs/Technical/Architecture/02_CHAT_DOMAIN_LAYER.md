# Part 2: Chat Domain Layer - Comprehensive Implementation Guide

## Overview
The Chat Domain Layer implements the core business logic using Domain-Driven Design (DDD) tactical patterns with full encapsulation and railway-oriented programming.

## Core Principles
- **Rich Domain Model**: All business logic encapsulated in domain objects
- **Strong Typing**: Value objects for all domain concepts
- **Immutability**: Value objects are immutable with factory methods
- **Railway-Oriented**: All operations return `Result<T>`
- **Event-Ready**: Domain events structured for future event sourcing

## 1. Aggregate Root - Conversation

### 1.1 Conversation Aggregate

```csharp
// Modules/Chat/Domain/Conversation/Conversation.cs
using BuildingBlocks.Core.Domain;
using BuildingBlocks.Core.Results;
using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using Axon.Modules.Chat.Domain.Conversation.Entities;
using Axon.Modules.Chat.Domain.Conversation.Events;
using Axon.Modules.Chat.Domain.Conversation.Rules;

namespace Axon.Modules.Chat.Domain.Conversation;

public sealed class Conversation : AggregateRoot<ConversationId>
{
    private readonly List<Message> _messages = new();
    private readonly List<Participant> _participants = new();
    private ConversationTitle _title;
    private ConversationStatus _status;
    private UserId _startedBy;
    private DateTime _startedAt;
    private DateTime? _lastMessageAt;
    private DateTime? _archivedAt;
    private string? _archiveReason;
    private TokenUsage? _tokenUsage;
    private Dictionary<string, string>? _metadata;

    // Properties with encapsulation
    public ConversationTitle Title => _title;
    public ConversationStatus Status => _status;
    public UserId StartedBy => _startedBy;
    public DateTime StartedAt => _startedAt;
    public DateTime? LastMessageAt => _lastMessageAt;
    public DateTime? ArchivedAt => _archivedAt;
    public string? ArchiveReason => _archiveReason;
    public IReadOnlyList<Message> Messages => _messages.AsReadOnly();
    public IReadOnlyList<Participant> Participants => _participants.AsReadOnly();
    public TokenUsage? TokenUsage => _tokenUsage;
    public IReadOnlyDictionary<string, string>? Metadata => _metadata?.AsReadOnly();

    // Private constructor for ORM
    private Conversation() { }

    // Factory method for starting a conversation
    public static Conversation Start(
        ConversationId id,
        ConversationTitle title,
        UserId startedBy,
        MessageContent initialMessage,
        Dictionary<string, string>? metadata = null)
    {
        var conversation = new Conversation
        {
            Id = id,
            _title = title,
            _status = ConversationStatus.Active,
            _startedBy = startedBy,
            _startedAt = DateTime.UtcNow,
            _metadata = metadata
        };

        // Add initial message
        var message = Message.Create(
            MessageId.New(),
            id,
            startedBy,
            initialMessage,
            MessageRole.User);

        conversation._messages.Add(message);
        conversation._lastMessageAt = message.SentAt;

        // Add creator as participant
        var participant = Participant.Create(startedBy, ParticipantRole.Owner);
        conversation._participants.Add(participant);

        // Raise domain event
        conversation.AddDomainEvent(new ConversationStartedEvent(
            id,
            title.Value,
            startedBy.Value,
            initialMessage.Value,
            DateTime.UtcNow));

        return conversation;
    }

    // Add message with business rules
    public Result<Unit> AddMessage(
        MessageId messageId,
        UserId userId,
        MessageContent content,
        MessageRole role,
        List<string>? attachments = null,
        Dictionary<string, object>? metadata = null)
    {
        // Check business rules
        var canAddMessageRule = new CanAddMessageRule(_status);
        if (!canAddMessageRule.IsSatisfied())
            return Result<Unit>.Failure(canAddMessageRule.Error);

        var participantExistsRule = new ParticipantExistsRule(_participants, userId);
        if (!participantExistsRule.IsSatisfied() && userId != _startedBy)
            return Result<Unit>.Failure(participantExistsRule.Error);

        var messageLimitRule = new MessageLimitRule(_messages.Count);
        if (!messageLimitRule.IsSatisfied())
            return Result<Unit>.Failure(messageLimitRule.Error);

        // Create and add message
        var message = Message.Create(
            messageId,
            Id,
            userId,
            content,
            role,
            attachments,
            metadata);

        _messages.Add(message);
        _lastMessageAt = message.SentAt;

        // Update token usage if AI message
        if (role == MessageRole.Assistant && metadata?.ContainsKey("tokens") == true)
        {
            var tokens = Convert.ToInt32(metadata["tokens"]);
            UpdateTokenUsage(tokens);
        }

        // Raise domain event
        AddDomainEvent(new MessageAddedEvent(
            Id,
            messageId,
            userId.Value,
            content.Value,
            role.ToString(),
            DateTime.UtcNow));

        return Result.Success(Unit.Value);
    }

    // Add participant with validation
    public Result<Unit> AddParticipant(UserId userId, ParticipantRole role = ParticipantRole.Member)
    {
        var canAddParticipantRule = new CanAddParticipantRule(_status);
        if (!canAddParticipantRule.IsSatisfied())
            return Result<Unit>.Failure(canAddParticipantRule.Error);

        var participantAlreadyExistsRule = new ParticipantAlreadyExistsRule(_participants, userId);
        if (!participantAlreadyExistsRule.IsSatisfied())
            return Result<Unit>.Failure(participantAlreadyExistsRule.Error);

        var participantLimitRule = new ParticipantLimitRule(_participants.Count);
        if (!participantLimitRule.IsSatisfied())
            return Result<Unit>.Failure(participantLimitRule.Error);

        var participant = Participant.Create(userId, role);
        _participants.Add(participant);

        AddDomainEvent(new ParticipantAddedEvent(
            Id,
            userId.Value,
            role.ToString(),
            DateTime.UtcNow));

        return Result.Success(Unit.Value);
    }

    // Remove participant
    public Result<Unit> RemoveParticipant(UserId userId)
    {
        var participant = _participants.FirstOrDefault(p => p.UserId == userId);
        if (participant == null)
            return Result<Unit>.Failure(ConversationErrors.ParticipantNotFound(userId.Value));

        if (participant.Role == ParticipantRole.Owner && _participants.Count(p => p.Role == ParticipantRole.Owner) == 1)
            return Result<Unit>.Failure(ConversationErrors.CannotRemoveLastOwner());

        participant.Leave();
        
        AddDomainEvent(new ParticipantRemovedEvent(
            Id,
            userId.Value,
            DateTime.UtcNow));

        return Result.Success(Unit.Value);
    }

    // Edit message with audit trail
    public Result<Unit> EditMessage(MessageId messageId, MessageContent newContent, UserId editedBy)
    {
        var message = _messages.FirstOrDefault(m => m.Id == messageId);
        if (message == null)
            return Result<Unit>.Failure(ConversationErrors.MessageNotFound(messageId.Value));

        if (message.UserId != editedBy)
            return Result<Unit>.Failure(ConversationErrors.UnauthorizedMessageEdit());

        var canEditMessageRule = new CanEditMessageRule(message.SentAt);
        if (!canEditMessageRule.IsSatisfied())
            return Result<Unit>.Failure(canEditMessageRule.Error);

        var result = message.Edit(newContent);
        if (result.IsFailure)
            return result;

        AddDomainEvent(new MessageEditedEvent(
            Id,
            messageId,
            newContent.Value,
            editedBy.Value,
            DateTime.UtcNow));

        return Result.Success(Unit.Value);
    }

    // Archive conversation
    public Result<Unit> Archive(string? reason = null)
    {
        if (_status == ConversationStatus.Archived)
            return Result<Unit>.Failure(ConversationErrors.AlreadyArchived());

        var canArchiveRule = new CanArchiveRule(_messages.Count);
        if (!canArchiveRule.IsSatisfied())
            return Result<Unit>.Failure(canArchiveRule.Error);

        _status = ConversationStatus.Archived;
        _archivedAt = DateTime.UtcNow;
        _archiveReason = reason;

        AddDomainEvent(new ConversationArchivedEvent(
            Id,
            reason,
            DateTime.UtcNow));

        return Result.Success(Unit.Value);
    }

    // Reactivate archived conversation
    public Result<Unit> Reactivate()
    {
        if (_status != ConversationStatus.Archived)
            return Result<Unit>.Failure(ConversationErrors.NotArchived());

        _status = ConversationStatus.Active;
        _archivedAt = null;
        _archiveReason = null;

        AddDomainEvent(new ConversationReactivatedEvent(
            Id,
            DateTime.UtcNow));

        return Result.Success(Unit.Value);
    }

    // Update title
    public Result<Unit> UpdateTitle(ConversationTitle newTitle)
    {
        if (_title == newTitle)
            return Result.Success(Unit.Value);

        var oldTitle = _title.Value;
        _title = newTitle;

        AddDomainEvent(new ConversationTitleChangedEvent(
            Id,
            oldTitle,
            newTitle.Value,
            DateTime.UtcNow));

        return Result.Success(Unit.Value);
    }

    // Private helper to update token usage
    private void UpdateTokenUsage(int newTokens)
    {
        if (_tokenUsage == null)
        {
            _tokenUsage = new TokenUsage(0, newTokens, newTokens, EstimateCost(newTokens));
        }
        else
        {
            var totalTokens = _tokenUsage.TotalTokens + newTokens;
            _tokenUsage = new TokenUsage(
                _tokenUsage.InputTokens,
                _tokenUsage.OutputTokens + newTokens,
                totalTokens,
                EstimateCost(totalTokens));
        }
    }

    private decimal EstimateCost(int tokens)
    {
        // Simple cost estimation - would be configurable in real implementation
        const decimal costPerToken = 0.00002m;
        return tokens * costPerToken;
    }
}
```

## 2. Entities

### 2.1 Message Entity

```csharp
// Modules/Chat/Domain/Conversation/Entities/Message.cs
using BuildingBlocks.Core.Domain;
using BuildingBlocks.Core.Results;

namespace Axon.Modules.Chat.Domain.Conversation.Entities;

public sealed class Message : Entity<MessageId>
{
    private MessageContent _content;
    private DateTime? _editedAt;
    private List<string>? _attachments;
    private Dictionary<string, object>? _metadata;

    public ConversationId ConversationId { get; private set; }
    public UserId UserId { get; private set; }
    public MessageContent Content => _content;
    public MessageRole Role { get; private set; }
    public DateTime SentAt { get; private set; }
    public DateTime? EditedAt => _editedAt;
    public IReadOnlyList<string>? Attachments => _attachments?.AsReadOnly();
    public IReadOnlyDictionary<string, object>? Metadata => _metadata?.AsReadOnly();

    private Message() { }

    public static Message Create(
        MessageId id,
        ConversationId conversationId,
        UserId userId,
        MessageContent content,
        MessageRole role,
        List<string>? attachments = null,
        Dictionary<string, object>? metadata = null)
    {
        return new Message
        {
            Id = id,
            ConversationId = conversationId,
            UserId = userId,
            _content = content,
            Role = role,
            SentAt = DateTime.UtcNow,
            _attachments = attachments,
            _metadata = metadata
        };
    }

    public Result<Unit> Edit(MessageContent newContent)
    {
        if (_content == newContent)
            return Result.Success(Unit.Value);

        _content = newContent;
        _editedAt = DateTime.UtcNow;

        return Result.Success(Unit.Value);
    }

    public void AddAttachment(string attachmentUrl)
    {
        _attachments ??= new List<string>();
        _attachments.Add(attachmentUrl);
    }

    public void AddMetadata(string key, object value)
    {
        _metadata ??= new Dictionary<string, object>();
        _metadata[key] = value;
    }
}
```

### 2.2 Participant Entity

```csharp
// Modules/Chat/Domain/Conversation/Entities/Participant.cs
namespace Axon.Modules.Chat.Domain.Conversation.Entities;

public sealed class Participant : Entity<UserId>
{
    public UserId UserId => Id;
    public ParticipantRole Role { get; private set; }
    public DateTime JoinedAt { get; private set; }
    public DateTime? LeftAt { get; private set; }
    public bool IsActive => LeftAt == null;

    private Participant() { }

    public static Participant Create(UserId userId, ParticipantRole role)
    {
        return new Participant
        {
            Id = userId,
            Role = role,
            JoinedAt = DateTime.UtcNow
        };
    }

    public void Leave()
    {
        LeftAt = DateTime.UtcNow;
    }

    public void ChangeRole(ParticipantRole newRole)
    {
        Role = newRole;
    }
}
```

## 3. Value Objects

### 3.1 Identifiers

```csharp
// Modules/Chat/Domain/Conversation/ValueObjects/ConversationId.cs
namespace Axon.Modules.Chat.Domain.Conversation.ValueObjects;

public sealed record ConversationId
{
    public Guid Value { get; }

    private ConversationId(Guid value)
    {
        Value = value;
    }

    public static ConversationId New() => new(Guid.NewGuid());

    public static Result<ConversationId> Create(Guid value)
    {
        if (value == Guid.Empty)
            return Result<ConversationId>.Failure(
                ValidationErrors.InvalidId("ConversationId cannot be empty"));

        return Result.Success(new ConversationId(value));
    }

    public override string ToString() => Value.ToString();
}

// Modules/Chat/Domain/Conversation/ValueObjects/MessageId.cs
public sealed record MessageId
{
    public Guid Value { get; }

    private MessageId(Guid value)
    {
        Value = value;
    }

    public static MessageId New() => new(Guid.NewGuid());

    public static Result<MessageId> Create(Guid value)
    {
        if (value == Guid.Empty)
            return Result<MessageId>.Failure(
                ValidationErrors.InvalidId("MessageId cannot be empty"));

        return Result.Success(new MessageId(value));
    }

    public override string ToString() => Value.ToString();
}

// Modules/Chat/Domain/Conversation/ValueObjects/UserId.cs
public sealed record UserId
{
    public Guid Value { get; }

    private UserId(Guid value)
    {
        Value = value;
    }

    public static UserId System => new(Guid.Parse("00000000-0000-0000-0000-000000000000"));
    
    public static Result<UserId> Create(Guid value)
    {
        if (value == Guid.Empty && value != System.Value)
            return Result<UserId>.Failure(
                ValidationErrors.InvalidId("UserId cannot be empty"));

        return Result.Success(new UserId(value));
    }

    public override string ToString() => Value.ToString();
}
```

### 3.2 ConversationTitle

```csharp
// Modules/Chat/Domain/Conversation/ValueObjects/ConversationTitle.cs
namespace Axon.Modules.Chat.Domain.Conversation.ValueObjects;

public sealed record ConversationTitle
{
    private const int MinLength = 1;
    private const int MaxLength = 200;

    public string Value { get; }

    private ConversationTitle(string value)
    {
        Value = value;
    }

    public static Result<ConversationTitle> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<ConversationTitle>.Failure(
                ValidationErrors.Required("ConversationTitle"));

        value = value.Trim();

        if (value.Length < MinLength)
            return Result<ConversationTitle>.Failure(
                ValidationErrors.TooShort("ConversationTitle", MinLength));

        if (value.Length > MaxLength)
            return Result<ConversationTitle>.Failure(
                ValidationErrors.TooLong("ConversationTitle", MaxLength));

        // Additional validation for inappropriate content
        if (ContainsInappropriateContent(value))
            return Result<ConversationTitle>.Failure(
                ValidationErrors.InappropriateContent("ConversationTitle"));

        return Result.Success(new ConversationTitle(value));
    }

    private static bool ContainsInappropriateContent(string value)
    {
        // Simple check - would be more sophisticated in production
        var bannedWords = new[] { "spam", "xxx", "viagra" };
        return bannedWords.Any(word => 
            value.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    public override string ToString() => Value;
}
```

### 3.3 MessageContent

```csharp
// Modules/Chat/Domain/Conversation/ValueObjects/MessageContent.cs
namespace Axon.Modules.Chat.Domain.Conversation.ValueObjects;

public sealed record MessageContent
{
    private const int MinLength = 1;
    private const int MaxLength = 4000;

    public string Value { get; }
    public int CharacterCount { get; }
    public int EstimatedTokens { get; }

    private MessageContent(string value)
    {
        Value = value;
        CharacterCount = value.Length;
        EstimatedTokens = EstimateTokenCount(value);
    }

    public static Result<MessageContent> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<MessageContent>.Failure(
                ValidationErrors.Required("MessageContent"));

        value = value.Trim();

        if (value.Length < MinLength)
            return Result<MessageContent>.Failure(
                ValidationErrors.TooShort("MessageContent", MinLength));

        if (value.Length > MaxLength)
            return Result<MessageContent>.Failure(
                ValidationErrors.TooLong("MessageContent", MaxLength));

        // Check for harmful content
        if (ContainsHarmfulContent(value))
            return Result<MessageContent>.Failure(
                ValidationErrors.HarmfulContent("MessageContent"));

        return Result.Success(new MessageContent(value));
    }

    private static int EstimateTokenCount(string text)
    {
        // Rough estimation: 1 token ≈ 4 characters
        return (int)Math.Ceiling(text.Length / 4.0);
    }

    private static bool ContainsHarmfulContent(string value)
    {
        // Placeholder for content moderation
        // In production, would use AI-based content filtering
        return false;
    }

    public bool ContainsCode()
    {
        // Simple heuristic for code detection
        return Value.Contains("```") || 
               Value.Contains("function") || 
               Value.Contains("class") ||
               Value.Contains("const ") ||
               Value.Contains("var ");
    }

    public override string ToString() => 
        Value.Length > 50 ? $"{Value.Substring(0, 47)}..." : Value;
}
```

### 3.4 TokenUsage

```csharp
// Modules/Chat/Domain/Conversation/ValueObjects/TokenUsage.cs
namespace Axon.Modules.Chat.Domain.Conversation.ValueObjects;

public sealed record TokenUsage
{
    public int InputTokens { get; }
    public int OutputTokens { get; }
    public int TotalTokens { get; }
    public decimal EstimatedCost { get; }

    public TokenUsage(int inputTokens, int outputTokens, int totalTokens, decimal estimatedCost)
    {
        if (inputTokens < 0)
            throw new ArgumentException("Input tokens cannot be negative", nameof(inputTokens));
        if (outputTokens < 0)
            throw new ArgumentException("Output tokens cannot be negative", nameof(outputTokens));
        if (totalTokens != inputTokens + outputTokens)
            throw new ArgumentException("Total tokens must equal sum of input and output tokens", nameof(totalTokens));
        if (estimatedCost < 0)
            throw new ArgumentException("Estimated cost cannot be negative", nameof(estimatedCost));

        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        TotalTokens = totalTokens;
        EstimatedCost = estimatedCost;
    }

    public TokenUsage Add(TokenUsage other)
    {
        return new TokenUsage(
            InputTokens + other.InputTokens,
            OutputTokens + other.OutputTokens,
            TotalTokens + other.TotalTokens,
            EstimatedCost + other.EstimatedCost);
    }
}
```

## 4. Enumerations

```csharp
// Modules/Chat/Domain/Conversation/Enums/ConversationStatus.cs
namespace Axon.Modules.Chat.Domain.Conversation;

public enum ConversationStatus
{
    Active,
    Archived,
    Suspended,
    Deleted
}

// Modules/Chat/Domain/Conversation/Enums/MessageRole.cs
public enum MessageRole
{
    User,
    Assistant,
    System,
    Function
}

// Modules/Chat/Domain/Conversation/Enums/ParticipantRole.cs
public enum ParticipantRole
{
    Owner,
    Moderator,
    Member,
    Viewer
}

// Modules/Chat/Domain/Conversation/Enums/AiModel.cs
public enum AiModel
{
    GPT4,
    GPT35Turbo,
    Claude3,
    Claude2,
    Custom
}
```

## 5. Domain Events

```csharp
// Modules/Chat/Domain/Conversation/Events/ConversationStartedEvent.cs
using BuildingBlocks.Core.Events;

namespace Axon.Modules.Chat.Domain.Conversation.Events;

public sealed record ConversationStartedEvent : DomainEvent
{
    public ConversationId ConversationId { get; }
    public string Title { get; }
    public Guid StartedBy { get; }
    public string InitialMessage { get; }

    public ConversationStartedEvent(
        ConversationId conversationId,
        string title,
        Guid startedBy,
        string initialMessage,
        DateTime occurredOn) : base(occurredOn)
    {
        ConversationId = conversationId;
        Title = title;
        StartedBy = startedBy;
        InitialMessage = initialMessage;
    }
}

// Modules/Chat/Domain/Conversation/Events/MessageAddedEvent.cs
public sealed record MessageAddedEvent : DomainEvent
{
    public ConversationId ConversationId { get; }
    public MessageId MessageId { get; }
    public Guid UserId { get; }
    public string Content { get; }
    public string Role { get; }

    public MessageAddedEvent(
        ConversationId conversationId,
        MessageId messageId,
        Guid userId,
        string content,
        string role,
        DateTime occurredOn) : base(occurredOn)
    {
        ConversationId = conversationId;
        MessageId = messageId;
        UserId = userId;
        Content = content;
        Role = role;
    }
}

// Modules/Chat/Domain/Conversation/Events/ConversationArchivedEvent.cs
public sealed record ConversationArchivedEvent : DomainEvent
{
    public ConversationId ConversationId { get; }
    public string? Reason { get; }

    public ConversationArchivedEvent(
        ConversationId conversationId,
        string? reason,
        DateTime occurredOn) : base(occurredOn)
    {
        ConversationId = conversationId;
        Reason = reason;
    }
}

// Additional events follow similar pattern...
```

## 6. Business Rules

```csharp
// Modules/Chat/Domain/Conversation/Rules/CanAddMessageRule.cs
using BuildingBlocks.Core.Domain;

namespace Axon.Modules.Chat.Domain.Conversation.Rules;

public sealed class CanAddMessageRule : IBusinessRule
{
    private readonly ConversationStatus _status;

    public CanAddMessageRule(ConversationStatus status)
    {
        _status = status;
    }

    public bool IsSatisfied() => _status == ConversationStatus.Active;

    public Error Error => ConversationErrors.CannotAddMessageToInactiveConversation();
}

// Modules/Chat/Domain/Conversation/Rules/MessageLimitRule.cs
public sealed class MessageLimitRule : IBusinessRule
{
    private const int MaxMessages = 1000;
    private readonly int _currentMessageCount;

    public MessageLimitRule(int currentMessageCount)
    {
        _currentMessageCount = currentMessageCount;
    }

    public bool IsSatisfied() => _currentMessageCount < MaxMessages;

    public Error Error => ConversationErrors.MessageLimitExceeded(MaxMessages);
}

// Modules/Chat/Domain/Conversation/Rules/ParticipantLimitRule.cs
public sealed class ParticipantLimitRule : IBusinessRule
{
    private const int MaxParticipants = 50;
    private readonly int _currentParticipantCount;

    public ParticipantLimitRule(int currentParticipantCount)
    {
        _currentParticipantCount = currentParticipantCount;
    }

    public bool IsSatisfied() => _currentParticipantCount < MaxParticipants;

    public Error Error => ConversationErrors.ParticipantLimitExceeded(MaxParticipants);
}

// Modules/Chat/Domain/Conversation/Rules/CanEditMessageRule.cs
public sealed class CanEditMessageRule : IBusinessRule
{
    private readonly DateTime _messageSentAt;
    private readonly TimeSpan _editWindow = TimeSpan.FromMinutes(15);

    public CanEditMessageRule(DateTime messageSentAt)
    {
        _messageSentAt = messageSentAt;
    }

    public bool IsSatisfied() => DateTime.UtcNow - _messageSentAt <= _editWindow;

    public Error Error => ConversationErrors.EditWindowExpired();
}

// Modules/Chat/Domain/Conversation/Rules/CanArchiveRule.cs
public sealed class CanArchiveRule : IBusinessRule
{
    private const int MinMessagesForArchive = 1;
    private readonly int _messageCount;

    public CanArchiveRule(int messageCount)
    {
        _messageCount = messageCount;
    }

    public bool IsSatisfied() => _messageCount >= MinMessagesForArchive;

    public Error Error => ConversationErrors.CannotArchiveEmptyConversation();
}
```

## 7. Domain Services

### 7.1 Message Validator

```csharp
// Modules/Chat/Domain/Conversation/Services/IMessageValidator.cs
namespace Axon.Modules.Chat.Domain.Conversation.Services;

public interface IMessageValidator
{
    Task<Result<Unit>> ValidateAsync(
        string content, 
        MessageRole role,
        CancellationToken cancellationToken = default);
}

public sealed class MessageValidator : IMessageValidator
{
    private readonly IContentModerationService _moderationService;
    private readonly ILogger<MessageValidator> _logger;

    public MessageValidator(
        IContentModerationService moderationService,
        ILogger<MessageValidator> logger)
    {
        _moderationService = moderationService;
        _logger = logger;
    }

    public async Task<Result<Unit>> ValidateAsync(
        string content,
        MessageRole role,
        CancellationToken cancellationToken = default)
    {
        // Skip validation for system messages
        if (role == MessageRole.System)
            return Result.Success(Unit.Value);

        // Check for harmful content
        var moderationResult = await _moderationService.CheckContentAsync(
            content,
            cancellationToken);

        if (moderationResult.IsHarmful)
        {
            _logger.LogWarning(
                "Harmful content detected: {Categories}",
                string.Join(", ", moderationResult.Categories));

            return Result<Unit>.Failure(
                ValidationErrors.HarmfulContent("Message"));
        }

        // Check for PII
        if (ContainsPII(content))
        {
            return Result<Unit>.Failure(
                ValidationErrors.ContainsPII("Message"));
        }

        return Result.Success(Unit.Value);
    }

    private bool ContainsPII(string content)
    {
        // Simple regex patterns for common PII
        var patterns = new[]
        {
            @"\b\d{3}-\d{2}-\d{4}\b", // SSN
            @"\b\d{16}\b", // Credit card
            @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b" // Email
        };

        return patterns.Any(pattern => 
            System.Text.RegularExpressions.Regex.IsMatch(content, pattern));
    }
}
```

### 7.2 AI Response Processor

```csharp
// Modules/Chat/Domain/Conversation/Services/IAiResponseProcessor.cs
namespace Axon.Modules.Chat.Domain.Conversation.Services;

public interface IAiResponseProcessor
{
    Task<Result<string>> ProcessAsync(
        string aiResponse,
        ConversationId conversationId,
        CancellationToken cancellationToken = default);
}

public sealed class AiResponseProcessor : IAiResponseProcessor
{
    private readonly IMessageValidator _validator;
    private readonly ILogger<AiResponseProcessor> _logger;

    public async Task<Result<string>> ProcessAsync(
        string aiResponse,
        ConversationId conversationId,
        CancellationToken cancellationToken = default)
    {
        // Validate AI response
        var validationResult = await _validator.ValidateAsync(
            aiResponse,
            MessageRole.Assistant,
            cancellationToken);

        if (validationResult.IsFailure)
            return Result<string>.Failure(validationResult.Error);

        // Clean and format response
        var processedResponse = CleanResponse(aiResponse);

        // Check for hallucinations or problematic content
        if (ContainsHallucination(processedResponse))
        {
            _logger.LogWarning(
                "Potential hallucination detected in AI response for conversation {ConversationId}",
                conversationId.Value);

            processedResponse = AddHallucinationWarning(processedResponse);
        }

        return Result.Success(processedResponse);
    }

    private string CleanResponse(string response)
    {
        // Remove any system tokens or artifacts
        response = response.Trim();
        response = System.Text.RegularExpressions.Regex.Replace(
            response, 
            @"<\|.*?\|>", 
            string.Empty);

        return response;
    }

    private bool ContainsHallucination(string response)
    {
        // Simple heuristics - would use more sophisticated detection in production
        var suspiciousPatterns = new[]
        {
            "As an AI language model",
            "I don't have access to real-time",
            "My training data"
        };

        return suspiciousPatterns.Any(pattern =>
            response.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private string AddHallucinationWarning(string response)
    {
        return $"{response}\n\n*Note: This response may contain inaccuracies. Please verify important information.*";
    }
}
```

### 7.3 Archive Policy

```csharp
// Modules/Chat/Domain/Conversation/Services/IArchivePolicy.cs
namespace Axon.Modules.Chat.Domain.Conversation.Services;

public interface IArchivePolicy
{
    Task<Result<bool>> CanArchiveAsync(
        Conversation conversation,
        CancellationToken cancellationToken = default);
    
    Task<Result<IEnumerable<Conversation>>> GetConversationsToArchiveAsync(
        CancellationToken cancellationToken = default);
}

public sealed class ArchivePolicy : IArchivePolicy
{
    private readonly IConversationRepository _repository;
    private readonly IConfiguration _configuration;

    public async Task<Result<bool>> CanArchiveAsync(
        Conversation conversation,
        CancellationToken cancellationToken = default)
    {
        // Check if conversation meets archive criteria
        if (conversation.Status != ConversationStatus.Active)
            return Result.Success(false);

        if (!conversation.LastMessageAt.HasValue)
            return Result.Success(false);

        var inactivityPeriod = _configuration.GetValue<TimeSpan>("Archive:InactivityPeriod");
        var isInactive = DateTime.UtcNow - conversation.LastMessageAt.Value > inactivityPeriod;

        if (!isInactive)
            return Result.Success(false);

        // Check for any pending operations
        var hasPendingOperations = await CheckPendingOperationsAsync(
            conversation.Id,
            cancellationToken);

        return Result.Success(!hasPendingOperations);
    }

    public async Task<Result<IEnumerable<Conversation>>> GetConversationsToArchiveAsync(
        CancellationToken cancellationToken = default)
    {
        var inactivityPeriod = _configuration.GetValue<TimeSpan>("Archive:InactivityPeriod");
        var cutoffDate = DateTime.UtcNow - inactivityPeriod;

        var specification = new InactiveConversationsSpecification(cutoffDate);
        return await _repository.GetBySpecificationAsync(specification, cancellationToken);
    }

    private async Task<bool> CheckPendingOperationsAsync(
        ConversationId conversationId,
        CancellationToken cancellationToken)
    {
        // Check for any pending AI responses, uploads, etc.
        // Implementation would check various services
        return false;
    }
}
```

## 8. Repository Interfaces

```csharp
// Modules/Chat/Domain/Conversation/Repositories/IConversationWriteRepository.cs
// TODO: consider ValueTask<TResult> for hot paths / high-QPS
namespace Axon.Modules.Chat.Domain.Conversation.Repositories;

public interface IConversationWriteRepository
{
    Task<Result<Unit>> AddAsync(
        Conversation conversation,
        CancellationToken cancellationToken = default);

    Task<Result<Conversation>> GetByIdAsync(
        ConversationId id,
        CancellationToken cancellationToken = default);

    Task<Result<Unit>> UpdateAsync(
        Conversation conversation,
        CancellationToken cancellationToken = default);

    Task<Result<Unit>> DeleteAsync(
        ConversationId id,
        CancellationToken cancellationToken = default);
}

// Modules/Chat/Domain/Conversation/Repositories/IConversationReadRepository.cs
// TODO: consider ValueTask<TResult> for hot paths / high-QPS
public interface IConversationReadRepository
{
    Task<Result<ConversationReadModel>> GetByIdAsync(
        ConversationId id,
        CancellationToken cancellationToken = default);

    Task<Result<IEnumerable<ConversationReadModel>>> GetByUserIdAsync(
        UserId userId,
        CancellationToken cancellationToken = default);

    Task<Result<PagedResult<ConversationReadModel>>> GetPagedAsync(
        ISpecification<ConversationReadModel> specification,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> ExistsAsync(
        ConversationId id,
        CancellationToken cancellationToken = default);
}

// Modules/Chat/Domain/Conversation/ReadModels/ConversationReadModel.cs
public sealed class ConversationReadModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid StartedBy { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime LastMessageAt { get; set; }
    public string? LastMessage { get; set; }
    public int MessageCount { get; set; }
    public int UnreadCount { get; set; }
    public List<ParticipantReadModel> Participants { get; set; } = new();
}

public sealed class ParticipantReadModel
{
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
}
```


## 9. Domain Errors

```csharp
// Modules/Chat/Domain/Conversation/Errors/ConversationErrors.cs
namespace Axon.Modules.Chat.Domain.Conversation.Errors;

public static class ConversationErrors
{
    // 04xx = NOT-FOUND / FORBIDDEN, 09 = CONFLICT, 22 = VALIDATION, 29 = LIMIT

    public static Error ConversationNotFound(Guid id) =>
        Error.NotFound("CON-0404", $"Conversation with ID {id} was not found");

    public static Error MessageNotFound(Guid id) =>
        Error.NotFound("MSG-0404", $"Message with ID {id} was not found");

    public static Error ParticipantNotFound(Guid userId) =>
        Error.NotFound("PAR-0404", $"Participant with user ID {userId} was not found");

    public static Error UnauthorizedAccess(Guid conversationId) =>
        Error.Forbidden("CON-0403", $"User does not have access to conversation {conversationId}");

    public static Error UnauthorizedMessageEdit() =>
        Error.Forbidden("MSG-0403", "User cannot edit messages from other users");

    public static Error CannotAddMessageToInactiveConversation() =>
        Error.Validation("CON-0422", "Cannot add messages to inactive conversation");

    public static Error MessageLimitExceeded(int limit) =>
        Error.Validation("CON-0429", $"Conversation has reached the maximum of {limit} messages");

    public static Error ParticipantLimitExceeded(int limit) =>
        Error.Validation("PAR-0429", $"Conversation has reached the maximum of {limit} participants");

    public static Error ParticipantAlreadyExists(Guid userId) =>
        Error.Conflict("PAR-0409", $"User {userId} is already a participant");

    public static Error CannotRemoveLastOwner() =>
        Error.Validation("PAR-0422", "Cannot remove the last owner from conversation");

    public static Error EditWindowExpired() =>
        Error.Validation("MSG-0422", "Message edit window has expired");

    public static Error AlreadyArchived() =>
        Error.Conflict("CON-0409", "Conversation is already archived");

    public static Error NotArchived() =>
        Error.Validation("CON-0422", "Conversation is not archived");

    public static Error CannotArchiveEmptyConversation() =>
        Error.Validation("CON-0422", "Cannot archive conversation with no messages");
}
```


## 10. Specifications

```csharp
// Modules/Chat/Domain/Conversation/Specifications/UserConversationsSpecification.cs
using BuildingBlocks.Core.Specifications;

namespace Axon.Modules.Chat.Domain.Conversation.Specifications;

public sealed class UserConversationsSpecification : Specification<ConversationReadModel>
{
    public UserConversationsSpecification(
        UserId userId,
        ConversationStatus? status = null,
        DateTime? since = null,
        int pageNumber = 1,
        int pageSize = 20,
        string? sortBy = null,
        bool descending = true)
    {
        AddCriteria(c => c.StartedBy == userId.Value || 
                        c.Participants.Any(p => p.UserId == userId.Value));

        if (status.HasValue)
            AddCriteria(c => c.Status == status.Value.ToString());

        if (since.HasValue)
            AddCriteria(c => c.LastMessageAt >= since.Value);

        ApplyPaging(pageNumber, pageSize);

        if (!string.IsNullOrEmpty(sortBy))
        {
            switch (sortBy.ToLower())
            {
                case "title":
                    ApplyOrderBy(c => c.Title, descending);
                    break;
                case "startedat":
                    ApplyOrderBy(c => c.StartedAt, descending);
                    break;
                default:
                    ApplyOrderBy(c => c.LastMessageAt, descending);
                    break;
            }
        }
    }
}

// Modules/Chat/Domain/Conversation/Specifications/InactiveConversationsSpecification.cs
public sealed class InactiveConversationsSpecification : Specification<Conversation>
{
    public InactiveConversationsSpecification(DateTime cutoffDate)
    {
        AddCriteria(c => c.Status == ConversationStatus.Active);
        AddCriteria(c => c.LastMessageAt.HasValue && c.LastMessageAt.Value < cutoffDate);
        ApplyOrderBy(c => c.LastMessageAt, false);
    }
}
```

## Summary

This Chat Domain Layer implementation provides:

1. **Rich Aggregate Root**: Complete encapsulation with all business operations
2. **Strong Value Objects**: Immutable with factory methods and validation
3. **Explicit Business Rules**: Separate classes for each rule
4. **Domain Events**: Ready for event sourcing migration
5. **Domain Services**: Complex business logic isolation
6. **Repository Interfaces**: Clean separation of concerns
7. **Comprehensive Error Handling**: Domain-specific error types
8. **Specifications**: Complex query logic encapsulation

The domain layer is completely isolated from infrastructure concerns and ready for integration with the Application and Infrastructure layers.