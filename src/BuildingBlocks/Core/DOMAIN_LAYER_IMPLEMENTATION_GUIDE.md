# Domain Layer Implementation Guide for Axon Backend

## Table of Contents
1. [Core Concepts & Philosophy](#core-concepts--philosophy)
2. [Entity Modeling](#entity-modeling)
3. [Value Objects](#value-objects)
4. [Aggregate Roots](#aggregate-roots)
5. [Strong IDs](#strong-ids)
6. [Domain Events](#domain-events)
7. [Business Rules](#business-rules)
8. [Result Pattern](#result-pattern)
9. [CQRS Implementation](#cqrs-implementation)
10. [Specifications](#specifications)
11. [Practical Chat Module Examples](#practical-chat-module-examples)

---

## Core Concepts & Philosophy

The Axon Backend domain layer follows **Domain-Driven Design (DDD)** principles with a strong emphasis on:
- **Type Safety**: Using strong typing to prevent primitive obsession
- **Functional Programming**: Immutable data structures with Result/Option monads
- **Rich Domain Models**: Business logic encapsulated within domain entities
- **Clean Architecture**: Domain layer is independent of infrastructure concerns

### Key Principles
1. **Domain Ignorance of Infrastructure**: Domain never references infrastructure
2. **Fail-Fast with Results**: Use `Result<T>` for all operations that can fail
3. **Immutability First**: Prefer records and immutable collections
4. **Type-Safe IDs**: Never use primitive IDs directly

---

## Entity Modeling

### Base Entity Class

All entities inherit from `Entity<TId>` which provides:
- Strong-typed ID management
- Value-based equality comparison
- Transient entity detection

```csharp
// Example: Chat Message Entity
public sealed class Message : Entity<MessageId>
{
    public ChatRoomId ChatRoomId { get; private set; }
    public UserId SenderId { get; private set; }
    public MessageContent Content { get; private set; }
    public MessageStatus Status { get; private set; }
    public DateTimeOffset SentAt { get; private set; }
    public DateTimeOffset? EditedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    
    // Private constructor for ORM
    private Message() : base() { }
    
    // Factory method with validation
    public static Result<Message> Create(
        ChatRoomId chatRoomId,
        UserId senderId,
        MessageContent content)
    {
        ArgumentNullException.ThrowIfNull(chatRoomId);
        ArgumentNullException.ThrowIfNull(senderId);
        ArgumentNullException.ThrowIfNull(content);
        
        if (!content.IsValid)
            return Result<Message>.Failure(content.ValidationErrors.First());
        
        var message = new Message
        {
            Id = MessageId.New(),
            ChatRoomId = chatRoomId,
            SenderId = senderId,
            Content = content,
            Status = MessageStatus.Sent,
            SentAt = DateTimeOffset.UtcNow
        };
        
        return Result<Message>.Success(message);
    }
    
    // Business methods return Results
    public Result<Unit> Edit(MessageContent newContent, UserId editorId)
    {
        if (SenderId != editorId)
            return Result<Unit>.Failure(Error.Forbidden("Only message sender can edit"));
            
        if (DeletedAt.HasValue)
            return Result<Unit>.Failure(Error.Conflict("Cannot edit deleted message"));
            
        if (!newContent.IsValid)
            return Result<Unit>.Failure(newContent.ValidationErrors.First());
        
        Content = newContent;
        EditedAt = DateTimeOffset.UtcNow;
        
        return Result<Unit>.Success(Unit.Value);
    }
}
```

### Entity Types Available

1. **Basic Entity**: `Entity<TId>` - Standard entity with ID
2. **Auditable Entity**: `AuditableEntity<TId>` - Adds CreatedAt, UpdatedAt, CreatedBy, UpdatedBy
3. **Soft-Deletable Entity**: `SoftDeletableEntity<TId>` - Adds DeletedAt, IsDeleted
4. **Auditable + Deletable**: `AuditableDeletableEntity<TId>` - Combines both

---

## Value Objects

Value objects are immutable types that represent domain concepts without identity.

### Creating Value Objects

```csharp
// Example: Message Content Value Object
public sealed record MessageContent : ValueObject
{
    public string Text { get; }
    public int CharacterCount { get; }
    public bool HasMentions { get; }
    public IReadOnlyList<string> MentionedUserIds { get; }
    
    private MessageContent(string text, IReadOnlyList<string> mentionedUserIds)
    {
        Text = text;
        CharacterCount = text.Length;
        MentionedUserIds = mentionedUserIds;
        HasMentions = mentionedUserIds.Count > 0;
    }
    
    // Factory method with validation
    public static Result<MessageContent> Create(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Result<MessageContent>.Failure(
                Error.Validation("Message content cannot be empty"));
        
        if (text.Length > 2000)
            return Result<MessageContent>.Failure(
                Error.Validation("Message cannot exceed 2000 characters"));
        
        var mentions = ExtractMentions(text);
        return Result<MessageContent>.Success(new MessageContent(text, mentions));
    }
    
    // Required for ValueObject base class
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Text;
        // Mentions are derived from Text, so we don't include them
    }
    
    // Required validation
    public override Validation<Unit> Validate()
    {
        var errors = new List<Error>();
        
        if (string.IsNullOrWhiteSpace(Text))
            errors.Add(Error.Validation("Message content cannot be empty"));
            
        if (Text.Length > 2000)
            errors.Add(Error.Validation($"Message exceeds maximum length: {Text.Length}/2000"));
        
        return errors.Count == 0 
            ? Validation<Unit>.Success(Unit.Value)
            : Validation<Unit>.Failure(errors);
    }
    
    private static IReadOnlyList<string> ExtractMentions(string text)
    {
        // Extract @mentions from text
        var pattern = @"@(\w+)";
        return Regex.Matches(text, pattern)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();
    }
}
```

### Single Value Objects

For simple value objects with one property, use `SingleValueObject<T>`:

```csharp
public sealed record ChatRoomName : SingleValueObject<string>
{
    private ChatRoomName(string value) : base(value) { }
    
    public static Result<ChatRoomName> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<ChatRoomName>.Failure(
                Error.Validation("Chat room name cannot be empty"));
        
        if (value.Length < 3 || value.Length > 50)
            return Result<ChatRoomName>.Failure(
                Error.Validation("Chat room name must be between 3 and 50 characters"));
        
        return Result<ChatRoomName>.Success(new ChatRoomName(value.Trim()));
    }
}
```

---

## Aggregate Roots

Aggregates are clusters of entities and value objects that are treated as a single unit for data changes.

### Creating Aggregate Roots

```csharp
public sealed class ChatRoom : AggregateRoot<ChatRoomId>
{
    private readonly List<ChatMember> _members = new();
    private readonly List<Message> _recentMessages = new();
    
    public ChatRoomName Name { get; private set; }
    public ChatRoomType Type { get; private set; }
    public UserId OwnerId { get; private set; }
    public ChatRoomSettings Settings { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsActive { get; private set; }
    
    public IReadOnlyList<ChatMember> Members => _members.AsReadOnly();
    public IReadOnlyList<Message> RecentMessages => _recentMessages.AsReadOnly();
    
    private ChatRoom() : base() { }
    
    // Factory method - always returns Result<T>
    public static Result<ChatRoom> Create(
        ChatRoomName name,
        ChatRoomType type,
        UserId ownerId,
        ChatRoomSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(ownerId);
        
        if (!name.IsValid)
            return Result<ChatRoom>.Failure(name.ValidationErrors.First());
        
        var chatRoom = new ChatRoom
        {
            Id = ChatRoomId.New(),
            Name = name,
            Type = type,
            OwnerId = ownerId,
            Settings = settings ?? ChatRoomSettings.Default,
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };
        
        // Add owner as first member
        var ownerMember = ChatMember.CreateOwner(ownerId, chatRoom.Id);
        chatRoom._members.Add(ownerMember);
        
        // Raise domain event
        chatRoom.RaiseDomainEvent(new ChatRoomCreatedEvent(
            chatRoom.Id,
            chatRoom.Name,
            chatRoom.OwnerId,
            chatRoom.CreatedAt));
        
        return Result<ChatRoom>.Success(chatRoom);
    }
    
    // Business method with domain logic
    public Result<ChatMember> AddMember(UserId userId, UserId addedBy)
    {
        // Check permissions
        if (!IsAdmin(addedBy))
            return Result<ChatMember>.Failure(
                Error.Forbidden("Only admins can add members"));
        
        // Check if already member
        if (_members.Any(m => m.UserId == userId))
            return Result<ChatMember>.Failure(
                Error.Conflict("User is already a member"));
        
        // Check room capacity
        if (_members.Count >= Settings.MaxMembers)
            return Result<ChatMember>.Failure(
                Error.Validation($"Room has reached maximum capacity: {Settings.MaxMembers}"));
        
        var member = ChatMember.Create(userId, Id, ChatMemberRole.Member);
        _members.Add(member);
        
        // Raise domain event
        RaiseDomainEvent(new MemberAddedToChatRoomEvent(
            Id, userId, addedBy, DateTimeOffset.UtcNow));
        
        return Result<ChatMember>.Success(member);
    }
    
    // Command method that modifies state
    public Result<Message> PostMessage(UserId senderId, MessageContent content)
    {
        if (!IsMember(senderId))
            return Result<Message>.Failure(
                Error.Forbidden("Only members can post messages"));
        
        if (Settings.IsReadOnly && !IsAdmin(senderId))
            return Result<Message>.Failure(
                Error.Forbidden("Only admins can post in read-only rooms"));
        
        var messageResult = Message.Create(Id, senderId, content);
        if (messageResult.IsFailure)
            return messageResult;
        
        var message = messageResult.Value;
        _recentMessages.Add(message);
        
        // Keep only last 100 messages in memory
        if (_recentMessages.Count > 100)
            _recentMessages.RemoveAt(0);
        
        // Raise domain event
        RaiseDomainEvent(new MessagePostedEvent(
            Id, message.Id, senderId, content, DateTimeOffset.UtcNow));
        
        return Result<Message>.Success(message);
    }
    
    // Query method - doesn't modify state
    public bool IsMember(UserId userId) => 
        _members.Any(m => m.UserId == userId && m.IsActive);
    
    public bool IsAdmin(UserId userId) =>
        _members.Any(m => m.UserId == userId && 
                        (m.Role == ChatMemberRole.Admin || m.Role == ChatMemberRole.Owner));
}
```

---

## Strong IDs

Strong IDs prevent primitive obsession and provide type safety.

### Creating Strong ID Types

```csharp
// Guid-based Strong ID
public sealed record ChatRoomId : GuidStrongId
{
    private ChatRoomId(Guid value) : base(value) { }
    
    public static ChatRoomId New() => new(Guid.NewGuid());
    
    public static ChatRoomId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("ChatRoomId cannot be empty");
        return new ChatRoomId(value);
    }
    
    public static Result<ChatRoomId> TryFrom(string value)
    {
        if (!Guid.TryParse(value, out var guid))
            return Result<ChatRoomId>.Failure(
                Error.Validation($"Invalid ChatRoomId format: {value}"));
        
        if (guid == Guid.Empty)
            return Result<ChatRoomId>.Failure(
                Error.Validation("ChatRoomId cannot be empty"));
        
        return Result<ChatRoomId>.Success(new ChatRoomId(guid));
    }
}

// Int-based Strong ID (for legacy systems)
public sealed record MessageSequenceNumber : IntStrongId
{
    private MessageSequenceNumber(int value) : base(value) { }
    
    public static MessageSequenceNumber From(int value)
    {
        if (value <= 0)
            throw new ArgumentException("Message sequence must be positive");
        return new MessageSequenceNumber(value);
    }
    
    public MessageSequenceNumber Next() => new(Value + 1);
}

// Custom primitive Strong ID
public sealed record UserId : StrongId<long>
{
    private UserId(long value) : base(value) { }
    
    public static UserId From(long value)
    {
        if (value <= 0)
            throw new ArgumentException("UserId must be positive");
        return new UserId(value);
    }
    
    public static Result<UserId> TryParse(string value)
    {
        if (!long.TryParse(value, out var id))
            return Result<UserId>.Failure(
                Error.Validation($"Invalid UserId format: {value}"));
        
        if (id <= 0)
            return Result<UserId>.Failure(
                Error.Validation("UserId must be positive"));
        
        return Result<UserId>.Success(new UserId(id));
    }
}
```

### Strong ID Features
- Type-safe comparisons (can't compare UserId with ChatRoomId)
- Implicit conversion to primitive when needed
- JSON serialization support via `StrongIdJsonConverterFactory`
- EF Core value conversion support

---

## Domain Events

Domain events capture things that happen in the domain that other parts might be interested in.

### Creating Domain Events

```csharp
// Base domain event
public sealed record ChatRoomCreatedEvent : DomainEvent
{
    public ChatRoomId ChatRoomId { get; }
    public ChatRoomName Name { get; }
    public UserId CreatedBy { get; }
    public DateTimeOffset CreatedAt { get; }
    
    public ChatRoomCreatedEvent(
        ChatRoomId chatRoomId,
        ChatRoomName name,
        UserId createdBy,
        DateTimeOffset createdAt)
    {
        ChatRoomId = chatRoomId;
        Name = name;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }
}

// Event with metadata
public sealed record MessageEditedEvent : DomainEvent
{
    public MessageId MessageId { get; }
    public ChatRoomId ChatRoomId { get; }
    public MessageContent OldContent { get; }
    public MessageContent NewContent { get; }
    public UserId EditedBy { get; }
    public DateTimeOffset EditedAt { get; }
    
    public MessageEditedEvent(
        MessageId messageId,
        ChatRoomId chatRoomId,
        MessageContent oldContent,
        MessageContent newContent,
        UserId editedBy,
        DateTimeOffset editedAt)
    {
        MessageId = messageId;
        ChatRoomId = chatRoomId;
        OldContent = oldContent;
        NewContent = newContent;
        EditedBy = editedBy;
        EditedAt = editedAt;
    }
}
```

### Raising Domain Events in Aggregates

```csharp
public class ChatRoom : AggregateRoot<ChatRoomId>
{
    public Result<Unit> ArchiveRoom(UserId archivedBy)
    {
        if (!IsAdmin(archivedBy))
            return Result<Unit>.Failure(Error.Forbidden("Only admins can archive rooms"));
        
        if (!IsActive)
            return Result<Unit>.Failure(Error.Conflict("Room is already archived"));
        
        IsActive = false;
        ArchivedAt = DateTimeOffset.UtcNow;
        ArchivedBy = archivedBy;
        
        // Raise domain event with metadata
        var @event = new ChatRoomArchivedEvent(Id, archivedBy, ArchivedAt.Value);
        
        // Optional: Add correlation/causation for tracing
        var envelope = @event.WithCorrelation(
            correlationId: CorrelationContext.Current?.CorrelationId,
            causationId: CorrelationContext.Current?.CausationId);
        
        RaiseDomainEvent(@event);
        
        return Result<Unit>.Success(Unit.Value);
    }
}
```

---

## Business Rules

Business rules encapsulate domain invariants and validation logic.

### Creating Business Rules

```csharp
public sealed class MessageContentRule : BusinessRule
{
    private readonly string _content;
    private readonly int _maxLength;
    
    public MessageContentRule(string content, int maxLength = 2000)
    {
        _content = content;
        _maxLength = maxLength;
    }
    
    public override Result<Unit> Check()
    {
        if (string.IsNullOrWhiteSpace(_content))
            return Result<Unit>.Failure(
                Error.Validation("Message content cannot be empty"));
        
        if (_content.Length > _maxLength)
            return Result<Unit>.Failure(
                Error.Validation($"Message exceeds maximum length: {_content.Length}/{_maxLength}"));
        
        if (ContainsForbiddenWords(_content))
            return Result<Unit>.Failure(
                Error.Validation("Message contains forbidden content"));
        
        return Result<Unit>.Success(Unit.Value);
    }
    
    private bool ContainsForbiddenWords(string content)
    {
        // Check against forbidden words list
        return false;
    }
}

// Using RuleBuilder for complex rules
public sealed class ChatRoomCreationRules
{
    public static Result<Unit> Validate(
        ChatRoomName name,
        ChatRoomType type,
        UserId creatorId,
        int existingRoomCount)
    {
        return RuleBuilder.Create()
            .Add(new NonEmptyRule(name, "Room name"))
            .Add(new ValidEnumRule(type, "Room type"))
            .Add(new UserCanCreateRoomRule(creatorId, existingRoomCount))
            .Add(new UniqueRoomNameRule(name)) // Would check against repository
            .Build()
            .Check();
    }
}

public sealed class UserCanCreateRoomRule : BusinessRule
{
    private readonly UserId _userId;
    private readonly int _existingRoomCount;
    private const int MaxRoomsPerUser = 10;
    
    public UserCanCreateRoomRule(UserId userId, int existingRoomCount)
    {
        _userId = userId;
        _existingRoomCount = existingRoomCount;
    }
    
    public override Result<Unit> Check()
    {
        if (_existingRoomCount >= MaxRoomsPerUser)
            return Result<Unit>.Failure(
                Error.Validation($"User has reached maximum room limit: {MaxRoomsPerUser}"));
        
        return Result<Unit>.Success(Unit.Value);
    }
}
```

### Using Rules in Domain Logic

```csharp
public class ChatRoom : AggregateRoot<ChatRoomId>
{
    public Result<Message> PostMessage(
        UserId senderId,
        string messageText,
        IReadOnlyList<AttachmentInfo>? attachments = null)
    {
        // Check member permissions
        var memberRule = new MemberCanPostMessageRule(this, senderId);
        var memberCheck = memberRule.Check();
        if (memberCheck.IsFailure)
            return Result<Message>.Failure(memberCheck.Error);
        
        // Validate message content
        var contentRule = new MessageContentRule(messageText);
        var contentCheck = contentRule.Check();
        if (contentCheck.IsFailure)
            return Result<Message>.Failure(contentCheck.Error);
        
        // Validate attachments if present
        if (attachments?.Count > 0)
        {
            var attachmentRule = new MessageAttachmentsRule(attachments);
            var attachmentCheck = attachmentRule.Check();
            if (attachmentCheck.IsFailure)
                return Result<Message>.Failure(attachmentCheck.Error);
        }
        
        // Create message
        var contentResult = MessageContent.Create(messageText);
        if (contentResult.IsFailure)
            return Result<Message>.Failure(contentResult.Error);
        
        var message = Message.Create(Id, senderId, contentResult.Value);
        // ... rest of implementation
    }
}
```

---

## Result Pattern

The Result pattern provides explicit error handling without exceptions.

### Basic Result Usage

```csharp
// Returning success
public Result<ChatRoom> CreateChatRoom(string name)
{
    var nameResult = ChatRoomName.Create(name);
    if (nameResult.IsFailure)
        return Result<ChatRoom>.Failure(nameResult.Error);
    
    var room = new ChatRoom(nameResult.Value);
    return Result<ChatRoom>.Success(room);
}

// Chaining operations with Bind
public Result<ChatRoom> CreateAndConfigureRoom(
    string name,
    ChatRoomSettings settings)
{
    return ChatRoomName.Create(name)
        .Bind(roomName => ChatRoom.Create(roomName, ChatRoomType.Public, UserId.System))
        .Bind(room => room.UpdateSettings(settings))
        .Map(room => room); // Final transformation if needed
}

// Using Match for different paths
public IActionResult HandleCreateRoom(CreateRoomCommand command)
{
    var result = CreateChatRoom(command.Name);
    
    return result.Match(
        success: room => Ok(new { roomId = room.Id.Value }),
        failure: error => error.Type switch
        {
            ErrorType.Validation => BadRequest(error.Message),
            ErrorType.NotFound => NotFound(error.Message),
            ErrorType.Conflict => Conflict(error.Message),
            ErrorType.Forbidden => Forbid(error.Message),
            _ => StatusCode(500, "Internal server error")
        });
}
```

### Advanced Result Patterns

```csharp
// Async operations with Results
public async Task<Result<ChatRoom>> CreateRoomAsync(
    CreateChatRoomCommand command,
    IChatRoomRepository repository,
    CancellationToken ct)
{
    // Validate name
    var nameResult = ChatRoomName.Create(command.Name);
    if (nameResult.IsFailure)
        return Result<ChatRoom>.Failure(nameResult.Error);
    
    // Check for duplicates
    var existingRoom = await repository
        .FindByNameAsync(nameResult.Value, ct);
    
    if (existingRoom != null)
        return Result<ChatRoom>.Failure(
            Error.Conflict($"Room with name '{command.Name}' already exists"));
    
    // Create room
    var roomResult = ChatRoom.Create(
        nameResult.Value,
        command.Type,
        command.CreatorId);
    
    if (roomResult.IsFailure)
        return roomResult;
    
    // Save to repository
    try
    {
        await repository.AddAsync(roomResult.Value, ct);
        await repository.SaveChangesAsync(ct);
        return roomResult;
    }
    catch (DbUpdateException ex)
    {
        return Result<ChatRoom>.Failure(
            Error.Unexpected("Failed to save chat room", ex));
    }
}

// Combining multiple Results
public Result<ChatRoomStats> CalculateRoomStats(ChatRoom room)
{
    return Result.Combine(
        ValidateRoom(room),
        ValidateMembers(room.Members),
        ValidateMessages(room.RecentMessages))
    .Map(() => new ChatRoomStats
    {
        MemberCount = room.Members.Count,
        MessageCount = room.RecentMessages.Count,
        ActiveMemberCount = room.Members.Count(m => m.IsActive),
        LastActivityAt = room.RecentMessages
            .OrderByDescending(m => m.SentAt)
            .FirstOrDefault()?.SentAt
    });
}

// Railway-oriented programming
public Result<ProcessedMessage> ProcessMessage(RawMessage raw)
{
    return ParseMessage(raw)
        .Bind(ValidateMessage)
        .Bind(SanitizeContent)
        .Bind(CheckSpam)
        .Bind(ExtractMentions)
        .Bind(AttachMetadata)
        .Map(FormatForDisplay);
}
```

### Error Types and Creation

```csharp
// Creating specific errors
var validationError = Error.Validation("Invalid email format");
var notFoundError = Error.NotFound("User", userId.Value);
var conflictError = Error.Conflict("Email already registered");
var forbiddenError = Error.Forbidden("Insufficient permissions");
var unexpectedError = Error.Unexpected("Database connection failed", exception);

// Custom error with metadata
var customError = new Error(
    "CHAT_ROOM_FULL",
    "Cannot join chat room: maximum capacity reached",
    ErrorType.Validation,
    new Dictionary<string, object>
    {
        ["RoomId"] = roomId.Value,
        ["CurrentMembers"] = 100,
        ["MaxMembers"] = 100
    });
```

---

## CQRS Implementation

### Commands (Write Operations)

```csharp
// Command without response
public sealed record ArchiveChatRoomCommand : ICommand
{
    public ChatRoomId RoomId { get; init; }
    public UserId ArchivedBy { get; init; }
    public string? Reason { get; init; }
}

// Command with response
public sealed record CreateChatRoomCommand : ICommand<ChatRoomDto>
{
    public string Name { get; init; } = default!;
    public ChatRoomType Type { get; init; }
    public UserId CreatorId { get; init; }
    public ChatRoomSettings? Settings { get; init; }
}

// Command handler
public sealed class CreateChatRoomCommandHandler : ICommandHandler<CreateChatRoomCommand, ChatRoomDto>
{
    private readonly IChatRoomRepository _repository;
    private readonly IEventDispatcher _eventDispatcher;
    private readonly ILogger<CreateChatRoomCommandHandler> _logger;
    
    public CreateChatRoomCommandHandler(
        IChatRoomRepository repository,
        IEventDispatcher eventDispatcher,
        ILogger<CreateChatRoomCommandHandler> logger)
    {
        _repository = repository;
        _eventDispatcher = eventDispatcher;
        _logger = logger;
    }
    
    public async Task<Result<ChatRoomDto>> Handle(
        CreateChatRoomCommand command,
        CancellationToken cancellationToken)
    {
        // Validate command
        var validationResult = await ValidateCommand(command, cancellationToken);
        if (validationResult.IsFailure)
            return Result<ChatRoomDto>.Failure(validationResult.Error);
        
        // Create room name value object
        var nameResult = ChatRoomName.Create(command.Name);
        if (nameResult.IsFailure)
            return Result<ChatRoomDto>.Failure(nameResult.Error);
        
        // Check for duplicate names
        var existing = await _repository.FindByNameAsync(nameResult.Value, cancellationToken);
        if (existing != null)
            return Result<ChatRoomDto>.Failure(
                Error.Conflict($"Chat room '{command.Name}' already exists"));
        
        // Create aggregate
        var roomResult = ChatRoom.Create(
            nameResult.Value,
            command.Type,
            command.CreatorId,
            command.Settings);
        
        if (roomResult.IsFailure)
            return Result<ChatRoomDto>.Failure(roomResult.Error);
        
        var room = roomResult.Value;
        
        // Persist
        await _repository.AddAsync(room, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        
        // Dispatch domain events
        await _eventDispatcher.DispatchAsync(room.DomainEvents, cancellationToken);
        room.ClearDomainEvents();
        
        // Map to DTO
        var dto = ChatRoomDto.FromDomain(room);
        
        _logger.LogInformation(
            "Chat room {RoomId} created by user {UserId}",
            room.Id,
            command.CreatorId);
        
        return Result<ChatRoomDto>.Success(dto);
    }
    
    private async Task<Result<Unit>> ValidateCommand(
        CreateChatRoomCommand command,
        CancellationToken cancellationToken)
    {
        // Check user exists and can create rooms
        var userExists = await _repository.UserExistsAsync(command.CreatorId, cancellationToken);
        if (!userExists)
            return Result<Unit>.Failure(Error.NotFound("User", command.CreatorId.Value));
        
        // Check user's room limit
        var userRoomCount = await _repository.CountUserRoomsAsync(command.CreatorId, cancellationToken);
        if (userRoomCount >= 10)
            return Result<Unit>.Failure(
                Error.Validation("User has reached maximum room limit"));
        
        return Result<Unit>.Success(Unit.Value);
    }
}
```

### Queries (Read Operations)

```csharp
// Query with pagination
public sealed record GetChatRoomMessagesQuery : IQuery<PagedResult<MessageDto>>
{
    public ChatRoomId RoomId { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public MessageSortOrder SortOrder { get; init; } = MessageSortOrder.Newest;
}

// Query handler
public sealed class GetChatRoomMessagesQueryHandler : 
    IQueryHandler<GetChatRoomMessagesQuery, PagedResult<MessageDto>>
{
    private readonly IMessageReadRepository _repository;
    private readonly ICacheService _cache;
    private readonly ICurrentUserService _currentUser;
    
    public GetChatRoomMessagesQueryHandler(
        IMessageReadRepository repository,
        ICacheService cache,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _cache = cache;
        _currentUser = currentUser;
    }
    
    public async Task<Result<PagedResult<MessageDto>>> Handle(
        GetChatRoomMessagesQuery query,
        CancellationToken cancellationToken)
    {
        // Check user has access to room
        var userId = _currentUser.UserId;
        var hasAccess = await _repository.UserHasAccessToRoomAsync(
            userId,
            query.RoomId,
            cancellationToken);
        
        if (!hasAccess)
            return Result<PagedResult<MessageDto>>.Failure(
                Error.Forbidden("You don't have access to this chat room"));
        
        // Try cache first
        var cacheKey = $"messages:{query.RoomId}:{query.PageNumber}:{query.PageSize}:{query.SortOrder}";
        var cached = await _cache.GetAsync<PagedResult<MessageDto>>(cacheKey, cancellationToken);
        if (cached != null)
            return Result<PagedResult<MessageDto>>.Success(cached);
        
        // Query from database
        var messages = await _repository.GetMessagesAsync(
            query.RoomId,
            query.PageNumber,
            query.PageSize,
            query.SortOrder,
            cancellationToken);
        
        // Map to DTOs
        var dtos = messages.Items.Select(MessageDto.FromDomain).ToList();
        
        var result = new PagedResult<MessageDto>(
            dtos,
            messages.TotalCount,
            query.PageNumber,
            query.PageSize);
        
        // Cache for 5 minutes
        await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5), cancellationToken);
        
        return Result<PagedResult<MessageDto>>.Success(result);
    }
}
```

---

## Specifications

Specifications encapsulate query logic and can be composed.

### Creating Specifications

```csharp
// Base specification for chat rooms
public sealed class ActiveChatRoomSpec : Specification<ChatRoom>
{
    public override Expression<Func<ChatRoom, bool>> ToExpression()
    {
        return room => room.IsActive && !room.DeletedAt.HasValue;
    }
}

// Specification with parameters
public sealed class ChatRoomByOwnerSpec : Specification<ChatRoom>
{
    private readonly UserId _ownerId;
    
    public ChatRoomByOwnerSpec(UserId ownerId)
    {
        _ownerId = ownerId;
    }
    
    public override Expression<Func<ChatRoom, bool>> ToExpression()
    {
        return room => room.OwnerId == _ownerId;
    }
}

// Composite specifications
public sealed class UserAccessibleChatRoomsSpec : Specification<ChatRoom>
{
    private readonly UserId _userId;
    
    public UserAccessibleChatRoomsSpec(UserId userId)
    {
        _userId = userId;
    }
    
    public override Expression<Func<ChatRoom, bool>> ToExpression()
    {
        return room => room.IsActive &&
                      !room.DeletedAt.HasValue &&
                      (room.Type == ChatRoomType.Public ||
                       room.Members.Any(m => m.UserId == _userId && m.IsActive));
    }
}

// Using specifications in repositories
public class ChatRoomRepository : IChatRoomRepository
{
    private readonly AppDbContext _context;
    
    public async Task<IReadOnlyList<ChatRoom>> FindAsync(
        Specification<ChatRoom> spec,
        CancellationToken cancellationToken)
    {
        return await _context.ChatRooms
            .Where(spec.ToExpression())
            .ToListAsync(cancellationToken);
    }
    
    public async Task<ChatRoom?> FindSingleAsync(
        Specification<ChatRoom> spec,
        CancellationToken cancellationToken)
    {
        return await _context.ChatRooms
            .Where(spec.ToExpression())
            .FirstOrDefaultAsync(cancellationToken);
    }
}

// Combining specifications
public async Task<IReadOnlyList<ChatRoom>> GetUserOwnedActiveRooms(UserId userId)
{
    var spec = new ActiveChatRoomSpec()
        .And(new ChatRoomByOwnerSpec(userId))
        .And(new ChatRoomCreatedAfterSpec(DateTimeOffset.UtcNow.AddMonths(-6)));
    
    return await _repository.FindAsync(spec, CancellationToken.None);
}
```

---

## Practical Chat Module Examples

### Complete Chat Module Domain Model

```csharp
// 1. Strong IDs
public sealed record ChatRoomId : GuidStrongId
{
    private ChatRoomId(Guid value) : base(value) { }
    public static ChatRoomId New() => new(Guid.NewGuid());
    public static ChatRoomId From(Guid value) => new(value);
}

public sealed record MessageId : GuidStrongId
{
    private MessageId(Guid value) : base(value) { }
    public static MessageId New() => new(Guid.NewGuid());
    public static MessageId From(Guid value) => new(value);
}

public sealed record UserId : GuidStrongId
{
    private UserId(Guid value) : base(value) { }
    public static UserId From(Guid value) => new(value);
}

// 2. Value Objects
public sealed record MessageContent : ValueObject
{
    public string Text { get; }
    public bool IsEdited { get; }
    public IReadOnlyList<UserId> MentionedUsers { get; }
    public IReadOnlyList<string> Hashtags { get; }
    
    private MessageContent(
        string text,
        bool isEdited,
        IReadOnlyList<UserId> mentionedUsers,
        IReadOnlyList<string> hashtags)
    {
        Text = text;
        IsEdited = isEdited;
        MentionedUsers = mentionedUsers;
        Hashtags = hashtags;
    }
    
    public static Result<MessageContent> Create(string text, bool isEdited = false)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Result<MessageContent>.Failure(
                Error.Validation("Message cannot be empty"));
        
        if (text.Length > 2000)
            return Result<MessageContent>.Failure(
                Error.Validation("Message exceeds 2000 characters"));
        
        var mentions = ExtractMentions(text);
        var hashtags = ExtractHashtags(text);
        
        return Result<MessageContent>.Success(
            new MessageContent(text.Trim(), isEdited, mentions, hashtags));
    }
    
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Text;
        yield return IsEdited;
    }
    
    public override Validation<Unit> Validate()
    {
        if (string.IsNullOrWhiteSpace(Text))
            return Validation<Unit>.Failure(Error.Validation("Message cannot be empty"));
        
        if (Text.Length > 2000)
            return Validation<Unit>.Failure(Error.Validation("Message too long"));
        
        return Validation<Unit>.Success(Unit.Value);
    }
    
    private static IReadOnlyList<UserId> ExtractMentions(string text)
    {
        var pattern = @"@([a-zA-Z0-9-]+)";
        var matches = Regex.Matches(text, pattern);
        
        return matches
            .Select(m => m.Groups[1].Value)
            .Where(id => Guid.TryParse(id, out _))
            .Select(id => UserId.From(Guid.Parse(id)))
            .Distinct()
            .ToList();
    }
    
    private static IReadOnlyList<string> ExtractHashtags(string text)
    {
        var pattern = @"#(\w+)";
        return Regex.Matches(text, pattern)
            .Select(m => m.Groups[1].Value.ToLowerInvariant())
            .Distinct()
            .ToList();
    }
}

// 3. Entities
public sealed class Message : Entity<MessageId>
{
    public ChatRoomId ChatRoomId { get; private set; }
    public UserId SenderId { get; private set; }
    public MessageContent Content { get; private set; }
    public MessageStatus Status { get; private set; }
    public DateTimeOffset SentAt { get; private set; }
    public DateTimeOffset? EditedAt { get; private set; }
    public MessageId? ReplyToId { get; private set; }
    public IReadOnlyList<Reaction> Reactions { get; private set; }
    
    private Message() : base() 
    {
        Reactions = new List<Reaction>();
    }
    
    public static Result<Message> Create(
        ChatRoomId roomId,
        UserId senderId,
        MessageContent content,
        MessageId? replyToId = null)
    {
        ArgumentNullException.ThrowIfNull(roomId);
        ArgumentNullException.ThrowIfNull(senderId);
        ArgumentNullException.ThrowIfNull(content);
        
        if (!content.IsValid)
            return Result<Message>.Failure(content.ValidationErrors.First());
        
        var message = new Message
        {
            Id = MessageId.New(),
            ChatRoomId = roomId,
            SenderId = senderId,
            Content = content,
            Status = MessageStatus.Sent,
            SentAt = DateTimeOffset.UtcNow,
            ReplyToId = replyToId,
            Reactions = new List<Reaction>()
        };
        
        return Result<Message>.Success(message);
    }
    
    public Result<Unit> Edit(MessageContent newContent, UserId editorId)
    {
        if (SenderId != editorId)
            return Result<Unit>.Failure(Error.Forbidden("Only sender can edit"));
        
        if (Status == MessageStatus.Deleted)
            return Result<Unit>.Failure(Error.Conflict("Cannot edit deleted message"));
        
        var timeSinceSent = DateTimeOffset.UtcNow - SentAt;
        if (timeSinceSent > TimeSpan.FromHours(24))
            return Result<Unit>.Failure(
                Error.Validation("Cannot edit messages older than 24 hours"));
        
        Content = newContent;
        EditedAt = DateTimeOffset.UtcNow;
        
        return Result<Unit>.Success(Unit.Value);
    }
    
    public Result<Reaction> AddReaction(UserId userId, string emoji)
    {
        if (string.IsNullOrWhiteSpace(emoji))
            return Result<Reaction>.Failure(Error.Validation("Invalid emoji"));
        
        var existing = Reactions.FirstOrDefault(r => r.UserId == userId && r.Emoji == emoji);
        if (existing != null)
            return Result<Reaction>.Failure(Error.Conflict("Reaction already exists"));
        
        var reaction = new Reaction(userId, emoji, DateTimeOffset.UtcNow);
        ((List<Reaction>)Reactions).Add(reaction);
        
        return Result<Reaction>.Success(reaction);
    }
}

// 4. Aggregate Root
public sealed class ChatRoom : AggregateRoot<ChatRoomId>
{
    private readonly List<ChatMember> _members = new();
    private readonly List<Message> _pinnedMessages = new();
    
    public ChatRoomName Name { get; private set; }
    public ChatRoomDescription? Description { get; private set; }
    public ChatRoomType Type { get; private set; }
    public UserId OwnerId { get; private set; }
    public ChatRoomSettings Settings { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastActivityAt { get; private set; }
    public bool IsActive { get; private set; }
    
    public IReadOnlyList<ChatMember> Members => _members.AsReadOnly();
    public IReadOnlyList<Message> PinnedMessages => _pinnedMessages.AsReadOnly();
    public int MemberCount => _members.Count(m => m.IsActive);
    
    private ChatRoom() : base() { }
    
    public static Result<ChatRoom> Create(
        ChatRoomName name,
        ChatRoomType type,
        UserId ownerId,
        ChatRoomDescription? description = null,
        ChatRoomSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(ownerId);
        
        if (!name.IsValid)
            return Result<ChatRoom>.Failure(name.ValidationErrors.First());
        
        if (description != null && !description.IsValid)
            return Result<ChatRoom>.Failure(description.ValidationErrors.First());
        
        var room = new ChatRoom
        {
            Id = ChatRoomId.New(),
            Name = name,
            Description = description,
            Type = type,
            OwnerId = ownerId,
            Settings = settings ?? ChatRoomSettings.Default,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow,
            IsActive = true
        };
        
        // Add creator as owner
        var owner = ChatMember.CreateOwner(ownerId, room.Id);
        room._members.Add(owner);
        
        // Raise event
        room.RaiseDomainEvent(new ChatRoomCreatedEvent(
            room.Id,
            room.Name,
            room.Type,
            room.OwnerId,
            room.CreatedAt));
        
        return Result<ChatRoom>.Success(room);
    }
    
    public Result<ChatMember> AddMember(
        UserId userId,
        UserId addedBy,
        ChatMemberRole role = ChatMemberRole.Member)
    {
        // Validate permissions
        if (!IsAdmin(addedBy))
            return Result<ChatMember>.Failure(
                Error.Forbidden("Only admins can add members"));
        
        // Check if already member
        if (IsMember(userId))
            return Result<ChatMember>.Failure(
                Error.Conflict("User is already a member"));
        
        // Check capacity
        if (_members.Count >= Settings.MaxMembers)
            return Result<ChatMember>.Failure(
                Error.Validation($"Room has reached capacity: {Settings.MaxMembers}"));
        
        // For private rooms, check if user was invited
        if (Type == ChatRoomType.Private)
        {
            // Additional invitation logic here
        }
        
        var member = new ChatMember(
            userId,
            Id,
            role,
            DateTimeOffset.UtcNow);
        
        _members.Add(member);
        LastActivityAt = DateTimeOffset.UtcNow;
        
        // Raise event
        RaiseDomainEvent(new MemberJoinedChatRoomEvent(
            Id,
            userId,
            addedBy,
            role,
            DateTimeOffset.UtcNow));
        
        return Result<ChatMember>.Success(member);
    }
    
    public Result<Unit> RemoveMember(UserId userId, UserId removedBy)
    {
        // Can't remove owner
        if (userId == OwnerId)
            return Result<Unit>.Failure(
                Error.Validation("Cannot remove room owner"));
        
        // Check permissions
        var canRemove = IsAdmin(removedBy) || userId == removedBy;
        if (!canRemove)
            return Result<Unit>.Failure(
                Error.Forbidden("Insufficient permissions"));
        
        var member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member == null)
            return Result<Unit>.Failure(
                Error.NotFound("Member not found"));
        
        _members.Remove(member);
        LastActivityAt = DateTimeOffset.UtcNow;
        
        // Raise event
        RaiseDomainEvent(new MemberLeftChatRoomEvent(
            Id,
            userId,
            removedBy,
            userId == removedBy ? LeaveReason.SelfRemoved : LeaveReason.Kicked,
            DateTimeOffset.UtcNow));
        
        return Result<Unit>.Success(Unit.Value);
    }
    
    public Result<Unit> PromoteMember(UserId userId, UserId promotedBy)
    {
        if (!IsOwner(promotedBy))
            return Result<Unit>.Failure(
                Error.Forbidden("Only owner can promote members"));
        
        var member = _members.FirstOrDefault(m => m.UserId == userId);
        if (member == null)
            return Result<Unit>.Failure(
                Error.NotFound("Member not found"));
        
        if (member.Role == ChatMemberRole.Admin)
            return Result<Unit>.Failure(
                Error.Conflict("Member is already an admin"));
        
        member.PromoteToAdmin();
        LastActivityAt = DateTimeOffset.UtcNow;
        
        RaiseDomainEvent(new MemberPromotedEvent(
            Id,
            userId,
            promotedBy,
            ChatMemberRole.Admin,
            DateTimeOffset.UtcNow));
        
        return Result<Unit>.Success(Unit.Value);
    }
    
    public Result<Unit> UpdateSettings(
        ChatRoomSettings newSettings,
        UserId updatedBy)
    {
        if (!IsAdmin(updatedBy))
            return Result<Unit>.Failure(
                Error.Forbidden("Only admins can update settings"));
        
        var oldSettings = Settings;
        Settings = newSettings;
        LastActivityAt = DateTimeOffset.UtcNow;
        
        RaiseDomainEvent(new ChatRoomSettingsUpdatedEvent(
            Id,
            oldSettings,
            newSettings,
            updatedBy,
            DateTimeOffset.UtcNow));
        
        return Result<Unit>.Success(Unit.Value);
    }
    
    public Result<Unit> PinMessage(MessageId messageId, UserId pinnedBy)
    {
        if (!IsAdmin(pinnedBy))
            return Result<Unit>.Failure(
                Error.Forbidden("Only admins can pin messages"));
        
        if (_pinnedMessages.Count >= 10)
            return Result<Unit>.Failure(
                Error.Validation("Maximum pinned messages reached"));
        
        if (_pinnedMessages.Any(m => m.Id == messageId))
            return Result<Unit>.Failure(
                Error.Conflict("Message is already pinned"));
        
        // In real implementation, would load message from repository
        // For this example, we'll assume we have it
        
        LastActivityAt = DateTimeOffset.UtcNow;
        
        RaiseDomainEvent(new MessagePinnedEvent(
            Id,
            messageId,
            pinnedBy,
            DateTimeOffset.UtcNow));
        
        return Result<Unit>.Success(Unit.Value);
    }
    
    // Query methods
    public bool IsMember(UserId userId) =>
        _members.Any(m => m.UserId == userId && m.IsActive);
    
    public bool IsAdmin(UserId userId) =>
        _members.Any(m => m.UserId == userId && 
                        m.IsActive &&
                        (m.Role == ChatMemberRole.Admin || m.Role == ChatMemberRole.Owner));
    
    public bool IsOwner(UserId userId) =>
        userId == OwnerId;
    
    public ChatMember? GetMember(UserId userId) =>
        _members.FirstOrDefault(m => m.UserId == userId);
}

// 5. Domain Events
public sealed record ChatRoomCreatedEvent(
    ChatRoomId RoomId,
    ChatRoomName Name,
    ChatRoomType Type,
    UserId CreatedBy,
    DateTimeOffset CreatedAt) : DomainEvent;

public sealed record MemberJoinedChatRoomEvent(
    ChatRoomId RoomId,
    UserId UserId,
    UserId AddedBy,
    ChatMemberRole Role,
    DateTimeOffset JoinedAt) : DomainEvent;

public sealed record MessagePostedEvent(
    ChatRoomId RoomId,
    MessageId MessageId,
    UserId SenderId,
    MessageContent Content,
    DateTimeOffset SentAt) : DomainEvent;

// 6. Business Rules
public sealed class CanJoinChatRoomRule : BusinessRule
{
    private readonly ChatRoom _room;
    private readonly UserId _userId;
    
    public CanJoinChatRoomRule(ChatRoom room, UserId userId)
    {
        _room = room;
        _userId = userId;
    }
    
    public override Result<Unit> Check()
    {
        if (!_room.IsActive)
            return Result<Unit>.Failure(Error.Validation("Room is not active"));
        
        if (_room.IsMember(_userId))
            return Result<Unit>.Failure(Error.Conflict("Already a member"));
        
        if (_room.MemberCount >= _room.Settings.MaxMembers)
            return Result<Unit>.Failure(Error.Validation("Room is full"));
        
        if (_room.Type == ChatRoomType.Private)
            return Result<Unit>.Failure(Error.Forbidden("Private room requires invitation"));
        
        return Result<Unit>.Success(Unit.Value);
    }
}

// 7. Specifications
public sealed class PublicChatRoomsSpec : Specification<ChatRoom>
{
    public override Expression<Func<ChatRoom, bool>> ToExpression()
    {
        return room => room.Type == ChatRoomType.Public && 
                      room.IsActive &&
                      !room.DeletedAt.HasValue;
    }
}

public sealed class UserChatRoomsSpec : Specification<ChatRoom>
{
    private readonly UserId _userId;
    
    public UserChatRoomsSpec(UserId userId)
    {
        _userId = userId;
    }
    
    public override Expression<Func<ChatRoom, bool>> ToExpression()
    {
        return room => room.Members.Any(m => m.UserId == _userId && m.IsActive);
    }
}
```

---

## Best Practices Summary

1. **Always use Result<T>** for operations that can fail
2. **Create Strong IDs** for all entity identifiers
3. **Encapsulate business logic** in domain entities and value objects
4. **Raise domain events** for important state changes
5. **Use specifications** for reusable query logic
6. **Validate invariants** in factories and business methods
7. **Keep aggregates small** and focused on consistency boundaries
8. **Use value objects** for concepts without identity
9. **Implement business rules** as separate, testable classes
10. **Follow CQRS** to separate reads from writes

## Testing Domain Logic

```csharp
[Fact]
public void ChatRoom_Create_Should_Add_Owner_As_First_Member()
{
    // Arrange
    var name = ChatRoomName.Create("Test Room").Value;
    var ownerId = UserId.From(Guid.NewGuid());
    
    // Act
    var result = ChatRoom.Create(name, ChatRoomType.Public, ownerId);
    
    // Assert
    result.IsSuccess.Should().BeTrue();
    var room = result.Value;
    room.Members.Should().HaveCount(1);
    room.Members.First().UserId.Should().Be(ownerId);
    room.Members.First().Role.Should().Be(ChatMemberRole.Owner);
}

[Fact]
public void Message_Edit_Should_Fail_When_User_Is_Not_Sender()
{
    // Arrange
    var message = Message.Create(
        ChatRoomId.New(),
        UserId.From(Guid.NewGuid()),
        MessageContent.Create("Original").Value).Value;
    
    var differentUser = UserId.From(Guid.NewGuid());
    var newContent = MessageContent.Create("Edited").Value;
    
    // Act
    var result = message.Edit(newContent, differentUser);
    
    // Assert
    result.IsFailure.Should().BeTrue();
    result.Error.Type.Should().Be(ErrorType.Forbidden);
}
```

This comprehensive guide provides a complete blueprint for implementing domain layers using the Axon Backend BuildingBlocks, with extensive examples from a Chat Module implementation.