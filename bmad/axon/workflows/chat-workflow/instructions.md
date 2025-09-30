# Chat Workflow - Module-Specific Enhancement Instructions

<workflow>

<critical>The workflow execution engine is governed by: {project-root}/bmad/core/tasks/workflow.md</critical>
<critical>You MUST have already loaded and processed: {project-root}/bmad/axon/workflows/chat-workflow/workflow.yaml</critical>
<critical>This workflow EXTENDS story-implementation - it adds Chat-specific context to the base 4-phase workflow</critical>
<critical>Do NOT duplicate the base workflow - inject Chat enhancements at strategic points</critical>

## Overview

This workflow enhances story-implementation with **Chat module expertise**:
- **Conversation Mastery**: Aggregate patterns (Conversation owns Messages), owned entities, composite keys
- **AI Integration Mastery**: Claude API orchestration, MCP servers, streaming (SSE), prompt caching
- **Domain Mastery**: 19 business rules, turn-taking enforcement, idempotency, message limits
- **Pattern Mastery**: EF Core OwnsMany, async orchestration, transactional consistency

## Chat Module Context

This workflow is invoked by **story-orchestrator** when `module = "Chat"` is detected.

**Chat Subdomains**:
1. **Conversation Management** - Lifecycle, ownership, status (Active/Completed)
2. **Message Processing** - Turn-taking, idempotency, atomic operations
3. **AI Integration** - Claude API, MCP servers, prompt caching, streaming
4. **Business Rules** - 19 domain rules, validation, limits

**Key Codebase Components**:
- **Aggregate Root**: Conversation (owns Messages via EF Core OwnsMany)
- **Owned Entity**: Message (composite key: ConversationId + MessageId)
- **Services**: MessageProcessingOrchestrator, AiProcessingService, McpServerResolutionService
- **CQRS Handlers**: StartConversation, AppendUserMessage, GetConversations, GetConversationMessages

---

<step n="0" goal="Initialize Chat workflow context">
<action>Load Chat workflow configuration: {installed_path}/workflow.yaml</action>
<action>Confirm this is a Chat module story (module = "Chat")</action>
<action>Set Chat context flag for all agents</action>

<critical>This step happens BEFORE story-implementation Step 0</critical>
</step>

---

## ENHANCEMENT POINT 1: CHAT-SPECIFIC DOC LOADING

<step n="1" goal="Load Chat module documentation (8 files: 5 module + 3 library)">
<action>Load all 8 Chat documentation files:

**Chat Module Docs (5 files)**:
1. {engineering_docs}/modules/chat/00-INDEX.md
   - Module overview, responsibilities, quick start
   - Aggregate: Conversation (owns Messages)
   - Domain events: 6 events
   - Commands: 2, Queries: 2

2. {engineering_docs}/modules/chat/01-domain-model.md
   - Conversation aggregate (command methods, query methods)
   - Message owned entity (factory methods CreateUser/CreateAssistant)
   - 6 domain invariants (turn-taking, limits, idempotency)
   - 19 business rules in Domain/Rules/
   - Value objects: ConversationId, MessageId, AiResponseId, MessageContent, MessageRole

3. {engineering_docs}/modules/chat/03-messaging-flows.md
   - Universal chat turn flow (POST /api/v1/chat/turns)
   - Message processing pipeline (MCP resolution → AI processing → persistence)
   - MessageProcessingOrchestrator orchestration
   - Idempotency via AiResponseId
   - Error handling (transient retries, 409 conflicts)

4. {engineering_docs}/modules/chat/05-api-contracts.md
   - POST /api/v1/chat/turns (universal endpoint)
   - GET /api/v1/conversations (pagination, filtering)
   - GET /api/v1/conversations/{id}/messages (pagination)
   - Error responses (400, 401, 403, 404, 409, 422, 502, 504)

5. {engineering_docs}/modules/chat/06-database-schema.md
   - EF Core owned entity configuration (OwnsMany)
   - Composite keys: (ConversationId, MessageId)
   - Concurrency: Single xmin token on aggregate root
   - Tables: Conversations (aggregate), Messages (owned)

**Chat Library Docs (3 files)**:
6. {libraries_docs}/OpenAI/IMPLEMENTATION_GUIDE.md
   - Claude API client patterns
   - Request/response mapping
   - Error handling strategies

7. {libraries_docs}/ModelContextProtocol/IMPLEMENTATION_GUIDE.md
   - MCP SDK usage
   - Server resolution patterns
   - Tool/resource integration

8. {libraries_docs}/ModelContextProtocolAspNetCore/IMPLEMENTATION_GUIDE.md
   - ASP.NET Core MCP integration
   - Dependency injection patterns
   - Lifecycle management
</action>

<action>Parse Chat-specific context from workflow.yaml:
  - 4 Chat subdomains (conversation_management, message_processing, ai_integration, business_rules)
  - 8 Chat challenges
  - 6 domain invariants
  - Key codebase patterns (aggregate_root, owned_entities, cqrs_handlers, async_orchestration)
</action>

<critical>All 8 docs must be loaded in memory for Chat-aware decision-making</critical>
</step>

<step n="2" goal="Map story to Chat subdomain">
<action>Analyze story content and classify into Chat subdomain:

**Conversation Management Subdomain** - Keywords: conversation, create, list, title, status, lifecycle
- Services: ConversationRepository, ChatWriteDbContext
- Patterns: Aggregate root ownership, soft delete, optimistic concurrency
- Tests: Conversation creation, title updates, completion, ownership validation

**Message Processing Subdomain** - Keywords: message, turn, append, exchange, ordering, sequence
- Services: MessageProcessingOrchestrator, ChatCommandDispatcher
- Patterns: Turn-taking enforcement, atomic operations, idempotency
- Tests: Turn-taking rules, message ordering, idempotent AI responses

**AI Integration Subdomain** - Keywords: AI, Claude, MCP, streaming, prompt caching, assistant
- Services: AiProcessingService, McpServerResolutionService, IAiClient
- Patterns: Prompt caching (previous_response_id), MCP resolution, SSE streaming
- Tests: Claude API integration, MCP server resolution, error handling

**Business Rules Subdomain** - Keywords: validation, limits, rules, constraints, invariants
- Entities: Conversation, Message (19 business rules)
- Patterns: Rule enforcement at domain layer, Result<T> error handling
- Tests: All 19 business rules, limit enforcement, error scenarios
</action>

<action>Set subdomain context variables:
  - {{chat_subdomain}} (primary classification)
  - {{chat_services}} (relevant services to consider)
  - {{chat_patterns}} (patterns to apply)
  - {{chat_tests}} (test scenarios to generate)
</action>

<template-output section="chat_subdomain_mapping">
**Chat Subdomain Classification**

Story: {{story_title}}
Primary Subdomain: {{chat_subdomain}}

**Relevant Components**:
- Services: {{chat_services}}
- Patterns: {{chat_patterns}}
- Test Scenarios: {{chat_tests}}

**Domain Invariants to Validate**:
[List applicable invariants from the 6 defined in workflow.yaml]

**Codebase Impact Analysis**:
- Conversation aggregate modifications needed? [Yes/No - which methods?]
- Message entity changes? [Fields, validation?]
- New services? [List if creating new services]
- Service extensions? [Which existing services to extend?]
- Business rules affected? [Which of 19 rules?]
</template-output>

<critical>This classification guides all subsequent Chat-specific validation</critical>
</step>

---

## ENHANCEMENT POINT 2: CHAT-SPECIFIC PRE-FLIGHT VALIDATION

<step n="3" goal="Chat-enhanced codebase discovery (Archaeologist)">
<action>Invoke @axon-archaeologist with Chat context:

**Search Strategy** (5 layers - Chat-focused):

**Layer 1: Domain Layer** (src/Modules/Chat/Domain/)
- Search Conversation aggregate:
  * Conversation.cs: Properties, factory methods (StartNewConversation)
  * Command methods: AppendUserMessage, AppendAssistantResponse, AppendMessageExchange, Complete, UpdateTitle
  * Query methods: GetAllMessages, GetRecentMessages, GetMessageBySequence, GetStatistics
  * Private _messages collection (owned entities)
- Search Message owned entity:
  * Message.cs: Factory methods (CreateUserMessage, CreateAssistantMessage)
  * Fields: Role, Content, Sequence, AiResponseId
  * Invariants: Assistant MUST have AiResponseId, User MUST NOT
- Search business rules:
  * Domain/Rules/: 19 business rule classes
  * Key rules: MessageTurnTakingRule (CHAT010), ConversationCanAcceptMoreMessagesRule (CHAT006), AiResponseIdMustBeUniqueRule (CHAT013)
- Search domain events:
  * ConversationStartedEvent, UserMessageAppendedEvent, AssistantMessageAppendedEvent, ConversationCompletedEvent

**Layer 2: Application Layer** (src/Modules/Chat/Application/)
- Search CQRS handlers:
  * Commands: StartConversationHandler, AppendUserMessageHandler
  * Queries: GetConversationsHandler, GetConversationMessagesHandler
  * Identify handler patterns (BaseCommand/QueryHandler inheritance)
- Search services:
  * MessageProcessingOrchestrator: Pipeline coordination
  * AiProcessingService: Claude API integration
  * McpServerResolutionService: MCP server resolution
  * ChatCommandDispatcher: Routing logic (Start vs Append)

**Layer 3: Infrastructure Layer** (src/Modules/Chat/Infrastructure/)
- Search EF Core configurations:
  * Persistence/Configurations/ConversationConfiguration.cs
  * OwnsMany configuration for Messages
  * Composite key: (ConversationId, MessageId)
  * Concurrency token (xmin)
- Search repositories:
  * ConversationRepository: Aggregate persistence patterns
- Search external services:
  * ExternalServices/AI/: Claude API client implementation

**Layer 4: API Layer** (src/Api/Endpoints/V1/Chat/)
- Search endpoints:
  * ChatTurnEndpoint: Universal endpoint (POST /api/v1/chat/turns)
  * GetConversationsEndpoint: Pagination, filtering
  * GetConversationMessagesEndpoint: Message retrieval
- Search validators:
  * ChatTurnRequestValidator: FluentValidation rules

**Layer 5: Test Layer** (tests/Modules/Chat/)
- Search test patterns:
  * Domain/: Conversation aggregate tests (132+ test files)
  * Application/: Handler tests, service tests
  * Infrastructure/: EF Core tests, persistence tests
</action>

<template-output section="chat_discovery_report">
**Chat Codebase Discovery Report**

**Reusable Components Found**:
- Conversation Methods: [List relevant command/query methods]
- Services: [Existing services that can be extended]
- Handlers: [Similar CQRS handler patterns]
- Tests: [Test patterns to replicate]

**Gaps Requiring New Code**:
- New command methods on Conversation? [List]
- New services? [List with rationale]
- New handlers? [List]
- Database migrations needed? [Yes/No - what changes?]

**Reuse Recommendations**:
- REUSE: [Components to use as-is]
- EXTEND: [Components to extend surgically]
- ADAPT: [Patterns to adapt]
- CREATE: [New components needed]

**Pattern Compliance Check**:
- Result<T, Error> pattern usage: ✓
- StrongId<T> usage: ✓
- Domain events: [Which events to raise?]
- Owned entity patterns: [Follows OwnsMany conventions?]
</template-output>
</step>

<step n="4" goal="Chat-enhanced library validation (Library Sage)">
<action>Invoke @axon-library-sage with Chat library context:

**Chat Library Stack** (validate against these):
1. **OpenAI** (Anthropic Claude API)
   - Claude API client patterns
   - Message format (user/assistant roles)
   - Streaming support (SSE)
   - Error handling (transient retries)
   - Usage: IAiClient, AiProcessingService

2. **ModelContextProtocol** (MCP SDK)
   - MCP server resolution
   - Tool/resource integration
   - Client-server communication
   - Usage: McpServerResolutionService

3. **ModelContextProtocolAspNetCore** (MCP ASP.NET Core)
   - Dependency injection integration
   - Lifecycle management
   - Configuration patterns
   - Usage: DI registration in Infrastructure layer

**Validation Checks**:
- Does story require Claude API calls? → IAiClient available
- Does story require MCP servers? → McpServerResolutionService available
- Does story require streaming? → SSE infrastructure available
- Are there missing library capabilities? → Recommend library additions

**4-Factor Scoring** (Library vs Manual):
1. **Capability Match**: Does library handle this requirement?
2. **Complexity Reduction**: How much code does it save?
3. **Maintenance Burden**: Does it reduce long-term maintenance?
4. **Integration Cost**: How easy to integrate?
</action>

<template-output section="chat_library_report">
**Chat Library Validation Report**

**Library Recommendations**:
- OpenAI (Claude): [Capability match - Yes/No/Partial]
- ModelContextProtocol: [Capability match - Yes/No/Partial]
- ModelContextProtocolAspNetCore: [Capability match - Yes/No/Partial]

**Recommended Approach**:
- Library-First: [Which libraries to use]
- Extension Needed: [Which services to extend]
- Manual Implementation: [What must be manual - with justification]

**Library Usage Patterns** (from codebase):
[Show existing usage patterns to follow]

**Risk Assessment**:
- Breaking changes: [Library version compatibility]
- Integration complexity: [High/Medium/Low]
</template-output>
</step>

<step n="5" goal="Chat-enhanced pattern validation (Doc Oracle)">
<action>Invoke @axon-doc-oracle with Chat pattern context:

**Pattern Validation Dimensions** (6 dimensions + Chat-specific):

**1. Result<T, Error> Pattern**:
- All Conversation command methods return Result<T, Error>
- All service methods return Result<T, Error>
- All handlers return Result<TResponse, Error>
- Error types: ChatDomainErrors, MessageDomainErrors

**2. StrongId<T> Pattern**:
- ConversationId (aggregate root ID)
- MessageId, AiResponseId (value object IDs)
- Custom Vogen value objects: MessageContent, MessageRole, ConversationStatus

**3. CQRS Pattern**:
- Commands: Inherit from ChatBaseCommand
- Queries: Inherit from ChatBaseQuery
- Handlers: BaseChatCommandHandler, BaseChatQueryHandler

**4. Domain Events Pattern**:
- Raise events on state changes: ConversationStartedEvent, MessageAppendedEvents, CompletedEvent
- Events published via MediatR pipeline
- No business logic in event handlers

**5. Owned Entity Pattern** (Chat-specific):
- Composite keys: (ConversationId, MessageId)
- OwnsMany configuration in EF Core
- No independent DbSet for Messages
- Single concurrency token on aggregate root (xmin)
- Access only via Conversation aggregate

**6. Domain Invariants** (Chat-specific):
- Turn-Taking Rule (CHAT010)
- Message Limit (100 max - CHAT006)
- Content Length (1-32K - CHAT008)
- Active Conversation Only (CHAT003)
- Ownership Verification (CHAT004)
- AiResponseId Uniqueness (CHAT013)

**ADR Compliance**:
- ADR-001: Modular Monolith (Chat is a bounded context)
- ADR-002: CQRS + MediatR
- ADR-003: Result Pattern
- ADR-004: Strong IDs
- ADR-005: PostgreSQL (EF Core owned entities, xmin)
</action>

<template-output section="chat_pattern_validation">
**Chat Pattern Validation Report**

**Compliance Score**: [95-100%]

**Pattern Checklist**:
- ✓ Result<T, Error> pattern usage
- ✓ StrongId<T> usage
- ✓ CQRS command/query separation
- ✓ Domain events on state changes
- ✓ Owned entity patterns (OwnsMany, composite keys)
- ✓ Domain invariants preserved (6/6)
- ✓ ADR compliance (5/5 applicable ADRs)

**Violations Detected**: [None / List violations with remediation]

**Chat-Specific Validations**:
- Owned entity composite keys correct? ✓
- No independent DbSet for Messages? ✓
- Single concurrency token strategy? ✓
- Domain invariants enforced in code? ✓

**Risk Assessment**:
- Pattern violations: [None / List]
- Architectural drift: [None / List]
- Invariant violations: [None / List]
</template-output>
</step>

---

## ✅ CHECKPOINT 2: CHAT PRE-FLIGHT APPROVAL (3-5 min)

<step n="6" goal="Checkpoint 2: Approve Chat-enhanced pre-flight">
<action>Combine outputs from steps 3, 4, 5 into unified pre-flight report</action>
<ask critical="true">
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ CHECKPOINT 2: CHAT PRE-FLIGHT APPROVAL
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

**Chat Subdomain**: {{chat_subdomain}}

**Discovery Summary**:
- Reusable components: [Count]
- New components needed: [Count]
- Reuse score: [High/Medium/Low]

**Library Summary**:
- Libraries to use: [List]
- Manual implementation: [Justification]

**Pattern Summary**:
- Compliance score: [95-100%]
- Domain invariants: [6/6 preserved]
- Violations: [None / List]

Review the Chat-enhanced pre-flight analysis above.

Do you approve proceeding to implementation?
- [c] Continue to Implementation Phase
- [e] Edit/clarify pre-flight findings
- [a] Abort workflow

Your choice:
</ask>

<action if="user_response == 'e'">
  <ask>What needs clarification in the pre-flight analysis?</ask>
  <action>Re-run specific validation based on feedback</action>
  <goto step="6">Re-present pre-flight for approval</goto>
</action>

<action if="user_response == 'a'">
  <action>Abort workflow and document reason</action>
  <exit/>
</action>
</step>

---

## ENHANCEMENT POINT 3: CHAT-SPECIFIC IMPLEMENTATION GUIDANCE

<step n="7" goal="Provide Chat-specific code generation guidance">
<action>Based on subdomain classification and pre-flight analysis, provide surgical implementation guidance:

**If Conversation Aggregate Modification Required**:
1. **Determine method type**:
   - New factory method? → Add to Conversation.cs
   - New command method? → Add to Conversation.cs (pattern: AppendXxx, UpdateXxx)
   - New query method? → Add to Conversation.cs (pattern: GetXxx, HasXxx)

2. **Follow existing method patterns**:
   - Signature: `public Result<T, Error> MethodName(params)`
   - Validate invariants BEFORE state mutation
   - Mutate _messages collection if needed
   - Raise domain events AFTER successful mutation
   - Return Result.Success or Result.Failure

3. **Domain Invariant Enforcement**:
   - Turn-Taking: Check MessageTurnTakingRule before append
   - Message Limit: Check ConversationCanAcceptMoreMessagesRule
   - Content Length: Validate via MessageContent value object
   - Active Status: Check ConversationMustBeActiveRule
   - Ownership: Verify in API layer (authorization)
   - AiResponseId Uniqueness: Check in AppendAssistantResponse

4. **Domain Event Raising**:
   - ConversationStartedEvent: On factory method
   - UserMessageAppendedEvent: After user message added
   - AssistantMessageAppendedEvent: After AI response added
   - ConversationCompletedEvent: On Complete()
   - ConversationTitleUpdatedEvent: On UpdateTitle()

**If Message Entity Modification Required**:
1. **Maintain Composite Key Pattern**:
   - All messages: (ConversationId, MessageId)
   - Never modify composite key pattern
   - Access only via aggregate root

2. **Factory Methods**:
   - CreateUserMessage(): AiResponseId = null (invariant)
   - CreateAssistantMessage(): AiResponseId REQUIRED (invariant)

3. **EF Core Configuration**:
   - Use OwnsMany() in ConversationConfiguration
   - Configure composite key: HasKey(m => new { m.ConversationId, m.Id })
   - No independent DbSet<Message>

4. **Migration Required?**:
   - New fields on Message? → Yes, create migration
   - Modified indexes? → Yes, update migration

**If Service Implementation/Extension Required**:
1. **Follow existing service patterns**:
   - Constructor injection: ILogger, dependencies
   - Return Result<T, Error> from all public methods
   - Use async/await for I/O operations

2. **Message Processing Services**:
   - MessageProcessingOrchestrator: Coordinates entire pipeline
   - AiProcessingService: Claude API client wrapper
   - McpServerResolutionService: MCP server discovery

3. **Service Responsibilities**:
   - Orchestrator: MCP resolution → AI processing → persistence
   - AiProcessingService: Prompt building, API calls, error handling
   - McpServerResolutionService: Server configuration lookup

**If CQRS Handler Required**:
1. **Command Handlers**:
   - Inherit: BaseChatCommandHandler
   - Pattern: Load aggregate → Invoke command method → SaveChanges
   - Example: StartConversationHandler creates + processes first message

2. **Query Handlers**:
   - Inherit: BaseChatQueryHandler
   - Read from ChatReadDbContext (separate from write)
   - Support pagination (GetConversations, GetMessages)

3. **Handler Testing**:
   - Integration tests with in-memory database
   - Test success path + all error paths
   - Test concurrency (DbUpdateConcurrencyException)

**If API Endpoint Required**:
1. **FastEndpoints Pattern**:
   - Inherit: Endpoint<TRequest, TResponse>
   - Configure: POST/GET /api/v1/chat/{route}
   - Use FluentValidation for request validation
   - Map Result<T, Error> to HTTP status codes

2. **Authentication**:
   - All endpoints require JWT Bearer token
   - Conversation ownership validated (403 Forbidden if not owner)

3. **Pagination Support**:
   - Query parameters: pageNumber, pageSize, sortBy, sortDirection
   - Response metadata: totalCount, totalPages, hasPrevious, hasNext
</action>

<template-output section="chat_implementation_plan">
**Chat Implementation Plan**

**Bottom-Up Layering** (Axon mandated order):

**Layer 1: Domain** (src/Modules/Chat/Domain/)
Files to modify/create:
- [ ] Conversation.cs: [New method: MethodName()]
- [ ] Message.cs: [New field: FieldName]
- [ ] Events/: [New event: EventName]
- [ ] Rules/: [New rule: RuleName]

Domain Invariants to Enforce:
- [List applicable invariants with enforcement strategy]

**Layer 2: Application** (src/Modules/Chat/Application/)
Files to modify/create:
- [ ] Commands/NewCommand/: NewCommandHandler.cs, NewCommand.cs
- [ ] Queries/NewQuery/: NewQueryHandler.cs, NewQuery.cs
- [ ] Services/: [NewService.cs or extend existing]

**Layer 3: Infrastructure** (src/Modules/Chat/Infrastructure/)
Files to modify/create:
- [ ] Services/: [Implementation of INewService]
- [ ] Persistence/Configurations/: [EF Core configuration if schema changes]
- [ ] Migrations/: [New migration if database changes]

**Layer 4: API** (src/Api/Endpoints/V1/Chat/)
Files to modify/create:
- [ ] NewEndpoint.cs: [FastEndpoint implementation]

**Implementation Notes**:
- Owned entity patterns: [Specific considerations]
- Domain events: [Which to raise, when]
- Library usage: [Which libraries, how]
- Test strategy: [Which test types needed]
</template-output>

<critical>Implementation Surgeon uses this plan for surgical code generation</critical>
</step>

---

## ENHANCEMENT POINT 4: CHAT-SPECIFIC VALIDATION

<step n="8" goal="Generate Chat-comprehensive test suite">
<action>Invoke @axon-quality-guardian with Chat test context:

**Chat Test Generation Strategy** (Multi-layer):

**Domain Tests** (tests/Modules/Chat/Domain/):
1. **Conversation Aggregate Tests**:
   - Factory method tests (StartNewConversation)
   - Command method tests (AppendUserMessage, AppendAssistantResponse, AppendMessageExchange)
   - Query method tests (GetAllMessages, GetRecentMessages, GetStatistics)
   - Test pattern: Arrange → Act → Assert Result + State + Events
   - Test all 6 invariants: Turn-taking, message limit, content length, etc.

2. **Message Entity Tests**:
   - Factory method tests (CreateUserMessage, CreateAssistantMessage)
   - Invariant tests: Assistant MUST have AiResponseId, User MUST NOT

3. **Business Rule Tests** (19 rules):
   - MessageTurnTakingRule (CHAT010)
   - ConversationCanAcceptMoreMessagesRule (CHAT006)
   - MessageContentWithinLimitsRule (CHAT008)
   - ConversationMustBeActiveRule (CHAT003)
   - ConversationMustBelongToOwnerRule (CHAT004)
   - AiResponseIdMustBeUniqueRule (CHAT013)
   - [13 more rules]

4. **Domain Event Tests**:
   - Event raised on state changes?
   - Event payload correct?

**Application Tests** (tests/Modules/Chat/Application/):
1. **Command Handler Integration Tests**:
   - StartConversationHandler: Create + process first message
   - AppendUserMessageHandler: Append + trigger AI processing
   - Use in-memory database (ChatTestDbContext)
   - Test Result<T, Error> success and error paths

2. **Query Handler Tests**:
   - GetConversationsHandler: Pagination, filtering, sorting
   - GetConversationMessagesHandler: Message retrieval with pagination

3. **Service Unit Tests**:
   - MessageProcessingOrchestrator: Pipeline coordination
   - AiProcessingService: Claude API integration (mocked)
   - McpServerResolutionService: Server resolution

**Infrastructure Tests** (tests/Modules/Chat/Infrastructure/):
1. **EF Core Tests**:
   - Owned entity persistence (composite keys)
   - Concurrency control (xmin optimistic locking)
   - Cascade operations (messages follow conversation)

2. **Service Integration Tests**:
   - AiProcessingService: Mock Claude API responses
   - McpServerResolutionService: Configuration lookup

3. **Repository Tests**:
   - ConversationRepository: SaveChanges, query patterns

**E2E Tests** (tests/Modules/Chat/E2E/):
1. **Chat Turn Flow Tests**:
   - POST /api/v1/chat/turns (conversationId = null) → Start conversation
   - POST /api/v1/chat/turns (conversationId = existing) → Append to existing
   - Full flow: Start → Append → Get messages

2. **Conversation Management Tests**:
   - GET /api/v1/conversations → List with pagination
   - GET /api/v1/conversations/{id}/messages → Retrieve messages

**Chat-Specific Test Scenarios** (from workflow.yaml):
- ✓ Turn-taking enforcement (user → assistant)
- ✓ Idempotency (duplicate AiResponseId rejection)
- ✓ Message limit enforcement (100 max)
- ✓ Content length validation (1-32K chars)
- ✓ Ownership verification (403 Forbidden)
- ✓ Conversation status validation (Active only)
- ✓ AI integration (Claude API, MCP servers)
- ✓ Owned entity cascade operations
</action>

<template-output section="chat_test_plan">
**Chat Test Plan**

**Test Coverage Breakdown**:
- Domain Tests: [X tests] - Aggregate, entity, rules, events
- Application Tests: [X tests] - Handlers, services
- Infrastructure Tests: [X tests] - EF Core, repositories, external services
- E2E Tests: [X tests] - Chat turn flows, conversation management

**Critical Test Scenarios**:
1. [Test Name]: [What it validates]
2. [Test Name]: [What it validates]
...

**Acceptance Criteria Coverage**:
- AC1: [Test file that validates this]
- AC2: [Test file that validates this]
...
Coverage: 100% (all ACs tested)

**Domain Invariant Testing**:
- Turn-Taking Rule: [Test validates]
- Message Limit: [Test validates]
- Content Length: [Test validates]
- Active Conversation Only: [Test validates]
- Ownership Verification: [Test validates]
- AiResponseId Uniqueness: [Test validates]
Coverage: 100% (6/6 invariants tested)

**Estimated Coverage**: 90%+
</template-output>
</step>

<step n="9" goal="Validate Chat domain invariants">
<action>Run comprehensive invariant validation:

**Invariant 1: Turn-Taking Rule**
- Code check: MessageTurnTakingRule validates role alternation
- Test check: Tests validate user → assistant → user pattern
- Result: ✓ Pass / ✗ Fail

**Invariant 2: Message Limit**
- Code check: ConversationCanAcceptMoreMessagesRule enforces 100 max
- Test check: Tests validate 101st message rejected
- Result: ✓ Pass / ✗ Fail

**Invariant 3: Content Length**
- Code check: MessageContentWithinLimitsRule validates 1-32K chars
- Test check: Tests validate boundary conditions
- Result: ✓ Pass / ✗ Fail

**Invariant 4: Active Conversation Only**
- Code check: ConversationMustBeActiveRule blocks mutations on Completed
- Test check: Tests validate completed conversation rejection
- Result: ✓ Pass / ✗ Fail

**Invariant 5: Ownership Verification**
- Code check: ConversationMustBelongToOwnerRule validates OwnerId
- Test check: Tests validate 403 Forbidden for non-owner
- Result: ✓ Pass / ✗ Fail

**Invariant 6: AiResponseId Uniqueness**
- Code check: AiResponseIdMustBeUniqueRule prevents duplicates
- Test check: Tests validate idempotency (same AiResponseId returns existing)
- Result: ✓ Pass / ✗ Fail
</action>

<template-output section="chat_invariant_validation">
**Chat Domain Invariant Validation**

**Validation Results**:
- ✓ Turn-Taking Rule
- ✓ Message Limit
- ✓ Content Length
- ✓ Active Conversation Only
- ✓ Ownership Verification
- ✓ AiResponseId Uniqueness

**Invariants Preserved**: 6/6 (100%) ✅

**Code Review**:
- All invariants enforced in domain code
- Tests validate all invariants
- API layer enforces authorization

**Risk Assessment**:
- Invariant violations: None detected
</template-output>
</step>

---

## COMPLETION: CHAT WORKFLOW ENHANCEMENT COMPLETE

<step n="10" goal="Generate Chat-specific implementation summary">
<template-output section="chat_completion_summary">
**Chat Workflow Enhancement Complete** ✅

**Story**: {{story_title}} ({{story_id}})
**Module**: Chat
**Subdomain**: {{chat_subdomain}}

**Chat-Specific Deliverables**:
- ✓ 8 Chat docs loaded and validated (5 module + 3 library)
- ✓ Subdomain classification performed
- ✓ Codebase discovery completed (reuse recommendations)
- ✓ Library validation performed (OpenAI, MCP, etc.)
- ✓ Pattern compliance validated (95%+ score)
- ✓ Domain invariants preserved (6/6)
- ✓ Chat-comprehensive test suite generated
- ✓ Owned entity patterns validated

**Pattern Compliance**:
- Result<T, Error>: ✓
- StrongId<T>: ✓
- CQRS: ✓
- Domain Events: ✓
- Owned Entities: ✓
- Domain Invariants: ✓ (6/6)

**Quality Metrics**:
- Test Coverage: 90%+
- AC Coverage: 100%
- Invariants Preserved: 100%
- Build Success: 100%
- Doc Sync: Zero drift

**Codebase Impact**:
- Domain: [Files modified/created]
- Application: [Files modified/created]
- Infrastructure: [Files modified/created]
- API: [Files modified/created]
- Tests: [Test files created]

**Next Steps**:
- Review chat-specific implementation
- Run build + tests
- Validate all 6 domain invariants
- Commit with Chat context in message
</template-output>

<critical>This summary is appended to the base story-implementation completion summary</critical>
</step>

</workflow>