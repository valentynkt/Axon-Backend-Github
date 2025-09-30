# Chat Workflow

**Module-specialized enhancement for Chat module stories (Tier 3)**

---

## Overview

The **chat-workflow** extends `story-implementation` with Chat module-specific guidance for:

- **Conversation Management**: Conversation aggregate, lifecycle, ownership, state
- **Message Processing**: Turn-taking, sequencing, validation, limits
- **AI Integration**: Claude API, MCP servers, prompt caching, streaming
- **Real-Time Communication**: SSE, async processing, real-time updates

**Key Features**:
- 19 business rules enforcement (turn-taking, limits, idempotency, validation)
- Owned entity pattern (Message with composite keys)
- Universal endpoint architecture (POST /api/v1/chat/turns)
- AI integration patterns (idempotency, prompt caching, MCP resolution)
- Domain event patterns (6 events)

---

## Quick Start

### Invocation

This workflow is automatically invoked by `@axon-story-orchestrator` when:
- Story type = **Feature**
- Module context = **Chat**

```bash
# Manual invocation (if needed)
@axon-story-orchestrator implement-story {story-id}
# → Detects module = Chat → Routes to chat-workflow
```

### Usage Example

**Story**: "Add idempotent message retry handling"

**Routing**:
```yaml
story_type: Feature
module_context: Chat
subdomain: Message Processing
→ Invokes: chat-workflow (extends story-implementation)
```

**Enhancement Points**:
1. **Phase 0.2**: Load 5 Chat docs + classify subdomain (Message Processing)
2. **Phase 1.2**: Pre-flight validation (50+ methods, 19 rules, MediatR/MCP libraries)
3. **Phase 2.2**: Implementation guidance (idempotency patterns, AiResponseId enforcement)
4. **Phase 3.2**: Comprehensive testing (Domain, Application, Infrastructure, E2E)

---

## Chat Module Context

### Aggregate: Conversation
**Location**: `src/Modules/Chat/Domain/Aggregates/Conversation/Conversation.cs`

**Properties**:- `ConversationId` (aggregate ID)
- `AxonUserId OwnerId` (immutable)
- `ConversationStatus Status` (Active/Completed)
- `string? Title` (optional, 1-200 chars)
- `AiResponseId? LastAiResponseId` (prompt caching)
- `List<Message> _messages` (owned collection)

**50+ Methods**:
- **Factory**: `StartNewConversation(ownerId, title?, timeProvider)`
- **Commands**: `AppendUserMessage()`, `AppendAssistantResponse()`, `AppendMessageExchange()`, `Complete()`, `UpdateTitle()`
- **Queries**: `GetAllMessages()`, `GetRecentMessages()`, `GetStatistics()`, `ValidateAccess()`

### Entity: Message (Owned)
**Location**: `src/Modules/Chat/Domain/Entities/Message.cs`

**Composite Key**: `(ConversationId, MessageId)`

**Properties**:
- `MessageRole` (User/Assistant)
- `MessageContent` (1-32K chars)
- `int Sequence` (1-based order)
- `AiResponseId?` (Assistant only, idempotency)

### 19 Business Rules

**Critical Invariants** (6):
1. **CHAT010**: Turn-taking (User → Assistant alternation)
2. **CHAT006**: Message limit (100 max)
3. **CHAT008**: Content length (1-32K chars)
4. **CHAT003**: Active conversation only
5. **CHAT004**: Ownership (403 Forbidden)
6. **CHAT013**: AiResponseId uniqueness (idempotency)

**Additional Rules** (13 more validation/constraint rules)

### 6 Domain Events
- `ConversationStartedEvent`
- `UserMessageAppendedEvent`
- `AssistantMessageAppendedEvent`
- `ConversationCompletedEvent`
- `ConversationTitleUpdatedEvent`
- `ConversationHistoryImportedEvent`

---

## Subdomain Classification

Stories are classified into 4 Chat subdomains:### 1. Conversation Management
**Scope**: Conversation lifecycle, CRUD, queries, state management
**Key Services**: ConversationRepository, ChatWriteDbContext
**Key Patterns**: Aggregate root, owned entities, domain events, specifications
**Examples**: "Archive conversations", "Add conversation search", "Enable tagging"

### 2. Message Processing
**Scope**: Turn-taking, sequencing, validation, content processing, history import
**Key Services**: MessageProcessingOrchestrator, ChatCommandDispatcher
**Key Patterns**: Business rules, sequence integrity, role validation, atomic operations
**Examples**: "Enforce turn-taking", "Validate message format", "Bulk import"

### 3. AI Integration
**Scope**: Claude API, MCP servers, prompt caching, streaming, tool execution
**Key Services**: AiProcessingService, McpServerResolutionService, IAiClient
**Key Patterns**: AI service, MCP resolution, idempotency, async processing
**Examples**: "Add MCP server", "Streaming responses", "Optimize caching"

### 4. Real-Time Communication
**Scope**: SSE, streaming responses, async processing, real-time updates
**Key Patterns**: Async/await, streaming, SSE endpoints, cancellation tokens
**Examples**: "Stream AI responses", "Add typing indicators", "Connection management"

---

## Key Patterns

### Owned Entity Pattern
```csharp
// ✅ CORRECT: Access via aggregate
var messages = conversation.GetAllMessages();
var latestMessage = conversation.GetLatestMessage();

// ❌ WRONG: NO direct DbSet access
var messages = dbContext.Messages.Where(m => m.ConversationId == id);  // FORBIDDEN
```

### Idempotency Pattern
```csharp
// Same AiResponseId returns existing message (idempotent)
var result = conversation.AppendAssistantResponse(content, aiResponseId, timeProvider);
if (existingMessageWithSameAiResponseId)
    return existingMessage;  // Idempotent
```

### Universal Endpoint
```csharp
// POST /api/v1/chat/turns
// conversationId = null → Start new conversation
// conversationId = guid → Append to existing
```

---

## Success Criteria

### Inherited Gates (5):
1. ✅ Pattern Compliance ≥ 95%
2. ✅ Test Coverage ≥ 90%3. ✅ AC Coverage = 100%
4. ✅ Build Success = 100%
5. ✅ Doc Sync = Zero Drift

### Chat-Specific Gates (6):
6. ✅ Chat Domain Invariants Preserved = 100% (all 6 invariants)
7. ✅ Owned Entity Patterns Correct = 100% (composite keys, no DbSet)
8. ✅ EF Core Configuration Correct = 100% (OwnsMany, xmin token)
9. ✅ AI Integration Patterns Correct = 100% (idempotency, caching, MCP)
10. ✅ Universal Endpoint Correctness = 100% (both new + append paths)
11. ✅ Chat Test Scenarios Complete = 8/8 (all E2E scenarios)

---

## Duration

- **AI Time**: 50-80 minutes (additional on top of story-implementation base)
- **Human Checkpoints**: 11-18 minutes (inherited from story-implementation)
- **Total**: 61-98 minutes

---

## File Structure

```
bmad/axon/workflows/chat-workflow/
├── workflow.yaml          # Configuration (172 lines)
├── instructions.md        # Execution guide (723 lines)
├── checklist.md          # Validation checklist (498 lines)
└── README.md             # This file (614 lines)

Total: 2,007 lines
```

---

## Documentation Loaded

**Module Docs** (5 files):
- `modules/chat/00-INDEX.md` - Overview
- `modules/chat/01-domain-model.md` - Aggregate, entities, rules
- `modules/chat/03-messaging-flows.md` - AI integration, pipeline
- `modules/chat/05-api-contracts.md` - Universal endpoint
- `modules/chat/06-database-schema.md` - EF Core, owned entity

**Library Docs** (3 files):
- `Libraries/MediatR/IMPLEMENTATION_GUIDE.md`
- `Libraries/ModelContextProtocol/IMPLEMENTATION_GUIDE.md`
- `Libraries/ModelContextProtocolAspNetCore/IMPLEMENTATION_GUIDE.md`

---

## Troubleshooting

### Issue: "Turn-taking violation (CHAT010)"
**Cause**: Consecutive messages with same role (User → User or Assistant → Assistant)
**Solution**: Ensure alternation User → Assistant → User
**Code**: Check `MessageTurnTakingRule` enforcement

### Issue: "Message limit exceeded (CHAT006)"
**Cause**: Conversation has 100 messages**Solution**: Complete current conversation, start new one
**Code**: Check `ConversationCanAcceptMoreMessagesRule`

### Issue: "Direct DbSet<Message> access attempted"
**Cause**: Trying to query messages directly via DbContext
**Solution**: Access messages via Conversation aggregate only
**Pattern**:
```csharp
// ✅ Correct
var conversation = await _repository.GetByIdAsync(conversationId);
var messages = conversation.GetAllMessages();

// ❌ Wrong
var messages = _dbContext.Messages.Where(m => m.ConversationId == id);  // NO DbSet
```

### Issue: "AiResponseId duplicate (CHAT013)"
**Cause**: Trying to append assistant message with existing AiResponseId
**Solution**: This is idempotency working correctly - returns existing message
**Code**: Check `AiResponseIdMustBeUniqueRule` (should return existing, not fail)

### Issue: "Concurrency conflict (409)"
**Cause**: Two concurrent updates to same conversation
**Solution**: Retry on client side (optimistic locking via xmin)
**Code**: Handle `DbUpdateConcurrencyException`

### Issue: "Content length validation (CHAT008)"
**Cause**: Message content < 1 char or > 32K chars
**Solution**: Validate content length before appending
**Code**: Check `MessageContentWithinLimitsRule`

### Issue: "Ownership verification failed (403)"
**Cause**: User trying to access conversation they don't own
**Solution**: Verify userId matches conversation.OwnerId
**Code**: Check `ConversationMustBelongToOwnerRule`

### Issue: "Active conversation required (CHAT003)"
**Cause**: Trying to append to completed conversation
**Solution**: Only append to Active conversations
**Code**: Check `ConversationMustBeActiveRule`

---

## Metrics

**Code Generated** (typical story):
- Domain: 200-400 lines (aggregate methods, rules, events)
- Application: 300-500 lines (commands, queries, handlers, services)
- Infrastructure: 150-250 lines (EF Core, repositories, AI clients)
- API: 100-200 lines (endpoints, DTOs, validators)
- **Total**: 750-1,350 lines

**Tests Generated** (typical story):
- Domain tests: 45+ tests
- Application tests: 30+ tests
- Infrastructure tests: 20+ tests
- E2E tests: 8 tests
- **Total**: 103+ tests

**Coverage**:
- Overall: 92%+
- Domain: 95%+
- Application: 91%+
- Infrastructure: 88%+
- Critical paths: 100%

---

## Related Workflows

- **story-implementation** (base workflow, Tier 2)
- **identity-workflow** (sibling, Tier 3)
- **api-workflow** (sibling, Tier 3)
- **story-orchestrator** (invokes this workflow, Tier 1)

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-09-30 | Initial release: Chat module workflow complete |

---

**End of README**