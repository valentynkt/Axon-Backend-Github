# Chat Workflow - Validation Checklist

**Module**: Chat
**Type**: Enhancement workflow validation
**Purpose**: Comprehensive validation of Chat module story implementation

---

## Phase 0: Story Understanding & Doc Loading

### Step 0.1: Story Context Loaded
- [ ] Story file read and parsed
- [ ] Acceptance criteria extracted
- [ ] BMM tech-spec reference loaded (if exists)
- [ ] Story understanding approved (Checkpoint 1)

### Step 0.2: Chat Module Docs Loaded
- [ ] `modules/chat/00-INDEX.md` loaded (module overview)
- [ ] `modules/chat/01-domain-model.md` loaded (aggregate, entities, rules)
- [ ] `modules/chat/03-messaging-flows.md` loaded (AI integration, pipeline)
- [ ] `modules/chat/05-api-contracts.md` loaded (universal endpoint)
- [ ] `modules/chat/06-database-schema.md` loaded (EF Core, owned entity)
- [ ] Total: 5 module docs loaded (~800 lines)

### Step 0.3: Chat Subdomain Classified
- [ ] Story analyzed for subdomain keywords
- [ ] Subdomain selected (Conversation Management / Message Processing / AI Integration / Real-Time Communication)
- [ ] Rationale documented
- [ ] Key services identified
- [ ] Key patterns identified
- [ ] Output: `chat-subdomain.yaml` created

### Step 0.4: Chat Library Docs Loaded (Conditional)
- [ ] MediatR library doc loaded (CQRS handlers)
- [ ] MCP SDK doc loaded (if AI subdomain)
- [ ] MCP AspNetCore doc loaded (if AI subdomain)
- [ ] Output: `chat-libraries.yaml` created

---

## Phase 1: Pre-Flight Validation

### Step 1.1: Archaeologist - Conversation Methods Cataloged
- [ ] Conversation aggregate methods searched
- [ ] Factory methods identified (StartNewConversation)
- [ ] Command methods cataloged (Append*, Update*, Complete*, Import*)- [ ] Query methods cataloged (Get*, Has*, Validate*)
- [ ] Total: 50+ methods documented
- [ ] Output: `chat-reusable-methods.yaml` created

### Step 1.2: Archaeologist - Business Rules Mapped
- [ ] All 19 business rule classes cataloged
- [ ] Critical invariants identified (6 rules: CHAT010, CHAT006, CHAT008, CHAT003, CHAT004, CHAT013)
- [ ] Additional rules documented (13 rules)
- [ ] Rule codes, descriptions, and enforcement points captured
- [ ] Output: `chat-business-rules.yaml` created

### Step 1.3: Archaeologist - Similar Features Found
- [ ] Application/Commands searched for similar handlers
- [ ] Application/Queries searched for similar patterns
- [ ] Services searched for reusable orchestration
- [ ] Similar CQRS handlers identified

### Step 1.4: Library Sage - MediatR Validated
- [ ] MediatR command patterns verified (IRequest<Result<T, Error>>)
- [ ] MediatR query patterns verified
- [ ] Handler registration checked
- [ ] CancellationToken propagation verified
- [ ] Async/await usage confirmed

### Step 1.5: Library Sage - MCP Integration Validated (if AI subdomain)
- [ ] MCP server resolution patterns checked
- [ ] MCP SDK usage verified
- [ ] MCP AspNetCore integration confirmed
- [ ] Tool execution patterns validated

### Step 1.6: Doc Oracle - Rule Compliance Validated
- [ ] Story analyzed against 19 business rules
- [ ] CHAT010 (turn-taking) compliance checked
- [ ] CHAT006 (message limit) compliance checked
- [ ] CHAT008 (content length) compliance checked
- [ ] CHAT003 (active only) compliance checked
- [ ] CHAT004 (ownership) compliance checked
- [ ] CHAT013 (idempotency) compliance checked
- [ ] All 19 rules assessed (applicable/pass/fail/warning)
- [ ] Output: `chat-rule-compliance.yaml` created

### Step 1.7: Doc Oracle - Domain Patterns Validated
- [ ] Owned entity access pattern validated (Message via Conversation only)
- [ ] Composite keys pattern validated (ConversationId, MessageId)- [ ] Domain events pattern validated (6 event types)
- [ ] Result<T> error handling validated
- [ ] No direct DbSet<Message> access confirmed

### Step 1.8: Pre-Flight Report Consolidated
- [ ] All parallel validations completed
- [ ] Reusable methods summary generated
- [ ] Business rules map generated
- [ ] Library recommendations generated
- [ ] Rule compliance summary generated
- [ ] Risks identified
- [ ] Output: `chat-preflight-report.yaml` created
- [ ] Pre-flight approved (Checkpoint 2)

---

## Phase 2: Implementation

### Step 2.1: Implementation Guidance Applied

**Domain Layer**:
- [ ] Conversation aggregate with private `List<Message> _messages`
- [ ] Message owned entity with ConversationId parent reference
- [ ] Value objects created (ConversationId, MessageId, AiResponseId, MessageContent, MessageRole, etc.)
- [ ] Business rules used (19 existing rules, new rules added if needed)
- [ ] Domain events raised (6 event types)
- [ ] Result<T> error handling (no exceptions in domain layer)

**Application Layer**:
- [ ] Commands created (StartConversationCommand, AppendUserMessageCommand, etc.)
- [ ] Command handlers return Result<ResponseDto, Error>
- [ ] Queries created (GetConversationsQuery, GetConversationMessagesQuery, etc.)
- [ ] Query handlers return Result<ResponseDto, Error>
- [ ] Services created/updated (AiProcessingService, MessageProcessingOrchestrator, etc.)
- [ ] CQRS pattern with MediatR

**Infrastructure Layer**:
- [ ] EF Core configuration with OwnsMany<Message>
- [ ] Composite keys configured (ConversationId, MessageId)
- [ ] Concurrency token xmin on Conversation only
- [ ] Repository with GetByIdAsync, UpdateAsync
- [ ] AI client for Claude API (if applicable)
- [ ] MCP client integration (if applicable)

**API Layer**:
- [ ] FastEndpoints created (ChatTurnEndpoint)- [ ] Universal endpoint handles both new + append (conversationId null/present)
- [ ] Request validation with FluentValidation
- [ ] Response mapping to DTOs
- [ ] Error handling maps Result<T> to HTTP status codes

### Step 2.2: Subdomain-Specific Patterns Applied

**Conversation Management**:
- [ ] Aggregate root pattern (Conversation owns Messages)
- [ ] Owned entity access via aggregate only
- [ ] Domain events raised on state changes
- [ ] Query methods for message access

**Message Processing**:
- [ ] Turn-taking rule enforced (CHAT010)
- [ ] Message limit enforced (CHAT006)
- [ ] Content validation enforced (CHAT008)
- [ ] Atomic operations (AppendMessageExchange)

**AI Integration**:
- [ ] AI service pattern (AiProcessingService)
- [ ] MCP server resolution (McpServerResolutionService)
- [ ] Message processing orchestration (MessageProcessingOrchestrator)
- [ ] Idempotency via AiResponseId (CHAT013)
- [ ] Prompt caching via LastAiResponseId

**Real-Time Communication**:
- [ ] Async/await throughout
- [ ] CancellationToken propagation
- [ ] Streaming support (if applicable)

### Step 2.3: Implementation Preview Generated
- [ ] Diff preview generated
- [ ] All layers included (Domain, Application, Infrastructure, API)
- [ ] Preview approved (Checkpoint 3)

### Step 2.4: Code Applied
- [ ] Domain code generated
- [ ] Application code generated
- [ ] Infrastructure code generated
- [ ] API code generated
- [ ] Inline XML documentation added

---

## Phase 3: Validation (Tests + Doc Sync + Learning)

### Step 3.1: Domain Tests Generated

**Conversation Aggregate Tests**:- [ ] StartNewConversation tests
- [ ] AppendUserMessage tests
- [ ] AppendAssistantResponse tests (including idempotency)
- [ ] AppendAssistantResponse turn-taking tests
- [ ] AppendMessageExchange atomic operation tests
- [ ] Complete tests
- [ ] GetStatistics tests
- [ ] Total: 15+ aggregate tests

**Business Rule Tests** (19 rules):
- [ ] MessageTurnTakingRule tests (CHAT010)
- [ ] ConversationCanAcceptMoreMessagesRule tests (CHAT006)
- [ ] MessageContentWithinLimitsRule tests (CHAT008)
- [ ] ConversationMustBeActiveRule tests (CHAT003)
- [ ] ConversationMustBelongToOwnerRule tests (CHAT004)
- [ ] AiResponseIdMustBeUniqueRule tests (CHAT013)
- [ ] Additional 13 business rule tests
- [ ] Total: 19+ rule tests

**Domain Event Tests**:
- [ ] ConversationStartedEvent tests
- [ ] UserMessageAppendedEvent tests
- [ ] AssistantMessageAppendedEvent tests
- [ ] ConversationCompletedEvent tests
- [ ] ConversationTitleUpdatedEvent tests
- [ ] ConversationHistoryImportedEvent tests
- [ ] Total: 6+ event tests

**Domain Tests Summary**:
- [ ] Total domain tests: 45+
- [ ] All tests passing
- [ ] Coverage: 95%+

### Step 3.2: Application Tests Generated

**Command Handler Tests**:
- [ ] StartConversationHandler tests
- [ ] AppendUserMessageHandler tests
- [ ] Ownership enforcement tests (403 Forbidden)
- [ ] Completed conversation tests (business rule error)
- [ ] Total: 10+ command handler tests

**Query Handler Tests**:
- [ ] GetConversationsHandler tests (pagination, filtering)
- [ ] GetConversationMessagesHandler tests
- [ ] Ownership enforcement tests
- [ ] Total: 8+ query handler tests

**Service Tests**:- [ ] AiProcessingService tests (Claude API calls, MCP context, prompt caching)
- [ ] MessageProcessingOrchestrator tests (full pipeline, idempotency)
- [ ] Total: 12+ service tests

**Application Tests Summary**:
- [ ] Total application tests: 30+
- [ ] All tests passing
- [ ] Coverage: 91%+

### Step 3.3: Infrastructure Tests Generated

**EF Core Persistence Tests**:
- [ ] ConversationConfiguration OwnsMany tests
- [ ] Composite key enforcement tests
- [ ] Concurrency token tests (xmin on Conversation only)
- [ ] ConversationRepository GetByIdAsync tests (includes messages)
- [ ] ConversationRepository UpdateAsync tests (atomic transaction)
- [ ] Concurrent updates tests (DbUpdateConcurrencyException)
- [ ] Total: 12+ persistence tests

**AI Client Tests** (if applicable):
- [ ] AI client request formatting tests
- [ ] Prompt caching tests (previous_response_id)
- [ ] Transient error retry tests
- [ ] MCP context inclusion tests
- [ ] Total: 8+ AI client tests

**Infrastructure Tests Summary**:
- [ ] Total infrastructure tests: 20+
- [ ] All tests passing
- [ ] Coverage: 88%+

### Step 3.4: E2E Tests Generated

**Chat Turn E2E Scenarios** (8 critical):
- [ ] Start conversation (conversationId = null)
- [ ] Continue conversation (conversationId = existing)
- [ ] Turn-taking enforcement (422 Unprocessable Entity)
- [ ] Message limit enforcement (422 Unprocessable Entity)
- [ ] Ownership enforcement (403 Forbidden)
- [ ] Concurrency conflict (409 Conflict)
- [ ] AI integration (assistant message returned)
- [ ] Idempotency (same AiResponseId returns existing)

**E2E Tests Summary**:
- [ ] Total E2E tests: 8+
- [ ] All tests passing
- [ ] All critical scenarios covered

### Step 3.5: Test Execution & Coverage

**Build**:- [ ] `dotnet build` executed
- [ ] Build success: 100%
- [ ] Warnings-as-errors: 0 warnings

**Test Execution**:
- [ ] `dotnet test` executed
- [ ] Domain tests: All passing
- [ ] Application tests: All passing
- [ ] Infrastructure tests: All passing
- [ ] E2E tests: All passing
- [ ] Total tests: 103+
- [ ] Test success rate: 100%

**Coverage**:
- [ ] Overall coverage: 92%+
- [ ] Domain coverage: 95%+
- [ ] Application coverage: 91%+
- [ ] Infrastructure coverage: 88%+
- [ ] Critical paths coverage: 100%

**Acceptance Criteria Coverage**:
- [ ] All ACs mapped to tests
- [ ] AC coverage: 100%
- [ ] All ACs validated

### Step 3.6: Documentation Sync
- [ ] Module docs updated (if needed)
- [ ] API docs updated (if new endpoints)
- [ ] README updated (if new features)
- [ ] No documentation drift detected
- [ ] Doc sync: Zero drift

### Step 3.7: Decision Log Captured
- [ ] Decision log created (YAML format)
- [ ] Story metadata captured
- [ ] Decisions array populated (timestamp, phase, agent, category, rationale)
- [ ] Learning section completed (what worked, challenges, improvements)
- [ ] Metrics captured (total decisions, by category, by agent)
- [ ] Summary generated (key decisions, approach, reuse score, pattern compliance)
- [ ] Output: `decisions/{story-id}-decisions.yaml` created

---

## Chat-Specific Success Criteria

### Gate 1: Pattern Compliance ≥ 95% (Inherited)
- [ ] Result<T> pattern: 100%
- [ ] StrongId<T> pattern: 100%
- [ ] CQRS pattern: 100%- [ ] Owned entity pattern: 100%
- [ ] Domain events pattern: 100%
- [ ] Overall pattern compliance: ≥95%

### Gate 2: Test Coverage ≥ 90% (Inherited)
- [ ] Overall coverage: 92%+
- [ ] Domain coverage: 95%+
- [ ] Application coverage: 91%+
- [ ] Infrastructure coverage: 88%+
- [ ] Critical paths: 100%

### Gate 3: AC Coverage = 100% (Inherited)
- [ ] All acceptance criteria mapped to tests
- [ ] All ACs validated
- [ ] AC coverage: 100%

### Gate 4: Build Success = 100% (Inherited)
- [ ] Build executed successfully
- [ ] Zero warnings (warnings-as-errors)
- [ ] All analyzers passing

### Gate 5: Doc Sync = Zero Drift (Inherited)
- [ ] Documentation updated
- [ ] No drift detected
- [ ] All changes documented

### Gate 6: Chat Domain Invariants Preserved = 100%
- [ ] CHAT010: Turn-taking enforced (User → Assistant alternation)
- [ ] CHAT006: Message limit enforced (100 max per conversation)
- [ ] CHAT008: Content length validated (1-32K chars)
- [ ] CHAT003: Active conversation only (Status = Active)
- [ ] CHAT004: Ownership verified (403 Forbidden on wrong owner)
- [ ] CHAT013: AiResponseId uniqueness enforced (idempotency)
- [ ] All 6 critical invariants: 100%

### Gate 7: Owned Entity Patterns Correct = 100%
- [ ] Message accessed via Conversation only (no direct DbSet access)
- [ ] Composite keys configured: (ConversationId, MessageId)
- [ ] EF Core OwnsMany<Message> pattern applied
- [ ] No independent DbSet<Message>
- [ ] All owned entity patterns: 100%

### Gate 8: EF Core Configuration Correct = 100%
- [ ] OwnsMany<Message>("_messages") configured
- [ ] Composite key: HasKey(m => new { m.ConversationId, m.Id })
- [ ] Concurrency token: xmin on Conversation only (not on Message)
- [ ] No separate DbSet<Message>- [ ] EF Core configuration: 100%

### Gate 9: AI Integration Patterns Correct = 100%
- [ ] AiResponseId idempotency enforced (CHAT013)
- [ ] Prompt caching via LastAiResponseId (previous_response_id)
- [ ] MCP server resolution implemented (if AI subdomain)
- [ ] Transient error retry (HTTP 502/504)
- [ ] Business rule failures (no retry)
- [ ] AI integration patterns: 100%

### Gate 10: Universal Endpoint Correctness = 100%
- [ ] POST /api/v1/chat/turns endpoint created
- [ ] conversationId = null → Start new conversation
- [ ] conversationId = guid → Append to existing
- [ ] Both paths tested and working
- [ ] Universal endpoint: 100%

### Gate 11: Chat Test Scenarios Complete = 8/8
- [ ] Scenario 1: Start conversation (null conversationId) ✅
- [ ] Scenario 2: Continue conversation (existing conversationId) ✅
- [ ] Scenario 3: Turn-taking enforcement (422 error) ✅
- [ ] Scenario 4: Message limit enforcement (422 error) ✅
- [ ] Scenario 5: Ownership enforcement (403 error) ✅
- [ ] Scenario 6: Concurrency conflict (409 error) ✅
- [ ] Scenario 7: AI integration (assistant message returned) ✅
- [ ] Scenario 8: Idempotency (same AiResponseId) ✅
- [ ] All 8 scenarios: 100%

---

## Final Approval

### Checkpoint 4: Final Commit Approval
- [ ] All 11 success gates passed (5 inherited + 6 Chat-specific)
- [ ] Code quality verified
- [ ] Tests comprehensive (103+ tests, 92%+ coverage)
- [ ] Documentation synchronized
- [ ] Decision log captured
- [ ] Ready for commit

### Commit Message
```
feat(chat): {story-title}

{story-summary}

Changes:
- Domain: {domain-changes-summary}
- Application: {application-changes-summary}
- Infrastructure: {infrastructure-changes-summary}
- API: {api-changes-summary}

Tests: {test-count} tests, {coverage}% coverage
AC Coverage: 100%
Pattern Compliance: {compliance}%

🤖 Generated with Claude Code
Co-Authored-By: Claude <noreply@anthropic.com>
```

---

**End of Checklist**