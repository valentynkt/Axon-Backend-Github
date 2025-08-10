# Chat Domain Module — Source of Truth (MVP)

## Overview

The Chat domain module implements single-owner conversational functionality between users and AI assistants using Domain-Driven Design (DDD) principles. It handles the complete conversation lifecycle from initiation through completion, enforcing strict business rules and maintaining message integrity.

**Problem Statement**: Enable users to engage in structured conversations with LLM assistants while maintaining data integrity, enforcing business constraints, and providing audit trails through domain events.

**Scope**: Domain layer only. Contains aggregates, entities, value objects, domain events, and business rules. No infrastructure, application, or presentation concerns.

### Ubiquous Language

- **Conversation**: An aggregate root representing a chat session between an owner (user) and assistant
- **Message**: A single conversational exchange with role (User/Assistant), content, and sequence
- **Owner**: The user who initiated and owns the conversation (identified by UserId)
- **Role**: Distinguishes between User messages and Assistant responses
- **Assistant**: The AI system that responds to user messages
- **Sequence**: 1-based consecutive numbering of messages within a conversation
- **Turn-Taking**: Business rule governing message ordering (no consecutive assistant messages)

## Public API Surface (Locked)

The following types constitute the stable public contract of the Chat domain. All other types are internal implementation details.

### Whitelisted Public Types

**Aggregates & Status:**
- `Conversation` - Main aggregate root for chat conversations
- `ConversationStatus` - Enumeration: Active, Completed

**Child Entities:**
- `Message` - Immutable message entity (public type, internal construction only)

**Value Objects:**
- `ConversationTitle` - Validated conversation title (1-200 characters)
- `MessageContent` - Validated message content (1-100,000 characters)  
- `MessageRole` - Message role enumeration (User, Assistant)

**Strong IDs:**
- `ConversationId` - Type-safe conversation identifier
- `MessageId` - Type-safe message identifier
- `UserId` - Type-safe user identifier

**Domain Events:**
- `ConversationStartedEvent` - Raised when conversation begins
- `UserMessageAppendedEvent` - Raised when user adds message
- `AssistantMessageAppendedEvent` - Raised when assistant responds
- `ConversationTitleUpdatedEvent` - Raised when title changes
- `ConversationCompletedEvent` - Raised when conversation ends

**Time Abstractions:**
- `IClock` - Interface for deterministic time access
- `SystemClock` - Production implementation of IClock

**Module Marker:**
- `AssemblyMarker` - For test assembly references

### Conversation Public Methods

```csharp
// Factory method
public static Result<Conversation> Start(UserId ownerId, string? titleOrNull, IClock clock)

// Message operations  
public Result<Message> AppendUserMessage(MessageContent content, IClock clock)
public Result<Message> AppendAssistantMessage(MessageContent content, IClock clock)

// Title operations
public Result<Unit> UpdateTitle(string newTitleValue, IClock clock)

// Lifecycle operations
public Result<Unit> Complete(IClock clock)

// Query operations
public bool BelongsTo(UserId userId)

// Properties (read-only)
public UserId OwnerId { get; }
public ConversationStatus Status { get; }  
public string Title { get; }
public bool IsDefaultTitle { get; }
public int MessageCount { get; }
public IReadOnlyList<Message> MessagesOrdered { get; }
public DateTimeOffset CreatedAtUtc { get; }
public DateTimeOffset UpdatedAtUtc { get; }
public bool IsActive { get; }
```

## Invariants & Business Rules (MVP)

### Turn-Taking Policy
- ✅ **Assistant may be first**: Empty conversations allow assistant messages
- ✅ **Consecutive user allowed**: Multiple user messages in a row are permitted
- ❌ **Consecutive assistant forbidden**: Two assistant messages in a row are blocked
- ✅ **Normal alternation**: User → Assistant → User → Assistant is the ideal pattern

### Content & Length Limits
- **Title (Start)**: ≤ 200 characters when provided; empty/null allowed (sets `isDefaultTitle=true`)
- **Title (Update)**: 1-200 characters required; empty strings forbidden on updates
- **Message Content**: 1-100,000 characters required; empty content forbidden
- **Message Count**: Maximum 10,000 messages per conversation

### Ownership & Status Rules
- **Owner Required**: All conversations must have a valid non-empty UserId owner
- **Active Status Required**: All mutations require conversation status to be Active
- **Completion Rules**: Requires ≥ 1 message; completed conversations cannot be mutated

### Sequence Integrity
- **1-Based Consecutive**: Message sequences start at 1 and increment without gaps
- **Success-Only Increments**: Failed operations never affect next sequence number
- **Computed by Aggregate**: Callers never provide sequence; always computed internally

### Time Discipline
- **IClock Only**: All timestamps come from injected IClock abstraction
- **UTC Timestamps**: All DateTimeOffset values stored and emitted in UTC
- **Forbidden**: Direct usage of DateTime.Now, DateTime.UtcNow, DateTimeOffset.Now, DateTimeOffset.UtcNow

## Error Catalog Mapping (Operation → Errors)

Exact error codes returned by each operation. See `04_VALIDATION & ERROR CATALOG.md` for complete error definitions.

### Start(ownerId, titleOrNull, clock)
- `CHAT_CONVERSATION_OWNER_REQUIRED` - Missing or empty owner ID
- `CHAT_CONVERSATION_TITLE_TOO_LONG` - Provided title exceeds 200 characters

### AppendUserMessage(content, clock)  
- `CHAT_CONVERSATION_NOT_ACTIVE` - Conversation status is not Active
- `CHAT_MESSAGE_CONTENT_EMPTY` - Message content is null, empty, or whitespace
- `CHAT_MESSAGE_CONTENT_TOO_LONG` - Content exceeds 100,000 characters
- `CHAT_MESSAGE_LIMIT_EXCEEDED` - Conversation already has 10,000 messages

### AppendAssistantMessage(content, clock)
- `CHAT_CONVERSATION_NOT_ACTIVE` - Conversation status is not Active  
- `CHAT_MESSAGE_CONTENT_EMPTY` - Message content is null, empty, or whitespace
- `CHAT_MESSAGE_CONTENT_TOO_LONG` - Content exceeds 100,000 characters
- `CHAT_MESSAGE_LIMIT_EXCEEDED` - Conversation already has 10,000 messages
- `CHAT_MESSAGE_ASSISTANT_TURN_VIOLATION` - Last message was also from assistant

### UpdateTitle(newTitleValue, clock)
- `CHAT_CONVERSATION_NOT_ACTIVE` - Conversation status is not Active
- `CHAT_CONVERSATION_TITLE_EMPTY` - New title is empty after trimming  
- `CHAT_CONVERSATION_TITLE_TOO_LONG` - Title exceeds 200 characters

### Complete(clock)
- `CHAT_CONVERSATION_NOT_ACTIVE` - Conversation status is not Active
- `CHAT_CONVERSATION_EMPTY_ON_COMPLETE` - Conversation has zero messages

## Domain Events (Stable Contracts)

All events are immutable sealed records with `Version = 1`. Events follow strict schema discipline and time discipline.

### ConversationStartedEvent
```csharp  
public sealed record ConversationStartedEvent(
    ConversationId ConversationId,    // Unique conversation identifier
    UserId OwnerId,                   // User who started the conversation  
    string Title,                     // Initial title (empty if default)
    bool IsDefaultTitle,              // True if title was auto-generated
    DateTimeOffset StartedAt          // When conversation began (IClock)
) : DomainEvent;
```

### UserMessageAppendedEvent
```csharp
public sealed record UserMessageAppendedEvent(
    ConversationId ConversationId,    // Conversation receiving the message
    MessageId MessageId,              // Unique message identifier
    int Sequence,                     // Message sequence (1-based)
    string ContentPreview,            // First 100 chars, no ellipsis
    DateTimeOffset CreatedAt          // Message creation timestamp (IClock)
) : DomainEvent;
```

### AssistantMessageAppendedEvent  
```csharp
public sealed record AssistantMessageAppendedEvent(
    ConversationId ConversationId,    // Conversation receiving the message
    MessageId MessageId,              // Unique message identifier  
    int Sequence,                     // Message sequence (1-based)
    string ContentPreview,            // First 100 chars, no ellipsis
    DateTimeOffset CreatedAt          // Message creation timestamp (IClock)
) : DomainEvent;
```

### ConversationTitleUpdatedEvent
```csharp
public sealed record ConversationTitleUpdatedEvent(
    ConversationId ConversationId,    // Conversation being updated
    string Title,                     // New title value
    bool IsDefaultTitle,              // False for user-set titles
    DateTimeOffset UpdatedAt          // When title was changed (IClock)  
) : DomainEvent;
```

### ConversationCompletedEvent
```csharp
public sealed record ConversationCompletedEvent(
    ConversationId ConversationId,    // Conversation being completed
    int MessageCount,                 // Total messages in conversation
    DateTimeOffset CompletedAt        // Completion timestamp (IClock)
) : DomainEvent;
```

### Event Contract Rules
- **Content Preview**: Plain truncation to 100 characters maximum, **no ellipsis** (`...`)
- **Time Discipline**: All timestamps from IClock, never direct time APIs
- **Version**: Default `Version = 1`; increment on breaking schema changes
- **Immutability**: Events are sealed records, never modified after creation
- **No Events on Failure**: Failed operations never emit domain events

## Usage Cookbook (Domain-only)

### Start Conversation

**With title:**
```csharp
var clock = new SystemClock();
var result = Conversation.Start(
    UserId.New(), 
    "My Chat Session", 
    clock);

if (result.IsSuccess)
{
    var conversation = result.Value;
    // ConversationStartedEvent emitted with IsDefaultTitle = false
}
```

**Without title (empty allowed):**
```csharp
var result = Conversation.Start(UserId.New(), null, clock);
// or: Conversation.Start(UserId.New(), "", clock);  
// ConversationStartedEvent emitted with IsDefaultTitle = true
```

### Append Messages & Capture Events

```csharp
var conversation = // ... existing conversation
var content = MessageContent.Create("Hello, assistant!").Value;

var result = conversation.AppendUserMessage(content, clock);
if (result.IsSuccess)
{
    var message = result.Value;
    // UserMessageAppendedEvent emitted
    // message.Sequence computed automatically (e.g., 1, 3, 5...)
}

// Assistant response
var assistantContent = MessageContent.Create("Hello! How can I help?").Value;
var assistantResult = conversation.AppendAssistantMessage(assistantContent, clock);
// AssistantMessageAppendedEvent emitted  
// Sequence computed automatically (e.g., 2, 4, 6...)
```

### Update Title (Non-empty Required)

```csharp
var result = conversation.UpdateTitle("Updated Chat Title", clock);
if (result.IsSuccess)
{
    // ConversationTitleUpdatedEvent emitted with IsDefaultTitle = false  
}

// This will fail:
var failResult = conversation.UpdateTitle("", clock);
// Returns: CHAT_CONVERSATION_TITLE_EMPTY error
```

### Complete Conversation

```csharp  
var result = conversation.Complete(clock);
if (result.IsSuccess)
{
    // ConversationCompletedEvent emitted
    // conversation.Status now equals ConversationStatus.Completed
}

// Will fail if conversation has no messages:
// Returns: CHAT_CONVERSATION_EMPTY_ON_COMPLETE error
```

### Important Notes
- **Sequence Auto-Computed**: Never pass sequence numbers; aggregate computes them
- **Result Pattern**: All operations return `Result<T>` for error handling
- **Event Collection**: Access domain events via `conversation.DomainEvents` after operations
- **IClock Required**: All time-sensitive operations require IClock injection

## Specifications (Query Recipes)

Pre-built specifications for common conversation queries. All are EF-translatable.

### Available Specifications (`src/Modules/Chat/Domain/Specifications/`)

- **ConversationsByOwnerSpec** - Filter by owner ID
- **ConversationsByStatusSpec** - Filter by status (Active, Completed)  
- **ConversationsCreatedBetweenSpec** - Filter by creation date range
- **ConversationsUpdatedSinceSpec** - Filter by last update date
- **ConversationTitleContainsSpec** - Text search in titles
- **ConversationsWithMinimumMessagesSpec** - Filter by message count threshold

### EF Translation Guidelines

**For case-insensitive queries:**
```csharp
// ✅ Correct: EF-translatable
spec.Query.Where(c => c.Title.ToLower().Contains(searchTerm.ToLowerInvariant()))

// ❌ Forbidden: Not EF-translatable  
spec.Query.Where(c => c.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
```

### Composition Example
```csharp
var activeByOwnerSpec = new ConversationsByOwnerSpec(userId)
    .And(new ConversationsByStatusSpec(ConversationStatus.Active));
    
var recentActiveConversations = new ConversationsUpdatedSinceSpec(DateTime.UtcNow.AddDays(-7))
    .And(activeByOwnerSpec);
```

## Testing Cookbook

### Test Infrastructure (`tests/Modules.Chat.Domain.Tests/TestInfrastructure/`)

**Builders:**
- `ConversationBuilder` - Fluent builder for conversation test data
- `MessageBuilder` - Fluent builder for message test data  

**Factories:**
- `StringFactory` - Deterministic string generation (`Len1()`, `Len100()`, `Len100k()`, etc.)
- `RoleFactory` - Message role patterns for turn-taking tests

**Time & Fixtures:**
- `FixedClock` - Deterministic time for reproducible tests
- `ConversationScript` - Multi-step conversation scenarios

### Property-Based Testing

Target boundary conditions with FsCheck generators:

**Content Boundaries:**
- 1, 100, 1000, 100000 characters (success cases)  
- 0, 100001+ characters (failure cases)

**Title Boundaries:**
- 1, 50, 200 characters (success cases)
- 0, 201+ characters (failure cases) 

**Message Count Boundaries:**  
- 1, 1000, 10000 messages (success cases)
- 10001+ messages (failure case)

### Guard Tests

**API Protection:**
- `PublicApiSnapshotTests` - Detects unintended API surface changes
- `PublicSurfaceWhitelistTests` - Ensures only whitelisted types are public
- `ConstructorGuardTests` - Enforces factory pattern usage

**Domain Purity:**
- `DomainPurityGuardTests` - Prevents infrastructure leakage
- Time discipline enforcement (no direct DateTime usage)  
- Business rules remain internal

### Testing Best Practices

```csharp
[Test]
public void AppendUserMessage_WithValidContent_ShouldSucceedAndEmitEvent()
{
    // Arrange
    var conversation = ConversationBuilder.New()
        .WithOwner(UserMother.ValidUser())
        .Build();
    var content = MessageContent.Create("Valid message").Value;
    var clock = new FixedClock(DateTime.UtcNow);
    
    // Act
    var result = conversation.AppendUserMessage(content, clock);
    
    // Assert  
    result.IsSuccess.Should().BeTrue();
    conversation.DomainEvents.Should().ContainSingle<UserMessageAppendedEvent>();
    conversation.MessageCount.Should().Be(1);
}
```

## Versioning & Compatibility

### Event Versioning
- **Default Version**: All events start with `Version = 1`
- **Breaking Changes**: Increment version when changing event schema
- **Additive Changes**: Adding optional properties is generally safe
- **Payload Evolution**: Use event upcasting for version migration

### Error Code Evolution
- **Add New Codes**: Safe to add new error codes anytime  
- **Never Repurpose**: Existing codes must retain their semantic meaning
- **Deprecation**: Mark obsolete codes but maintain compatibility

### Public API Surface Evolution
- **Additive Changes**: New public methods/properties allowed (minor version)
- **Breaking Changes**: Removing/changing public API requires major version  
- **Snapshot Maintenance**: Update `PublicApiSnapshotTests` for intentional changes
- **Whitelist Updates**: Review `PublicSurfaceWhitelistTests` for new public types

### Backward Compatibility Strategy
```csharp
// ✅ Safe evolution example:
public sealed record ConversationStartedEvent(
    ConversationId ConversationId,
    UserId OwnerId, 
    string Title,
    bool IsDefaultTitle,
    DateTimeOffset StartedAt,
    string? OptionalNewField = null  // Safe addition
) : DomainEvent;
```

## Performance Notes

The Chat domain implements allocation-optimized patterns to minimize memory pressure in hot paths. These optimizations are validated by guard tests to prevent regressions.

### Preview Generation Hygiene

**Message content previews** (100-character truncation for events) follow strict allocation rules:

- **≤100 characters**: Preview reuses the original string reference (0 extra allocations)
- **>100 characters**: Preview creates exactly one substring allocation  
- **No ellipsis**: Plain truncation without `...` suffix to avoid extra string operations

```csharp
// Internal implementation via TextSlices.Preview()
var preview = TextSlices.Preview(content, 100);
// Returns same reference when content.Length ≤ 100
// Returns content.Substring(0, 100) when content.Length > 100
```

### Specification Search Patterns

**Case-insensitive search** uses EF-friendly lowering with constants pre-normalized:

- **Constants**: Lowered once using `TextSlices.NormalizeForSearchConst()` outside expressions
- **Entity fields**: Use `.ToLower()` inside LINQ expressions (EF-translatable)
- **Empty terms**: Return pass-through expressions (`c => true`) for optimal query performance

```csharp
// Specification constructor (constant side)
_normalizedTerm = TextSlices.NormalizeForSearchConst(searchTerm);

// Expression method (entity side)  
return c => c.Title.ToLower().Contains(_normalizedTerm);
```

This pattern prevents double-lowering operations and ensures Entity Framework can translate the queries efficiently to SQL.

## Contribution Checklist

Before merging changes to the Chat domain:

### Code Quality
- [ ] **Public API Snapshot** - Unchanged or consciously updated via `PublicApiSnapshotTests`  
- [ ] **Error Codes** - Match exact codes from `04_VALIDATION & ERROR CATALOG.md`
- [ ] **Guard Tests Green** - Purity, time discipline, internal rules, constructor enforcement
- [ ] **Business Rules Internal** - All rule classes remain internal to domain

### Testing & Documentation  
- [ ] **Unit Tests** - Cover positive/negative cases and boundary conditions
- [ ] **Property-Based Tests** - For complex invariants and edge case coverage
- [ ] **Domain Events** - New/changed events documented in this README
- [ ] **Usage Examples** - Update cookbook if new public methods added

### Architecture Compliance
- [ ] **Time Discipline** - All timestamps from IClock, no direct DateTime usage
- [ ] **Result Pattern** - Public methods return Result<T> for error handling  
- [ ] **Event Discipline** - Events only on success, immutable sealed records
- [ ] **Strong Typing** - Use ConversationId, MessageId, UserId (no primitive obsession)

### Performance Considerations
- [ ] **Zero Allocations** - Message previews, specification composition
- [ ] **EF Translation** - Query specifications use EF-compatible patterns  
- [ ] **List Exposure** - Public collections as IReadOnlyList<T> only

---

*This README serves as the canonical source of truth for the Chat domain module. When in doubt about contracts, behavior, or evolution policy, refer to this document first.*