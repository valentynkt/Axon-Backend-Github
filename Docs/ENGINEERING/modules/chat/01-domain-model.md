# Chat Domain Model

**Aggregate, entities, value objects, and business rules.**

---

## Overview

Chat module uses **Conversation** aggregate with **Message** as owned entity (EF Core `OwnsMany`).

**Key Patterns:**
- Aggregate boundary: Conversation owns Messages (no separate DbSet)
- Composite keys: `(ConversationId, MessageId)`
- Concurrency: PostgreSQL `xmin` on aggregate root only
- Idempotency: `AiResponseId` prevents duplicate assistant messages

---

## Aggregate: Conversation

**Source**: `src/Modules/Chat/Domain/Aggregates/Conversation/Conversation.cs`

### Core Properties

```csharp
public sealed class Conversation : AggregateRoot<ConversationId>
{
    public AxonUserId OwnerId { get; }                // Owner (immutable)
    public ConversationStatus Status { get; }          // Active/Completed
    public string? Title { get; }                      // Optional (1-200 chars)
    public AiResponseId? LastAiResponseId { get; }     // For prompt caching
    private readonly List<Message> _messages;          // Owned collection
}
```

### Key Methods

**Command Methods:**
```csharp
// Factory
StartNewConversation(ownerId, title?, timeProvider)

// Message Operations
AppendUserMessageToConversation(content, timeProvider)
AppendAssistantResponseToConversation(content, aiResponseId, timeProvider)
AppendMessageExchange(userContent, assistantContent, aiResponseId, timeProvider)
ImportMessageHistory(messages, timeProvider)

// State Management
UpdateTitle(newTitle, timeProvider)
Complete(timeProvider)
```

**Query Methods:**
```csharp
GetMessageCount(), HasMessages()
GetMessageBySequence(sequence), GetLatestMessage()
GetAllMessages(), GetRecentMessages(count)
GetMessagesByRole(role), GetStatistics()
BelongsTo(userId), ValidateAccess(userId)
```

---

## Entity: Message (Owned)

**Source**: `src/Modules/Chat/Domain/Entities/Message.cs`

```csharp
public sealed class Message : AuditableDeletableEntity<MessageId>
{
    public ConversationId ConversationId { get; }  // Parent aggregate
    public MessageRole Role { get; }               // User/Assistant
    public MessageContent Content { get; }         // 1-32K chars
    public int Sequence { get; }                   // 1-based order
    public AiResponseId? AiResponseId { get; }     // Assistant only, null for User
}
```

**Factory Methods:**
```csharp
CreateUserMessage(conversationId, content, sequence)         // AiResponseId = null
CreateAssistantMessage(conversationId, content, sequence, aiResponseId)  // Required
```

**Invariants:**
- Assistant messages MUST have `AiResponseId`
- User messages MUST NOT have `AiResponseId`
- Sequence > 0

---

## Value Objects

| Value Object | Type | Rules |
|--------------|------|-------|
| **ConversationId** | Vogen<Guid> | Strong ID |
| **MessageId** | Vogen<Guid> | Strong ID |
| **AiResponseId** | Vogen<string> | Strong ID (from Claude API) |
| **ConversationTitle** | Vogen<string> | 1-200 chars, trimmed |
| **MessageContent** | Vogen<string> | 1-32K chars |
| **MessageRole** | Vogen<string> | "user" or "assistant" |
| **ConversationStatus** | enum | Active(1), Completed(2) |
| **ConversationStatistics** | record | TotalMessages, UserMessages, AssistantMessages, AvgLength, LastActivity, IsActive |

---

## Domain Events

| Event | When | Contains |
|-------|------|----------|
| **ConversationStartedEvent** | New conversation | ConversationId, OwnerId, Title?, Timestamp |
| **UserMessageAppendedEvent** | User message added | ConversationId, MessageId, Sequence, ContentPreview, Timestamp |
| **AssistantMessageAppendedEvent** | AI response added | ConversationId, MessageId, Sequence, ContentPreview, AiResponseId, Timestamp |
| **ConversationCompletedEvent** | Conversation closed | ConversationId, TotalMessages, Timestamp |
| **ConversationTitleUpdatedEvent** | Title changed | ConversationId, NewTitle, Timestamp |
| **ConversationHistoryImportedEvent** | Bulk import | ConversationId, MessageCount, Timestamp |

---

## Business Rules

**Key Rules** (`src/Modules/Chat/Domain/Rules/`):

| Rule | Code | Description |
|------|------|-------------|
| **MessageTurnTakingRule** | CHAT010 | Must alternate User → Assistant → User |
| **ConversationCanAcceptMoreMessagesRule** | CHAT006 | Max 100 messages per conversation |
| **MessageContentWithinLimitsRule** | CHAT008 | 1-32K characters per message |
| **ConversationMustBeActiveRule** | CHAT003 | Status = Active for mutations |
| **ConversationMustBelongToOwnerRule** | CHAT004 | Owner verification (403 Forbidden) |
| **AiResponseIdMustBeUniqueRule** | CHAT013 | No duplicate AI response IDs |

**Total: 19 business rule classes** in `Domain/Rules/`

---

## Key Patterns

### 1. Turn-Taking
```csharp
// ✅ Valid: User → Assistant → User → Assistant
// ❌ Invalid: User → User (consecutive same role)
```

### 2. Idempotency
```csharp
// Retry-safe: same AiResponseId returns existing message
conversation.AppendAssistantResponse(content, aiResponseId, tp);
```

### 3. Atomic Operations
```csharp
// Single transaction for user + assistant pair
var (userMsg, assistantMsg) = conversation.AppendMessageExchange(
    userContent, assistantContent, aiResponseId, timeProvider);
```

### 4. Owned Entity Access
```csharp
// ✅ Correct: Through aggregate
var messages = conversation.GetAllMessages();

// ❌ Wrong: No direct DbSet
dbContext.Messages.Where(m => m.ConversationId == id);
```

---

## Related Docs

- **[03-messaging-flows.md](03-messaging-flows.md)** - Message processing
- **[05-api-contracts.md](05-api-contracts.md)** - REST API
- **[06-database-schema.md](06-database-schema.md)** - EF Core
- **[00-INDEX.md](00-INDEX.md)** - Navigation