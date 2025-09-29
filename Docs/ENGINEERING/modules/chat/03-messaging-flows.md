# Messaging Flows

**Chat turn processing and AI integration.**

---

## Overview

Universal endpoint **POST /api/v1/chat/turns** handles both starting new conversations and appending to existing ones.

**Flow**: User message → Persistence → AI processing (Claude + MCP) → Assistant message → Persistence

**Key Characteristics:**
- Single endpoint for all chat interactions
- Idempotent assistant messages (`AiResponseId`)
- Turn-taking enforced by domain
- MCP servers for extended AI capabilities
- Transactional (all-or-nothing)

---

## Universal Chat Turn Flow

```
POST /api/v1/chat/turns
{ conversationId?, message, mcpServers? }
        ↓
ChatTurnEndpoint → ChatCommandDispatcher
        ↓
    conversationId?
   ↙            ↘
  null          present
   ↓              ↓
StartConversation  AppendUserMessage
   ↓              ↓
Create aggregate   Load & validate ownership
Append user msg    Append user message
Persist           Persist
   ↓              ↓
   └──────┬───────┘
          ↓
MessageProcessingOrchestrator
   1. Resolve MCP servers
   2. Call AI service (Claude API)
   3. Append assistant message
   4. Persist
   5. Return response
```

---

## Key Components

| Component | Responsibility |
|-----------|----------------|
| **ChatTurnEndpoint** | API entry, validation, mapping |
| **ChatCommandDispatcher** | Route to Start or Append command |
| **StartConversationHandler** | Create conversation + process |
| **AppendUserMessageHandler** | Append to existing + process |
| **MessageProcessingOrchestrator** | AI processing coordination |
| **AiProcessingService** | Claude API integration |
| **IAiClient** | HTTP client (Anthropic) |

---

## Message Processing Pipeline

After user message is persisted:

### Step 1: Resolve MCP Servers
```csharp
var mcpConfigs = await _mcpResolutionService.ResolveServersAsync(conversationId, ct);
```
MCP servers provide: file system access, DB queries, external APIs, custom tools

### Step 2: Process AI Request
```csharp
var aiResult = await _aiProcessingService.ProcessMessageAsync(
    userMessage,
    conversationId,
    previousResponseId: conversation.LastAiResponseId,  // Prompt caching
    mcpConfigs,
    ct);
```

AI client includes:
- Full conversation history
- MCP server context
- `previous_response_id` for caching

### Step 3: Append Assistant Message
```csharp
var assistantMsg = conversation.AppendAssistantResponseToConversation(
    aiResult.AssistantContent,
    aiResult.ResponseId,  // AiResponseId from Claude
    timeProvider);
```

**Idempotency**: If `AiResponseId` exists → returns existing message

### Step 4: Persist & Return
```csharp
await _repository.UpdateAsync(conversation, ct);
await _repository.UnitOfWork.SaveChangesAsync(ct);

return new ProcessMessageResponse(conversationId, userMessageId, assistantMessageId, assistantMessage);
```

---

## Business Rules Enforced

**Start New Conversation:**
- ✅ User authenticated
- ✅ Message content valid (1-32K chars)

**Append to Existing:**
- ✅ Conversation exists
- ✅ User is owner (403 if not)
- ✅ Conversation active
- ✅ Under message limit (100 max)
- ✅ Turn-taking (no consecutive same-role)

**AI Processing:**
- ✅ AiResponseId unique (idempotency)

---

## Error Handling

### Exception Types

| Exception | HTTP | Retry? |
|-----------|------|--------|
| **DbUpdateConcurrencyException** | 409 Conflict | ✅ Yes |
| **DbUpdateException** | 500 Persistence | ❌ No |
| **TimeoutException** | 504 Timeout | ✅ Yes |
| **HttpRequestException** | 502 Bad Gateway | ✅ Yes (transient) |
| **BusinessRuleException** | 422 Unprocessable | ❌ No |
| **OperationCanceledException** | Re-throw | ❌ No |
| **Exception** | 500 Internal | ❌ No |

### Transient HTTP Errors
- Connection timeout, reset, closed
- Network unreachable
- Temporary DNS failure

---

## Performance

**Database:**
- Single query to load conversation (EF `Include`)
- Single `SaveChangesAsync()` per turn
- Composite key lookups: `(ConversationId, MessageId)`

**AI Processing:**
- Prompt caching via `previous_response_id`
- MCP resolution: ~10-50ms overhead

**Concurrency:**
- PostgreSQL `xmin` (optimistic locking)
- Client retries on 409 Conflict

---

## Code References

**Application Layer:**
- Dispatcher: `Application/Services/Dispatching/ChatCommandDispatcher.cs`
- Handlers: `Application/Commands/{StartConversation,AppendUserMessage}/`
- Orchestrator: `Application/Services/Orchestration/MessageProcessingOrchestrator.cs`
- AI Service: `Application/Services/AI/AiProcessingService.cs`

**API Layer:**
- Endpoint: `Api/Endpoints/V1/Chat/Commands/ChatTurn/ChatTurnEndpoint.cs`
- DTOs: `Api/Contracts/V1/Chat/ChatTurn{Request,Response}Dto.cs`

---

## Related Docs

- **[01-domain-model.md](01-domain-model.md)** - Domain layer
- **[05-api-contracts.md](05-api-contracts.md)** - REST API
- **[06-database-schema.md](06-database-schema.md)** - Database