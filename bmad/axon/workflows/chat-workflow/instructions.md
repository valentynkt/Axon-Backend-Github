# Chat Workflow - Module Enhancement Instructions

<workflow>

<critical>Governed by: {project-root}/bmad/core/tasks/workflow.md</critical>
<critical>Loaded config: {project-root}/bmad/axon/workflows/chat-workflow/workflow.yaml</critical>
<critical>EXTENDS story-implementation - inject at 4 strategic points, do NOT duplicate</critical>

## Overview

Enhances **story-implementation** with Chat module expertise for conversation management, message processing, AI integration (Claude API + MCP), and real-time communication in brownfield .NET + Clean Architecture + DDD + CQRS.

**Invocation**: `story-orchestrator` when `module = "Chat"`

**Core Patterns**: Conversation aggregate (50+ methods), owned Message entities, 19 business rules, universal endpoint (/api/v1/chat/turns), AI integration (idempotency via AiResponseId), domain events (6).

---

<step n="0" goal="Initialize Chat context">
<action>Load {installed_path}/workflow.yaml</action>
<action>Confirm module = "Chat"</action>
<action>Set Chat context flag for all agents</action>
</step>

---

## ENHANCEMENT POINT 1: CHAT DOC LOADING

<step n="1" goal="Load Chat documentation (8 files)">
<action>Load all 8 Chat docs referenced in workflow.yaml:

**Module Docs** (5 files - chat_module_docs in workflow.yaml):
- 00-INDEX.md, 01-domain-model.md, 03-messaging-flows.md, 05-api-contracts.md, 06-database-schema.md

**Library Docs** (3 files - chat_library_docs in workflow.yaml):
- MediatR/IMPLEMENTATION_GUIDE.md (CQRS)
- ModelContextProtocol/IMPLEMENTATION_GUIDE.md (MCP SDK)
- ModelContextProtocolAspNetCore/IMPLEMENTATION_GUIDE.md (MCP ASP.NET Core)

**Optional Integration Docs** (1 file):
- integrations/00-INDEX.md (Claude API patterns)
</action>

<action>Parse workflow.yaml context:
- chat_subdomains (4)
- chat_domain_invariants (6 critical + 13 additional = 19 total)
- conversation_aggregate_path
- business_rules_path
- domain_events_path
</action>

<critical>All 8 docs must be loaded before proceeding</critical>
</step>

<step n="2" goal="Classify to Chat subdomain">
<action>Analyze story, match to ONE primary subdomain:
1. **Conversation Management** - Conversation aggregate, lifecycle, CRUD, state
2. **Message Processing** - Turn-taking, sequencing, validation, content
3. **AI Integration** - Claude API, MCP servers, prompt caching, streaming
4. **Real-Time Communication** - SSE, streaming responses, async processing
</action>

<action>Set subdomain context from workflow.yaml:
- {{chat_subdomain}} (primary)
- {{chat_patterns}} (key patterns)
- {{chat_services}} (AI services if applicable)
</action>

<output section="subdomain_classification">
Subdomain: {{chat_subdomain}}
Patterns: {{chat_patterns}}
Services: {{chat_services}}
</output>
</step>

---

## ENHANCEMENT POINT 2: CHAT PRE-FLIGHT VALIDATION

<step n="3" goal="Chat codebase discovery (@axon-archaeologist)">
<action>Invoke @axon-archaeologist:

**Search Strategy** (5 layers):
1. **Domain**: Conversation aggregate (50+ methods - factory, commands, queries), Message entity (owned), business rules (19), domain events (6)
2. **Application**: CQRS handlers (StartConversation, ProcessTurn, GetConversations), services (AiProcessingService, McpServerResolutionService, MessageProcessingOrchestrator)
3. **Infrastructure**: AI integration (IAiClient, Claude API), MCP resolution, EF Core configs (OwnsMany<Message>)
4. **API**: Universal endpoint (POST /api/v1/chat/turns), FastEndpoints patterns
5. **Tests**: Test patterns (Domain: business rules, Application: handlers, Infrastructure: AI client tests, E2E: turn flows)

**Reuse Focus**: Find similar Conversation methods, existing services, handler patterns, business rule patterns, test patterns.
</action>

<output section="discovery_report">
**Reuse Recommendations**:
- REUSE: [Components as-is]
- EXTEND: [Components to extend]
- ADAPT: [Patterns to adapt]
- CREATE: [New components]
</output>
</step>

<step n="4" goal="Chat library validation (@axon-library-sage)">
<action>Invoke @axon-library-sage:

**Validate against Chat library stack** (3 libraries from workflow.yaml):
1. MediatR - CQRS command/query patterns
2. ModelContextProtocol SDK - MCP server registration, tool execution
3. ModelContextProtocolAspNetCore - ASP.NET Core integration, DI

**4-Factor Scoring**: Capability match, complexity reduction, maintenance burden, integration cost.
</action>

<output section="library_validation">
**Library Recommendations**:
- Library-First: [Which libraries]
- Extension Needed: [Which services]
- Manual: [Justification]
</output>
</step>

<step n="5" goal="Chat pattern validation (@axon-doc-oracle)">
<action>Invoke @axon-doc-oracle:

**Validate 6 dimensions + Chat-specific**:
1. **Result<T, Error>**: All Conversation commands, services, handlers
2. **StrongId<T>**: ConversationId, MessageId, AxonUserId
3. **CQRS**: BaseChatCommandHandler, BaseChatQueryHandler
4. **Domain Events**: ConversationStartedEvent, UserMessageAppendedEvent, AssistantMessageAppendedEvent, ConversationCompletedEvent, ConversationTitleUpdatedEvent, ConversationHistoryImportedEvent (6 events)
5. **Owned Entities** (Chat-specific): Message with composite keys (ConversationId, MessageId), OwnsMany, no independent DbSet<Message>, single xmin token on Conversation
6. **Business Rules** (Chat-specific): 19 rules from workflow.yaml - 6 critical invariants (CHAT010 turn-taking, CHAT006 message limit, CHAT008 content length, CHAT003 active only, CHAT004 ownership, CHAT013 AiResponseId uniqueness) + 13 additional rules

**ADR Compliance**: ADR-001 (Modular Monolith), ADR-002 (CQRS), ADR-003 (Result), ADR-004 (Strong IDs), ADR-005 (PostgreSQL + xmin)
</action>

<output section="pattern_validation">
**Compliance Score**: [95-100%]
**Business Rules**: [19/19 preserved]
**Violations**: [None / List]
</output>
</step>

---

## ✅ CHECKPOINT 2: CHAT PRE-FLIGHT APPROVAL

<step n="6" goal="Approve Chat pre-flight">
<ask critical="true">
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ CHECKPOINT 2: CHAT PRE-FLIGHT APPROVAL
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

**Subdomain**: {{chat_subdomain}}
**Reuse Score**: [High/Medium/Low]
**Libraries**: [List]
**Compliance**: [95-100%]
**Business Rules**: [19/19]

Approve proceeding to implementation?
[c] Continue | [e] Edit | [a] Abort
</ask>
</step>

---

## ENHANCEMENT POINT 3: CHAT IMPLEMENTATION GUIDANCE

<step n="7" goal="Chat-specific implementation guidance">
<action>Provide surgical implementation guidance based on subdomain:

**Conversation Aggregate Modifications**:
- 50+ existing methods (factory, commands, queries)
- Follow patterns: StartNewConversation(), AppendUserMessage(), AppendAssistantResponse(), AppendMessageExchange()
- Enforce business rules BEFORE mutation (19 rules from workflow.yaml)
- Raise domain events AFTER mutation (6 events)
- Return Result<T, Error>

**Message Entity Modifications**:
- Owned entity: Composite keys (ConversationId, MessageId)
- Factory methods: CreateUserMessage(), CreateAssistantMessage()
- AiResponseId: Required for Assistant, null for User
- EF Core: OwnsMany, no independent DbSet, single xmin token on Conversation

**Business Rule Enforcement** (6 critical):
1. **CHAT010: Turn-Taking** → MessageTurnTakingRule (User → Assistant alternation)
2. **CHAT006: Message Limit** → ConversationCanAcceptMoreMessagesRule (100 max)
3. **CHAT008: Content Length** → MessageContentWithinLimitsRule (1-32K chars)
4. **CHAT003: Active Only** → ConversationMustBeActiveRule
5. **CHAT004: Ownership** → ConversationMustBelongToOwnerRule (403 Forbidden)
6. **CHAT013: AiResponseId Uniqueness** → AiResponseIdMustBeUniqueRule (idempotency)

**AI Integration Patterns**:
- AiProcessingService - Claude API integration
- McpServerResolutionService - MCP server resolution
- MessageProcessingOrchestrator - Pipeline coordination
- Idempotency via AiResponseId

**Universal Endpoint**:
- POST /api/v1/chat/turns
- conversationId = null → Start new
- conversationId = guid → Append to existing
- Flow: User message → Persistence → AI processing → Assistant message → Persistence

**CQRS Handlers**:
- Commands: BaseChatCommandHandler
- Queries: BaseChatQueryHandler
- Pattern: Load aggregate → Invoke method → SaveChanges

**API Endpoints**:
- FastEndpoints: Endpoint<TRequest, TResponse>
- Route: POST/GET /api/v1/chat/{route}
- FluentValidation for request validation
- Map Result<T, Error> → HTTP status codes
</action>

<output section="implementation_plan">
**Bottom-Up Layering**:
1. Domain: [Conversation methods, Message entity, business rules, events]
2. Application: [Handlers, services (AI, MCP)]
3. Infrastructure: [IAiClient implementation, EF Core config, migrations]
4. API: [Universal endpoint, FastEndpoints]
</output>
</step>

---

## ENHANCEMENT POINT 4: CHAT VALIDATION

<step n="8" goal="Chat comprehensive testing (@axon-quality-guardian)">
<action>Invoke @axon-quality-guardian:

**4-Layer Test Strategy**:
1. **Domain**: Conversation command tests (50+ methods), Message entity tests, business rule tests (19 rules), domain event tests (6 events)
2. **Application**: Handler integration tests (in-memory DB), AI service tests (mocked Claude API), MCP resolution tests
3. **Infrastructure**: EF Core tests (composite keys, xmin concurrency), AI client integration tests (Claude API), MCP integration tests
4. **E2E**: Turn flows (Start → Append User → AI Response → Append Assistant), universal endpoint tests (new + append)

**Chat Test Scenarios** (from workflow.yaml success_metrics):
- Turn-taking enforcement (CHAT010)
- Message limit enforcement (CHAT006)
- Content validation (CHAT008)
- Active conversation only (CHAT003)
- Ownership verification (CHAT004)
- AiResponseId idempotency (CHAT013)
- Universal endpoint (new + append)
- AI integration (Claude + MCP)
</action>

<output section="test_plan">
**Coverage Breakdown**: [Domain, Application, Infrastructure, E2E]
**AC Coverage**: 100%
**Business Rule Coverage**: 19/19 (100%)
**Estimated Coverage**: 90%+
</output>
</step>

<step n="9" goal="Validate Chat business rules">
<action>Validate all 19 business rules from workflow.yaml:

**Critical Rules** (6):
1. **CHAT010: Turn-Taking** → MessageTurnTakingRule, Conversation.AppendUserMessage/AppendAssistantResponse
2. **CHAT006: Message Limit** → ConversationCanAcceptMoreMessagesRule (100 max)
3. **CHAT008: Content Length** → MessageContentWithinLimitsRule, MessageContent value object (1-32K)
4. **CHAT003: Active Only** → ConversationMustBeActiveRule
5. **CHAT004: Ownership** → ConversationMustBelongToOwnerRule (403 Forbidden)
6. **CHAT013: AiResponseId Uniqueness** → AiResponseIdMustBeUniqueRule, Conversation.AppendAssistantResponse

**Additional Rules** (13): See workflow.yaml for complete list.

**Validation**: Code check + Test check for each rule.
</action>

<output section="business_rule_validation">
**Business Rules Preserved**: 19/19 (100%) ✅
**Critical Rules**: 6/6 validated ✓
</output>
</step>

---

## COMPLETION

<step n="10" goal="Chat summary">
<output section="completion_summary">
**Chat Workflow Complete** ✅

**Module**: Chat
**Subdomain**: {{chat_subdomain}}

**Deliverables**:
- ✓ 8 Chat docs loaded
- ✓ Subdomain classified
- ✓ Codebase discovery (reuse recommendations)
- ✓ Library validation (MediatR, MCP SDK, MCP AspNetCore)
- ✓ Pattern compliance (95%+)
- ✓ Business rules preserved (19/19)
- ✓ Comprehensive test suite
- ✓ Owned entity patterns validated

**Quality Metrics**:
- Test Coverage: 90%+
- AC Coverage: 100%
- Business Rules: 100%
- Build: 100%
- Doc Sync: Zero drift
</output>

<critical>Append to base story-implementation summary</critical>
</step>

</workflow>