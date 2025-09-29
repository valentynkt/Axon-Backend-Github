# Chat Module

**AI-powered conversations with Claude API + MCP integration.**

---

## Overview

Manages conversations and messages with AI orchestration through Anthropic Claude API and Model Context Protocol (MCP) servers.

**Key Features:**
- Conversation aggregates with owned messages (DDD + EF Core `OwnsMany`)
- Turn-taking enforcement (user → assistant alternation)
- Idempotent AI responses via `AiResponseId`
- MCP server integration for extended capabilities
- Strict business rules (100 message limit, 1-32K char content)

---

## Quick Start

### Create & Send Message

```http
POST /api/v1/chat/turns
Authorization: Bearer {token}

{ "conversationId": null, "message": "Hello!" }
→ { conversationId, userMessageId, assistantMessageId, assistantMessage }
```

### List Conversations

```http
GET /api/v1/conversations?pageSize=20&sortBy=UpdatedAt
```

### Get Messages

```http
GET /api/v1/conversations/{id}/messages?pageSize=50
```

---

## Architecture

### Domain Layer
**Aggregate:** `Conversation` (owns Messages)
**Entity:** `Message` (owned, composite key: `(ConversationId, MessageId)`)
**Rules:** 19 business rules (turn-taking, limits, validation)
**Events:** 6 domain events (started, message appended, completed, etc.)

### Application Layer
**Commands:** `StartConversation`, `AppendUserMessage`
**Queries:** `GetConversations`, `GetConversationMessages`
**Services:** `AiProcessingService`, `McpServerResolutionService`, `MessageProcessingOrchestrator`

### Infrastructure
**Persistence:** EF Core with PostgreSQL (`chat` schema)
**Concurrency:** Optimistic locking via `xmin`
**AI Integration:** Anthropic Claude API client

---

## Key Patterns

### 1. Owned Entity (EF Core)
```csharp
// Messages owned by Conversation - no separate DbSet
builder.OwnsMany<Message>("_messages", messages => {
    messages.ToTable("Messages");
    messages.HasKey(m => new { m.ConversationId, m.Id });  // Composite key
});
```

### 2. Turn-Taking
```csharp
// Domain enforces: User → Assistant → User → Assistant
// ❌ Invalid: User → User (consecutive same role)
```

### 3. Idempotency
```csharp
// Retry-safe: same AiResponseId returns existing message
conversation.AppendAssistantResponse(content, aiResponseId, tp);
```

---

## Source Code

```
src/Modules/Chat/
├── Domain/
│   ├── Aggregates/Conversation/  # Aggregate root
│   ├── Entities/Message.cs       # Owned entity
│   ├── ValueObjects/              # ConversationId, MessageId, etc.
│   ├── Rules/                     # 19 business rules
│   └── Events/                    # 6 domain events
├── Application/
│   ├── Commands/                  # StartConversation, AppendUserMessage
│   ├── Queries/                   # GetConversations, GetMessages
│   ├── Services/AI/               # AiProcessingService, MCP resolution
│   └── Services/Orchestration/    # MessageProcessingOrchestrator
└── Infrastructure/
    ├── Persistence/               # EF Core config, repositories
    └── ExternalServices/          # Claude API client
```

---

## Documentation

| Doc | Purpose |
|-----|---------|
| **[01-domain-model.md](01-domain-model.md)** | Aggregate, entities, value objects, rules |
| **[03-messaging-flows.md](03-messaging-flows.md)** | Message processing & AI integration |
| **[05-api-contracts.md](05-api-contracts.md)** | REST endpoints & schemas |
| **[06-database-schema.md](06-database-schema.md)** | EF Core & PostgreSQL schema |

---

## Testing

**Location:** `tests/Modules/Chat/`
**Coverage:** 132+ test files

**Key Suites:**
- `Domain/` - Aggregate invariants, business rules
- `Application/Commands/` - Command handlers
- `Application/Queries/` - Query handlers
- `Infrastructure/Persistence/` - EF Core patterns, transactions

---

## Dependencies

**External:**
- Anthropic Claude API (AI processing)
- MCP Servers (extended capabilities)

**Internal:**
- Identity Module (`AxonUserId` for ownership)