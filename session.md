# Axon Module Design Session - Complete Documentation

**Session Date**: 2025-09-29  
**Participant**: Valik (Solo AI-Driven Developer)  
**Objective**: Design ultra-efficient BMAD module for brownfield .NET development with doc-grounded, library-first approach

---

## 📋 TABLE OF CONTENTS

1. [Context & Pain Points](#context--pain-points)
2. [Creative Brainstorming Analysis](#creative-brainstorming-analysis)
3. [Final Architecture Design](#final-architecture-design)
4. [Agent Team Specification](#agent-team-specification)
5. [Workflow Catalog](#workflow-catalog)
6. [Module-Specific Workflows](#module-specific-workflows)
7. [Implementation Roadmap](#implementation-roadmap)

---

## 🎯 CONTEXT & PAIN POINTS

### **Project Context**

**Project**: Axon Backend  
**Architecture**: Modular Monolith + Clean Architecture + DDD + CQRS  
**Tech Stack**: .NET 10, FastEndpoints, MediatR, FluentValidation, EF Core 9, PostgreSQL  
**Codebase Size**: Large, complex brownfield  
**Modules**: 2 major (Identity, Chat)  
**Documentation**: 120+ docs in `/Docs` (PRODUCT, ENGINEERING, PROCESS)  
**Libraries**: 15+ with implementation guides

### **Developer Profile**

- **Solo developer** relying 100% on AI-driven workflow
- Workflow: Specs → Implementation → Tests → Validation (all AI-assisted)
- Requires **Human-in-the-Loop** at strategic checkpoints
- Values efficiency over micromanagement

### **Critical Pain Points**

#### **Pain Point #1: AI Reinvents Instead of Reuses** 🔴
**Problem**: AI creates new methods when existing ones exist in codebase
**Impact**: Wasted time, duplicate code, inconsistent patterns
**Example**: Creating new `VerifyWallet()` when `WalletVerificationService` already exists

#### **Pain Point #2: AI Writes Manual Code Instead of Using Libraries** 🔴
**Problem**: AI implements manual logic when library provides out-of-box solution
**Impact**: More code to maintain, missing library features, reinventing the wheel
**Example**: Manual retry loops instead of using Polly, manual validation instead of FluentValidation

#### **Pain Point #3: AI Hallucinates Non-Existent APIs** 🔴
**Problem**: AI invents methods/properties that don't exist
**Impact**: Code doesn't compile, frustration, time wasted debugging
**Example**: Calling `User.GetWalletById()` when method doesn't exist

#### **Pain Point #4: Pattern Violations** 🔴
**Problem**: AI ignores established patterns (Result<T>, StrongId<T>, CQRS)
**Impact**: Inconsistent codebase, technical debt, violates architecture decisions
**Example**: Throwing exceptions instead of returning Result<T>, using Guid instead of StrongId<T>

#### **Pain Point #5: Documentation Drift** 🔴
**Problem**: Code evolves but docs stay stale
**Impact**: Docs become unreliable, AI gets wrong information from outdated docs
**Example**: Service added to module but module doc not updated

#### **Pain Point #6: Checkpoint Fatigue** 🔴
**Problem**: Too many approval points slow down development
**Impact**: Workflow feels bureaucratic, developer exhaustion
**Goal**: 3-4 strategic checkpoints (not 21+)

---

## 🧠 CREATIVE BRAINSTORMING ANALYSIS

### **Technique 1: First Principles Thinking** 🔬

**Fundamental Truths About Brownfield AI Development**:
1. ✅ **Code already exists** → Discovery before creation is mandatory
2. ✅ **Libraries already provide features** → Check library first, code second
3. ✅ **Patterns already established** → Conform, don't invent
4. ✅ **Docs are source of truth** → Ground in `/Docs`, not assumptions
5. ✅ **Solo developer = limited time** → Every checkpoint must add value
6. ✅ **Documentation drift = death** → Docs must stay synced with code

**Core Insight**: Workflow must be **DOC-GROUNDED, DISCOVERY-FIRST, LIBRARY-AWARE, PATTERN-COMPLIANT, CHECKPOINT-EFFICIENT, DOC-MAINTAINING**

---

### **Technique 2: SCAMPER Analysis** 🔧

**S - Substitute**: Replace "5 separate agents" with "agents that auto-load docs"  
**C - Combine**: Combine "discovery + library + pattern" into ONE pre-flight  
**A - Adapt**: Adapt aviation pre-flight checklists → comprehensive validation before implementation  
**M - Modify**: Modify checkpoints to be batched (not scattered)  
**P - Put to other use**: Use docs as executable validation rules (not just reference)  
**E - Eliminate**: Eliminate coordination overhead between agents  
**R - Reverse**: Work backwards from docs (docs → code, not code → docs update)

**Core Insight**: **ONE pre-flight workflow + Doc-as-validation + Batched checkpoints + Continuous doc integration**

---

### **Technique 3: Six Thinking Hats** 🎩

**White Hat (Facts)**:
- 120+ docs, 15+ libraries, 6 ADRs, 2 modules
- Solo developer, full AI workflow, brownfield complexity

**Red Hat (Emotions)**:
- Frustration: AI invents methods
- Overwhelm: Too many checkpoints
- Confidence: When docs referenced
- Fear: Breaking existing code

**Black Hat (Risks)**:
- ⚠️ Too many agents = coordination overhead
- ⚠️ 7 workflows × 5 checkpoints = checkpoint fatigue (35 total!)
- ⚠️ Sequential waterfall = slow
- ⚠️ Docs updated "at end" = forgotten

**Yellow Hat (Benefits)**:
- ✅ Doc-grounded = single source of truth
- ✅ Pre-flight = early error detection
- ✅ Library-first = leverage power
- ✅ Batched checkpoints = efficiency

**Green Hat (Creativity)**:
- 💡 **"Doc Oracle"**: AI queries docs like database
- 💡 **"Confidence Scoring"**: AI self-rates, auto-escalates on doubt
- 💡 **"Diff Preview"**: See changes before they happen
- 💡 **"Learning Loop"**: Capture decisions to prevent repeat mistakes

**Blue Hat (Meta)**:
- Need ONE master workflow that orchestrates
- Checkpoints should be batched (3-4 total)
- Agent architecture: Specialized experts with mandatory doc loading
- Doc integration: Continuous (not end-of-process)

---

### **Key Innovations Identified**

1. **📖 Doc-Oracle Concept**: Docs as executable validation engine
2. **🔍 Pre-Flight Validation**: Catch issues BEFORE code generation
3. **📊 Confidence Scoring**: AI self-assesses and escalates uncertainty
4. **👀 Diff Preview**: See what will change BEFORE writing code
5. **🔗 Inline Doc References**: Code self-documents with doc links
6. **📚 Continuous Doc Integration**: Docs updated WITH code, not after
7. **🧠 Learning Loop**: Capture decisions for future reference
8. **✅ Batched Checkpoints**: 4 strategic approvals (not 35)
9. **🎯 Expert Agents**: Domain specialists with mandatory doc loading
10. **📋 Module-Specific Workflows**: Handle Identity vs Chat domain complexity

---

## 🏗️ FINAL ARCHITECTURE DESIGN

### **Module Identity**

```yaml
Module Code: axon
Module Name: Axon Development Orchestrator
Tagline: "Brownfield-aware AI development with library-first implementation"

Purpose:
  AI-driven development that:
  - GROUNDS in existing code (discovery before creation)
  - REUSES existing patterns (never reinvent)
  - LEVERAGES libraries correctly (out-of-box solutions first)
  - VALIDATES against docs (doc-as-validation)
  - MAINTAINS docs continuously (zero drift)
  - LEARNS from decisions (prevent repeat mistakes)

Module Type: Standard (5-10 agents, 5-10 workflows)
Domain: .NET Backend Development (Brownfield)
Target Audience: Solo AI-driven developers working on complex codebases
```

---

## 🦸 AGENT TEAM SPECIFICATION

### **Agent Team Structure**

**Total Agents**: 6  
**Team Type**: Specialized Experts with Domain Knowledge  
**Coordination**: Sequential workflow with batched checkpoints  
**Communication**: Via Story Orchestrator (hub-and-spoke)

---

### **Agent 1: Axon Story Orchestrator** 🎯

**Role**: Workflow conductor & Human-in-the-Loop manager  
**Personality**: Project manager who keeps everything flowing smoothly  
**Agent Type**: Module (orchestrator with commands)

**Core Responsibilities**:
- Parse stories & acceptance criteria
- Route tasks to specialist agents
- Manage checkpoints & human approvals
- Ensure workflow completion
- Track decisions & learning
- Module routing (Identity vs Chat vs generic)

**Mandatory Doc Loading** (auto-loads on activation):
```yaml
critical_docs:
  - Docs/PROCESS/story-template.md
  - Docs/PROCESS/bmad-workflow.md
  - Docs/ENGINEERING/guides/workflows/development-workflow.md
  - Docs/ENGINEERING/ai-context/module-boundaries-map.md
  - Docs/ENGINEERING/00-START-HERE.md
```

**Commands**:
- `*implement-story {story-id}` - Full story implementation workflow
- `*checkpoint {phase}` - Create human approval point
- `*delegate {agent} {task}` - Route to specialist agent
- `*status` - Show workflow progress & state
- `*route-workflow {feature}` - Determine which workflow to use
- `*capture-decision {story}` - Record learning for future

**Why Critical**: Central coordinator that ensures all pieces work together

---

### **Agent 2: Axon Doc Oracle** 📚

**Role**: Documentation intelligence & grounding specialist  
**Personality**: Librarian scholar who knows every doc by heart  
**Agent Type**: Expert (deep knowledge, consulting role)

**Core Responsibilities**:
- Load relevant docs based on task context
- Validate approaches against ADRs & patterns
- Detect doc drift (when code diverges from docs)
- Suggest doc updates when implementation changes
- Maintain doc-code alignment
- Query architecture decisions

**Mandatory Doc Loading** (auto-loads on activation):
```yaml
critical_docs:
  - Docs/README.md                                            # Master navigation
  - Docs/ENGINEERING/00-START-HERE.md                        # Entry point
  - Docs/ENGINEERING/guides/patterns/00-QUICK-REFERENCE.md   # HOT PATH
  - Docs/ENGINEERING/guides/architecture/adrs/00-INDEX.md    # All ADRs
  - Docs/Libraries/00-INDEX.md                               # Library index
  - Docs/ENGINEERING/ai-context/quick-reference-index.md

contextual_loading:  # Loads based on module/feature context
  identity_module:
    - Docs/ENGINEERING/modules/identity/00-INDEX.md
    - Docs/ENGINEERING/modules/identity/01-domain-model.md
    - Docs/ENGINEERING/modules/identity/03-authentication.md
    - Docs/ENGINEERING/modules/identity/06-database-schema.md
  
  chat_module:
    - Docs/ENGINEERING/modules/chat/00-INDEX.md
    - Docs/ENGINEERING/modules/chat/01-domain-model.md
    - Docs/ENGINEERING/modules/chat/03-messaging-flows.md
  
  patterns:
    - Docs/ENGINEERING/guides/patterns/cqrs.md
    - Docs/ENGINEERING/guides/patterns/domain-modeling.md
    - Docs/ENGINEERING/guides/patterns/error-handling.md
    - Docs/ENGINEERING/guides/patterns/validation.md
  
  infrastructure:
    - Docs/ENGINEERING/infrastructure/persistence/ef-core-configuration.md
    - Docs/ENGINEERING/infrastructure/resilience/circuit-breakers.md
    - Docs/ENGINEERING/infrastructure/caching/strategy.md
  
  libraries:
    - Docs/Libraries/{library}/IMPLEMENTATION_GUIDE.md
```

**Commands**:
- `*load-context {module|pattern|library}` - Load relevant documentation
- `*validate-against-docs {approach}` - Check compliance with docs
- `*detect-drift {implementation}` - Find doc inconsistencies
- `*suggest-updates {code-changes}` - Recommend doc updates
- `*query-adr {topic}` - Find relevant architecture decisions
- `*compliance-score {design}` - Rate pattern adherence

**Why Critical**: **SOURCE OF TRUTH GUARDIAN** - stops pattern violations, prevents doc drift

---

### **Agent 3: Axon Archaeologist** 🔍

**Role**: Codebase discovery & reuse specialist  
**Personality**: Detective who finds existing treasure in the codebase  
**Agent Type**: Expert (search & analysis specialist)

**Core Responsibilities**:
- Search for existing implementations before creating new
- Map available APIs (methods, properties, classes)
- Find reusable patterns in codebase
- Prevent reinvention of the wheel
- Document what exists vs. what needs building
- Locate similar features for reference

**Mandatory Doc Loading** (auto-loads on activation):
```yaml
critical_docs:
  - Docs/ENGINEERING/guides/codebase/source-tree.md
  - Docs/ENGINEERING/guides/codebase/project-conventions.md
  - Docs/ENGINEERING/guides/patterns/00-QUICK-REFERENCE.md

search_patterns:  # Knows where to look in codebase
  domain_logic: "src/Modules/{module}/Domain/**/*.cs"
  aggregates: "src/Modules/{module}/Domain/Aggregates/**/*.cs"
  services: "src/Modules/{module}/Application/Services/**/*.cs"
  commands: "src/Modules/{module}/Application/Commands/**/*.cs"
  queries: "src/Modules/{module}/Application/Queries/**/*.cs"
  repositories: "src/Modules/{module}/Infrastructure/Repositories/**/*.cs"
  endpoints: "src/Api/Modules/{module}/**/*.cs"
  tests: "tests/Modules/{module}/**/*.cs"
  building_blocks: "src/BuildingBlocks/Core/**/*.cs"
```

**Commands**:
- `*search-existing {feature}` - Find existing implementations
- `*map-apis {class}` - List available methods/properties/constructors
- `*find-pattern {pattern-type}` - Locate pattern usage examples
- `*discover-similar {description}` - Find comparable features
- `*reuse-report {requirement}` - Analysis: what to reuse vs build
- `*find-references {method|class}` - Where is this used?

**Why Critical**: **STOPS AI HALLUCINATION** - ensures AI uses what exists, doesn't invent phantom methods

---

### **Agent 4: Axon Library Sage** 🛠️

**Role**: Library-first implementation specialist  
**Personality**: Wise craftsperson who knows every tool in the toolbox  
**Agent Type**: Expert (library knowledge specialist)

**Core Responsibilities**:
- Check library capabilities BEFORE writing manual code
- Reference library implementation guides
- Suggest out-of-box solutions from libraries
- Prevent manual reimplementation of library features
- Ensure correct library usage patterns
- Recommend library vs manual code decisions

**Mandatory Doc Loading** (auto-loads on activation):
```yaml
critical_docs:
  - Docs/Libraries/00-INDEX.md  # Library hub

always_loaded_libraries:  # Core tech stack (always in context)
  - Docs/Libraries/MediatR/IMPLEMENTATION_GUIDE.md
  - Docs/Libraries/FastEndpoints/IMPLEMENTATION_GUIDE.md
  - Docs/Libraries/FluentValidation/IMPLEMENTATION_GUIDE.md
  - Docs/Libraries/Polly/IMPLEMENTATION_GUIDE.md
  - Docs/Libraries/NUnit/IMPLEMENTATION_GUIDE.md
  - Docs/Libraries/Shouldly/USAGE_GUIDE.md

contextual_libraries:  # Loads based on feature needs
  identity_auth:
    - Docs/Libraries/dynamic_auth/IMPLEMENTATION_GUIDE.md
    - Docs/Libraries/dynamic_auth/authentication-protocols.md
  
  observability:
    - Docs/Libraries/OpenTelemetry/IMPLEMENTATION_GUIDE.md
    - Docs/Libraries/SystemDiagnosticsActivity/IMPLEMENTATION_GUIDE.md
  
  testing_mocks:
    - Docs/Libraries/Moq/IMPLEMENTATION_GUIDE.md
  
  ai_integration:
    - Docs/Libraries/OpenAI/IMPLEMENTATION_GUIDE.md
    - Docs/Libraries/ModelContextProtocol/IMPLEMENTATION_GUIDE.md
```

**Commands**:
- `*check-library {requirement}` - Does library already solve this?
- `*suggest-approach {feature}` - Library-based vs manual comparison
- `*show-pattern {library}` - How to use library correctly in Axon
- `*validate-usage {code}` - Is library being used correctly?
- `*library-alternatives {problem}` - Which library should we use?
- `*library-capabilities {library}` - What can this library do?

**Why Critical**: **STOPS MANUAL REINVENTION** - leverages library power, prevents wheel reinvention

---

### **Agent 5: Axon Implementation Surgeon** ⚙️

**Role**: Precise code generation & modification expert  
**Personality**: Surgical specialist who makes minimal precise changes  
**Agent Type**: Expert (execution specialist)

**Core Responsibilities**:
- Generate code ONLY after validation from other agents
- Extend existing code (never replace unnecessarily)
- Follow established patterns exactly
- Apply library solutions correctly
- Make surgical, minimal changes
- Brownfield-safe modifications

**Mandatory Doc Loading** (auto-loads on activation):
```yaml
critical_docs:
  - Docs/ENGINEERING/guides/codebase/coding-standards.md
  - Docs/ENGINEERING/guides/patterns/00-QUICK-REFERENCE.md
  - Docs/ENGINEERING/guides/patterns/cqrs.md
  - Docs/ENGINEERING/guides/patterns/domain-modeling.md
  - Docs/ENGINEERING/guides/patterns/error-handling.md

context_sensitive:  # Loaded based on active module
  identity:
    - Docs/ENGINEERING/modules/identity/01-domain-model.md
    - Docs/ENGINEERING/modules/identity/03-authentication.md
    - Docs/ENGINEERING/modules/identity/06-database-schema.md
  
  chat:
    - Docs/ENGINEERING/modules/chat/01-domain-model.md
    - Docs/ENGINEERING/modules/chat/03-messaging-flows.md
  
  infrastructure:
    - Docs/ENGINEERING/infrastructure/persistence/ef-core-configuration.md
    - Docs/ENGINEERING/infrastructure/resilience/retry-policies.md
```

**Commands**:
- `*implement {spec}` - Generate new code from specification
- `*extend {class} {feature}` - Modify existing class (surgical)
- `*apply-pattern {pattern} {code}` - Refactor to follow pattern
- `*diff-preview {changes}` - Show what will change BEFORE writing
- `*inline-docs {code}` - Add doc reference comments to code
- `*integrate-library {library} {feature}` - Add library-based solution

**Why Critical**: **SAFE EXECUTOR** - only generates code after full validation, minimal changes

---

### **Agent 6: Axon Quality Guardian** ✅

**Role**: Testing, validation & final safety check specialist  
**Personality**: Quality assurance expert who ensures excellence  
**Agent Type**: Expert (validation & testing specialist)

**Core Responsibilities**:
- Generate comprehensive test suites (unit + integration)
- Validate implementation against acceptance criteria
- Run build & test validation
- Ensure pattern compliance in final code
- Capture learning & decisions
- Update docs when implementation complete

**Mandatory Doc Loading** (auto-loads on activation):
```yaml
critical_docs:
  - Docs/ENGINEERING/testing/00-INDEX.md
  - Docs/ENGINEERING/testing/testing-philosophy.md
  - Docs/ENGINEERING/testing/unit-testing-guide.md
  - Docs/ENGINEERING/testing/integration-testing-guide.md
  - Docs/Libraries/NUnit/IMPLEMENTATION_GUIDE.md
  - Docs/Libraries/Shouldly/USAGE_GUIDE.md
  - Docs/Libraries/Moq/IMPLEMENTATION_GUIDE.md

pattern_validation:
  - Docs/ENGINEERING/guides/patterns/00-QUICK-REFERENCE.md
  - Docs/ENGINEERING/guides/architecture/adrs/00-INDEX.md

context_testing:
  concurrency:
    - Docs/ENGINEERING/testing/concurrency-testing-guide.md
  api:
    - Docs/ENGINEERING/testing/api-testing-guide.md
  performance:
    - Docs/ENGINEERING/testing/performance-testing-guide.md
```

**Commands**:
- `*generate-tests {implementation}` - Create comprehensive test suite
- `*validate-implementation {spec}` - Check against requirements
- `*run-build` - Execute `dotnet build`
- `*run-tests` - Execute `dotnet test`
- `*compliance-check {code}` - Final pattern adherence verification
- `*capture-decision {story}` - Record learning for knowledge base
- `*update-docs {changes}` - Apply doc updates identified

**Why Critical**: **FINAL SAFETY NET** - comprehensive validation, learning capture, doc maintenance

---

## 📋 WORKFLOW CATALOG

### **Core Workflows (Universal)** - 4 workflows

1. **`story-to-production`** - Master orchestration workflow (most common)
2. **`pre-flight-validation`** - Discovery + Library + Pattern check (reusable)
3. **`safe-refactor`** - Brownfield-safe code improvements
4. **`doc-sync`** - Documentation maintenance & alignment

### **Identity Module Workflows** - 3 specialized workflows

5. **`identity-authentication-flow`** - JWT, wallet signatures, Dynamic.xyz
6. **`identity-wallet-integration`** - Wallet linking, verification, multi-chain
7. **`identity-principal-management`** - Principal CRUD, credentials, resolution

### **Chat Module Workflows** - 3 specialized workflows

8. **`chat-conversation-flow`** - Conversations, messages, business rules
9. **`chat-ai-integration`** - Claude API, MCP servers, streaming
10. **`chat-aggregate-design`** - Owned entities, EF Core patterns

**Total**: 10 workflows (4 core + 6 module-specific)

---

## 🔄 CORE WORKFLOW DETAILS

### **Workflow 1: `story-to-production`** 🚀

**Purpose**: Master orchestration workflow for complete story implementation  
**When to Use**: Default workflow for any new feature/story  
**Complexity**: High (orchestrates all other components)

**Agents Involved**: All 6 agents

**Workflow Phases**:

#### **Phase 1: Story Understanding & Doc-Grounding** 📖
**Agents**: Story Orchestrator + Doc Oracle

**Steps**:
1. **[Orchestrator]** Parse story → Extract module, features, acceptance criteria
2. **[Orchestrator]** Analyze complexity → Route to generic or module-specific workflow
3. **[Orchestrator]** Delegate to **Doc Oracle** → Load relevant context
4. **[Doc Oracle]** Load mandatory docs + contextual docs (module, patterns, libraries)
5. **[Doc Oracle]** Self-assess confidence: HIGH | MEDIUM | LOW
6. **[Doc Oracle]** If LOW confidence → Stop and ask clarifying questions
7. **[Doc Oracle]** Output: **Doc Context Package** + compliance baseline

**Output**:
```markdown
### Story Understanding Report

**Story ID**: AX-123
**Module**: Identity
**Feature**: Auto-revoke old credentials when new credential added
**Complexity**: Medium

**Loaded Documentation**:
- ✅ Identity module domain model
- ✅ Authentication flows
- ✅ Result<T> pattern guide
- ✅ CQRS guide
- ✅ Dynamic.xyz integration docs

**AI Confidence**: HIGH
**Ready to Proceed**: Yes
```

**✅ CHECKPOINT 1: Story Understanding Approval**
**Human Reviews**:
- Story interpretation correct?
- Loaded docs appropriate?
- Confidence score acceptable?

**Options**: Approve | Clarify | Add more docs | Reject interpretation

---

#### **Phase 2: Pre-Flight Validation** 🔍
**Agents**: Archaeologist + Library Sage + Doc Oracle (parallel execution)

**[Archaeologist] Codebase Discovery**:
```yaml
Actions:
  - Search for existing credential management code
  - Map AxonPrincipal aggregate methods
  - Find existing revocation patterns
  - Locate similar auto-revocation logic

Output:
  Discovery Report:
    Existing:
      - AxonPrincipal.AddCredential() exists
      - IdentityCredential entity has Revoked property
      - No auto-revocation logic found
    Needs Building:
      - Auto-revocation on new credential
      - Cascade revocation logic
    Similar Patterns:
      - WalletOwnership verification pattern (reference)
```

**[Library Sage] Library Capability Check**:
```yaml
Actions:
  - Check MediatR pipeline behaviors for cross-cutting logic
  - Check if Dynamic.xyz SDK has revocation features
  - Review FluentValidation for credential validation
  
Output:
  Library Strategy:
    Library Solutions:
      - ✅ Use MediatR pipeline behavior (not manual trigger)
      - ✅ Use FluentValidation for credential uniqueness
      - ❌ No library for auto-revocation (domain logic)
    Manual Implementation:
      - Auto-revocation logic in aggregate
```

**[Doc Oracle] Pattern Compliance Validation**:
```yaml
Actions:
  - Load Result<T> pattern docs
  - Load CQRS command patterns
  - Load aggregate invariant patterns
  - Check module boundary rules

Output:
  Compliance Report:
    Pattern Adherence:
      - ✅ Result<T, Error>: Must return Result
      - ✅ StrongId<T>: Use CredentialId, PrincipalId
      - ✅ CQRS: Implement as command (not query)
      - ✅ Aggregate Invariant: Logic belongs in AxonPrincipal
      - ⚠️ Event Raising: Must raise CredentialRevokedEvent
    
    Issues to Address:
      - Must raise domain event when revoking
      - Consider concurrency (RowVersion check)
```

**Combined Pre-Flight Package**:
```markdown
### Pre-Flight Validation Results

**Discovery** (Archaeologist):
- ✅ Can extend: AxonPrincipal.AddCredential()
- ❌ Must build: Auto-revocation logic
- 📚 Reference: WalletOwnership verification pattern

**Library Strategy** (Library Sage):
- ✅ Use MediatR for command handling
- ✅ Use FluentValidation for validation
- ❌ Manual: Auto-revocation domain logic

**Compliance** (Doc Oracle):
- ✅ Result<T>: 100%
- ✅ StrongId<T>: 100%
- ✅ CQRS: 100% (command pattern)
- ⚠️ Domain Events: Must add CredentialRevokedEvent

**Implementation Plan**:
1. Extend AxonPrincipal.AddCredential() (reuse)
2. Add private RevokeOldCredentials() method (new)
3. Raise CredentialRevokedEvent (domain event)
4. Use Result<T> for error handling
5. Add concurrency check (RowVersion)

**Estimated Complexity**: Medium
**Risk**: Low (well-defined aggregate change)
**Estimated LOC**: ~50 lines
```

**✅ CHECKPOINT 2: Pre-Flight Approval**
**Human Reviews**:
- Discovery report (what exists vs needs building)
- Library strategy (correct tool usage)
- Compliance report (pattern adherence)
- Implementation plan (overall approach)

**Options**: Approve | Adjust plan | Add context | Reject approach

---

#### **Phase 3: Grounded Implementation** ⚙️
**Agent**: Implementation Surgeon

**Steps**:
1. **Receive**: Validated pre-flight package from Phase 2
2. **Load**: Module-specific coding standards + patterns
3. **Preview**: Generate implementation diff summary
4. **Show Diff Preview**:
```markdown
### Implementation Diff Preview

**Files to Modify**:
- src/Modules/Identity/Domain/Aggregates/AxonPrincipal/AxonPrincipal.cs
  - Extend AddCredential() method
  - Add private RevokeOldCredentials() helper

**Files to Add**:
- src/Modules/Identity/Domain/Events/CredentialRevokedEvent.cs

**No Files Deleted**

**Libraries Used**:
- MediatR (command handling)
- Result<T> pattern (BuildingBlocks)
- StrongId<T> (CredentialId)

**Estimated LOC**: ~50 lines
**Risk**: Low (single aggregate modification)
```

5. **Generate Code** (after human approval):
```csharp
// src/Modules/Identity/Domain/Aggregates/AxonPrincipal/AxonPrincipal.cs

// Pattern: Result<T, Error> - see Docs/ENGINEERING/guides/patterns/domain-modeling.md
// Reusing: AxonPrincipal.AddCredential pattern - see line 145
// Library: N/A (domain logic)

public Result<IdentityCredential, Error> AddCredential(
    ProviderType provider,
    string providerAccountId,
    string accessToken)
{
    // Validate uniqueness
    if (_credentials.Any(c => 
        c.Provider == provider && 
        c.ProviderAccountId == providerAccountId && 
        !c.IsRevoked))
    {
        return Result.Failure<IdentityCredential, Error>(
            Error.BusinessRule("Credential already exists for this provider"));
    }

    // Auto-revoke old credentials for same provider
    var revokeResult = RevokeOldCredentials(provider);
    if (revokeResult.IsFailure)
    {
        return Result.Failure<IdentityCredential, Error>(revokeResult.Error);
    }

    // Add new credential
    var credential = new IdentityCredential(
        id: CredentialId.New(),
        principalId: Id,
        provider: provider,
        providerAccountId: providerAccountId,
        accessToken: accessToken);

    _credentials.Add(credential);

    // Raise domain event
    RaiseDomainEvent(new CredentialAddedEvent(Id, credential.Id));

    return Result.Success<IdentityCredential, Error>(credential);
}

// Pattern: Private helper - see aggregate patterns doc
private Result<Unit, Error> RevokeOldCredentials(ProviderType provider)
{
    var oldCredentials = _credentials
        .Where(c => c.Provider == provider && !c.IsRevoked)
        .ToList();

    foreach (var credential in oldCredentials)
    {
        credential.Revoke();
        RaiseDomainEvent(new CredentialRevokedEvent(Id, credential.Id));
    }

    return Result.Success<Unit, Error>(Unit.Value);
}
```

6. **Inline Documentation**: Add doc reference comments (see code above)

7. **Doc Drift Detection**: Check if docs need updates
```markdown
### Doc Update Suggestions

**Docs Needing Updates**:
- ⚠️ Docs/ENGINEERING/modules/identity/01-domain-model.md
  - Section: "AxonPrincipal Methods"
  - Change: Document auto-revocation behavior in AddCredential
  
- ⚠️ Docs/ENGINEERING/modules/identity/01-domain-model.md
  - Section: "Domain Events"
  - Change: Add CredentialRevokedEvent to event list
```

**✅ CHECKPOINT 3: Implementation Review**
**Human Reviews**:
- Generated code quality
- Implementation diff (what changed)
- Inline doc references
- Pattern compliance (Result<T>, StrongId<T>, events)
- Doc update suggestions

**Options**: Approve | Refactor code | Update docs now | Reject

---

#### **Phase 4: Validation & Learning** ✅
**Agent**: Quality Guardian

**Steps**:

1. **Generate Test Suite**:
```csharp
// tests/Modules/Identity/Domain/AxonPrincipalTests/AddCredentialTests.cs

[TestFixture]
public class AddCredentialWithAutoRevocationTests
{
    [Test]
    public void AddCredential_WhenSameProviderExists_ShouldRevokeOldCredential()
    {
        // Arrange
        var principal = AxonPrincipal.Create(/* ... */);
        var oldCredential = principal.AddCredential(
            ProviderType.Dynamic,
            "old-account",
            "old-token").Value;

        // Act
        var result = principal.AddCredential(
            ProviderType.Dynamic,
            "new-account",
            "new-token");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        oldCredential.IsRevoked.ShouldBeTrue();
    }

    [Test]
    public void AddCredential_WhenRevokingOld_ShouldRaiseCredentialRevokedEvent()
    {
        // Arrange
        var principal = AxonPrincipal.Create(/* ... */);
        principal.AddCredential(ProviderType.Dynamic, "old", "token");
        principal.ClearDomainEvents();

        // Act
        principal.AddCredential(ProviderType.Dynamic, "new", "token2");

        // Assert
        var events = principal.GetDomainEvents();
        events.ShouldContain(e => e is CredentialRevokedEvent);
    }

    // ... more tests: concurrency, edge cases, error paths
}
```

2. **Run Build**: `dotnet build` → Must pass
3. **Run Tests**: `dotnet test` → Must pass
4. **Final Compliance Check**: Verify Result<T>, StrongId<T>, CQRS, domain events

5. **Apply Doc Updates**:
Update `Docs/ENGINEERING/modules/identity/01-domain-model.md` with auto-revocation behavior

6. **Capture Learning Decision**:
```yaml
# Docs/PROCESS/active-stories/AX-123/decisions.yaml

decision_id: AX-123-auto-revoke
story: "Auto-revoke old credentials when new credential added"
date: 2025-09-29
module: Identity

approach:
  what: "Extended AxonPrincipal.AddCredential with auto-revocation"
  why: "Ensures one active credential per provider, domain invariant"
  how: "Private RevokeOldCredentials() helper, domain events raised"

patterns_used:
  - Result<T, Error>
  - Domain Events (CredentialRevokedEvent)
  - Aggregate Invariants
  - StrongId<CredentialId>

libraries_used:
  - None (pure domain logic)

alternatives_considered:
  - Application service for revocation (rejected: domain logic)
  - Manual revocation by user (rejected: automatic is safer)

lessons_learned:
  - Auto-revocation prevents credential proliferation
  - Domain events critical for downstream processing
  - Aggregate handles its own invariants

doc_updates:
  - Docs/ENGINEERING/modules/identity/01-domain-model.md

references:
  - Docs/ENGINEERING/guides/patterns/domain-modeling.md
  - Docs/ENGINEERING/modules/identity/01-domain-model.md
```

**✅ FINAL CHECKPOINT: Commit Approval**
**Human Reviews**:
- ✅ All tests passing
- ✅ Build successful
- ✅ Docs updated
- ✅ Decision captured
- ✅ Ready to commit

**Options**: Commit & push | Add more tests | Add more docs | Refactor

---

### **Workflow Summary: `story-to-production`**

```
Story Input
    ↓
[Phase 1: Doc-Grounding] 📖 (Orchestrator + Doc Oracle)
    ↓ (load context, confidence check)
[✅ CHECKPOINT 1: Understanding] (1 min review)
    ↓
[Phase 2: Pre-Flight] 🔍 (Archaeologist + Library Sage + Doc Oracle - PARALLEL)
    ↓ (discover + library + pattern validation)
[✅ CHECKPOINT 2: Pre-Flight] (3-5 min review)
    ↓
[Phase 3: Implementation] ⚙️ (Implementation Surgeon)
    ↓ (code gen + doc drift detection)
[✅ CHECKPOINT 3: Code Review] (5-10 min review)
    ↓
[Phase 4: Validation] ✅ (Quality Guardian)
    ↓ (tests + doc updates + learning capture)
[✅ FINAL CHECKPOINT: Commit] (2 min review)
    ↓
Production ✅ + Docs Synced ✅ + Learning Captured ✅
```

**Total Checkpoints**: 4 (strategic, batched)  
**Total Time**: 30-60 minutes  
**Doc Drift**: ZERO (updated inline)  
**AI Hallucination**: Minimized (doc-grounded + discovery)  
**Pattern Violations**: Caught early (pre-flight)  
**Learning**: Captured (knowledge base)

---

### **Workflow 2: `pre-flight-validation`** 🔍

**Purpose**: Comprehensive validation BEFORE code generation (reusable component)  
**When to Use**: Called by other workflows, or standalone for design validation  
**Complexity**: Medium

**Agents Involved**: Archaeologist, Library Sage, Doc Oracle

**Note**: This is the **CORE INNOVATION** - discovery + library + pattern check in ONE workflow

**Steps**: See Phase 2 in `story-to-production` workflow above

**Output**: Pre-flight validation package (discovery + library + compliance + plan)

---

### **Workflow 3: `safe-refactor`** 🔧

**Purpose**: Improve existing code without breaking anything  
**When to Use**: Technical debt, code smells, pattern upgrades  
**Complexity**: High (brownfield danger zone)

**Agents Involved**: All 6 agents

**Key Differences from `story-to-production`**:
- **Extra Discovery**: Map ALL usages of code to refactor
- **Impact Analysis**: What breaks if we change X?
- **Backward Compatibility**: Design refactoring with compatibility
- **Extra Validation**: All existing tests must still pass
- **Rollback Plan**: How to undo if things go wrong

**Additional Checkpoint**: "Refactor Plan Approval" (before implementation)

---

### **Workflow 4: `doc-sync`** 📚

**Purpose**: Maintain documentation alignment with code  
**When to Use**: Scheduled maintenance, after major changes  
**Complexity**: Low

**Agents Involved**: Doc Oracle, Archaeologist, Quality Guardian

**Steps**:
1. **[Archaeologist]** Scan codebase for recent changes
2. **[Doc Oracle]** Identify docs potentially affected
3. **[Doc Oracle]** Detect drift (code vs docs discrepancies)
4. **[Doc Oracle]** Suggest updates
5. **[Quality Guardian]** Apply updates
6. **[Quality Guardian]** Validate docs still accurate

**No Checkpoints**: Can run autonomously or with final approval

---

## 📱 MODULE-SPECIFIC WORKFLOWS

### **Identity Module Workflows** 🔐

---

### **Workflow 5: `identity-authentication-flow`** 🔑

**Purpose**: Implement authentication features with Dynamic.xyz, JWT, wallet signatures  
**When to Use**: Auth methods, JWT validation, wallet signatures, Dynamic.xyz integration  
**Complexity**: High (external dependencies)

**Module-Specific Doc Loading**:
```yaml
mandatory_docs:
  - Docs/ENGINEERING/modules/identity/00-INDEX.md
  - Docs/ENGINEERING/modules/identity/01-domain-model.md
  - Docs/ENGINEERING/modules/identity/03-authentication.md
  - Docs/Libraries/dynamic_auth/IMPLEMENTATION_GUIDE.md
  - Docs/Libraries/dynamic_auth/authentication-protocols.md
  - Docs/ENGINEERING/integrations/dynamic-xyz/authentication-flow.md
  - Docs/ENGINEERING/integrations/dynamic-xyz/api-reference.md
  - Docs/Libraries/Polly/IMPLEMENTATION_GUIDE.md  # For Dynamic API resilience
```

**Identity-Specific Challenges**:
- JWT validation (JWKS, Dynamic.xyz specifics)
- Wallet signature verification (Ed25519 for Solana)
- Multi-provider credential management
- Principal resolution (deterministic)
- Token refresh flows

**Workflow Phases** (same structure as `story-to-production` but with Identity context):

**Phase 1: Authentication Context Loading**
- Doc Oracle loads Identity + Dynamic.xyz docs
- Validates against authentication patterns
- Loads JWT, wallet signature verification patterns

**Phase 2: Identity-Specific Discovery**
- Archaeologist searches:
  - `PrincipalResolutionService`
  - `WalletVerificationService`
  - `JwksService`, `DynamicAuthService`
  - `Ed25519SignatureVerifier`
  - Principal aggregate auth methods

**Phase 3: Authentication Library Strategy**
- Library Sage checks:
  - Dynamic.xyz SDK
  - NSec.Cryptography (Ed25519)
  - JWT libraries
  - Polly (resilience for Dynamic API)

**Phase 4: Identity Pattern Validation**
- Doc Oracle validates:
  - Principal aggregate invariants
  - Owned entity patterns (IdentityCredential)
  - Concurrency handling (RowVersion)
  - Multi-chain defaults (PrincipalChainDefault)

**Output**: Auth feature with Dynamic.xyz + wallet verification + pattern compliance

---

### **Workflow 6: `identity-wallet-integration`** 🔗

**Purpose**: Implement wallet linking, verification, multi-chain management  
**When to Use**: New blockchain support, signature challenges, chain defaults, ownership proofs  
**Complexity**: High (crypto + concurrency)

**Module-Specific Doc Loading**:
```yaml
mandatory_docs:
  - Docs/ENGINEERING/modules/identity/00-INDEX.md
  - Docs/ENGINEERING/modules/identity/01-domain-model.md
  - Docs/ENGINEERING/modules/identity/06-database-schema.md
  - Docs/Libraries/dynamic_auth/csharp-integration-guide.md
  - Docs/ENGINEERING/integrations/helius/integration-overview.md
```

**Identity-Specific Challenges**:
- Owned entity concurrency (WalletOwnership as owned entity)
- Multi-principal wallet resolution (many-to-many)
- Chain-specific signature verification
- Auto-revocation on credential changes
- Composite keys in EF Core

**Specialized Steps**:

**Phase 2: Wallet-Specific Discovery**
- `AxonPrincipal.LinkWallet()` implementation
- `WalletOwnership` owned entity patterns
- `PrincipalChainDefault` management
- Existing signature verifiers (Ed25519, etc.)

**Phase 3: Crypto Library Check**
- NSec.Cryptography (Ed25519 for Solana)
- Helius SDK (Solana RPC)
- Dynamic.xyz wallet APIs
- Chain-specific libraries

**Phase 4: Owned Entity Pattern Validation**
- Owned entity configuration (OwnsMany)
- Concurrency at aggregate root only
- No separate DbSet for owned entities
- Composite key patterns

**Output**: Wallet features with owned entity compliance + crypto verification

---

### **Workflow 7: `identity-principal-management`** 👤

**Purpose**: Principal creation, credential management, identity resolution  
**When to Use**: New credential types, principal resolution, lifecycle, auto-revocation  
**Complexity**: Medium (aggregate logic)

**Module-Specific Doc Loading**:
```yaml
mandatory_docs:
  - Docs/ENGINEERING/modules/identity/00-INDEX.md
  - Docs/ENGINEERING/modules/identity/01-domain-model.md
  - Docs/ENGINEERING/modules/identity/06-database-schema.md
```

**Specialized Validation**:

**Phase 4: Aggregate Invariant Validation**
- Principal aggregate invariants (unique credentials)
- Deterministic principal resolution
- Credential lifecycle (active → revoked)
- Domain event raising patterns

**Identity-Specific Rules**:
- One active credential per provider per principal
- Auto-revoke old credentials on new
- Principal resolution is deterministic
- Wallets can belong to multiple principals

**Output**: Principal management with aggregate patterns + invariants

---

### **Chat Module Workflows** 💬

---

### **Workflow 8: `chat-conversation-flow`** 💭

**Purpose**: Implement conversation and message features with business rules  
**When to Use**: Conversation management, message append, lifecycle, limits/validation  
**Complexity**: Medium (owned entities + business rules)

**Module-Specific Doc Loading**:
```yaml
mandatory_docs:
  - Docs/ENGINEERING/modules/chat/00-INDEX.md
  - Docs/ENGINEERING/modules/chat/01-domain-model.md
  - Docs/ENGINEERING/modules/chat/03-messaging-flows.md
  - Docs/ENGINEERING/modules/chat/05-api-contracts.md
```

**Specialized Steps**:

**Phase 2: Chat-Specific Discovery**
- `Conversation.AppendUserMessage()` implementation
- `Conversation.AppendAssistantMessage()` patterns
- Message ordering logic
- Turn-taking enforcement
- Business rules (20+ rules in `Domain/Rules/`)

**Phase 4: Business Rule Validation**
- Message limits (max 100 per conversation)
- Turn-taking (user → assistant → user)
- Content validation rules
- Conversation state machine (Active → Completed)

**Chat-Specific Rules**:
- Messages are owned entities (no separate DbSet)
- Cannot append user if last is user
- Cannot append assistant if last is assistant
- Message ordering guarantees

**Output**: Conversation features with business rule compliance

---

### **Workflow 9: `chat-ai-integration`** 🤖

**Purpose**: Claude API integration with MCP server support  
**When to Use**: AI processing, MCP servers, streaming responses, AI error handling  
**Complexity**: High (external AI API + async)

**Module-Specific Doc Loading**:
```yaml
mandatory_docs:
  - Docs/ENGINEERING/modules/chat/00-INDEX.md
  - Docs/ENGINEERING/modules/chat/03-messaging-flows.md
  - Docs/Libraries/OpenAI/IMPLEMENTATION_GUIDE.md
  - Docs/Libraries/ModelContextProtocol/IMPLEMENTATION_GUIDE.md
  - Docs/Libraries/Polly/IMPLEMENTATION_GUIDE.md
```

**Specialized Steps**:

**Phase 3: AI Library Strategy**
- Anthropic SDK capabilities
- MCP server integration patterns
- Polly retry policies (API failures)
- OpenTelemetry tracing (AI calls)

**Phase 4: AI Integration Patterns**
- `AiProcessingService` patterns
- `McpServerResolutionService` config
- Streaming response handling (SSE)
- Error handling (API failures, rate limits)

**AI-Specific Challenges**:
- Async AI processing
- Streaming responses (Server-Sent Events)
- Token limit handling
- Context window management

**Output**: AI integration with resilience + MCP support

---

### **Workflow 10: `chat-aggregate-design`** 🏗️

**Purpose**: Implement owned entity patterns for messages  
**When to Use**: Message entity changes, new owned entities, aggregate boundaries, EF Core config  
**Complexity**: Medium (EF Core owned entities)

**Module-Specific Doc Loading**:
```yaml
mandatory_docs:
  - Docs/ENGINEERING/modules/chat/00-INDEX.md
  - Docs/ENGINEERING/modules/chat/01-domain-model.md
  - Docs/ENGINEERING/infrastructure/persistence/ef-core-configuration.md
  - Docs/ENGINEERING/infrastructure/persistence/concurrency-handling.md
```

**Specialized Steps**:

**Phase 4: Owned Entity Pattern Validation**
- OwnsMany configuration (EF Core)
- No separate DbSet for Message
- Composite key: (ConversationId, MessageId)
- Concurrency at aggregate root only
- No navigation properties on owned entities

**Chat-Specific EF Core Patterns**:
```csharp
// Correct: Owned entity configuration
builder.OwnsMany(c => c.Messages, mb =>
{
    mb.WithOwner().HasForeignKey(nameof(Message.ConversationId));
    mb.Property<int>("Id").ValueGeneratedOnAdd();
    mb.HasKey(nameof(Message.ConversationId), "Id");
});

// Incorrect: Separate DbSet
// ❌ public DbSet<Message> Messages { get; set; }
```

**Output**: Aggregate design with owned entity compliance

---

## 🎯 WORKFLOW ROUTING LOGIC

### **Story Orchestrator Decision Tree**:

```yaml
Story Input → Analyze module context

Module Detection:
  Identity Module:
    Authentication feature? → identity-authentication-flow
    Wallet feature? → identity-wallet-integration
    Principal/credential? → identity-principal-management
    Other? → story-to-production (generic)

  Chat Module:
    Conversation/message? → chat-conversation-flow
    AI integration? → chat-ai-integration
    Aggregate/entity design? → chat-aggregate-design
    Other? → story-to-production (generic)

  Cross-cutting:
    → story-to-production (generic)

Task Type Detection:
  Refactoring? → safe-refactor
  Doc maintenance? → doc-sync
  New feature? → story-to-production or module-specific
```

---

## 📊 IMPLEMENTATION ROADMAP

### **Phase 1: Module Foundation** (Week 1)

**Goal**: Create Axon module structure with config

**Tasks**:
1. Create `bmad/axon/` directory structure
2. Create `bmad/axon/config.yaml`
3. Create `bmad/axon/README.md`
4. Define module metadata

**Deliverables**:
- Module skeleton
- Configuration file
- Basic documentation

---

### **Phase 2: Core Agents** (Week 2)

**Goal**: Implement 6 core agents with doc loading

**Tasks**:
1. Create `bmad/axon/agents/axon-story-orchestrator.md`
2. Create `bmad/axon/agents/axon-doc-oracle.md`
3. Create `bmad/axon/agents/axon-archaeologist.md`
4. Create `bmad/axon/agents/axon-library-sage.md`
5. Create `bmad/axon/agents/axon-implementation-surgeon.md`
6. Create `bmad/axon/agents/axon-quality-guardian.md`

**Each Agent Includes**:
- Agent XML structure (BMAD Core compliant)
- Mandatory doc loading config
- Command definitions
- Critical actions
- Communication style

**Deliverables**:
- 6 functional agent definitions
- Doc loading configs per agent
- Command interfaces

---

### **Phase 3: Core Workflows** (Week 3)

**Goal**: Implement 4 universal workflows

**Tasks**:
1. Create `bmad/axon/workflows/story-to-production/`
   - workflow.yaml
   - instructions.md
   - checklist.md
   - README.md

2. Create `bmad/axon/workflows/pre-flight-validation/`
3. Create `bmad/axon/workflows/safe-refactor/`
4. Create `bmad/axon/workflows/doc-sync/`

**Deliverables**:
- 4 complete workflows
- YAML configurations
- Instruction documents
- Validation checklists

---

### **Phase 4: Identity Module Workflows** (Week 4)

**Goal**: Implement Identity-specific workflows

**Tasks**:
1. Create `bmad/axon/workflows/identity-authentication-flow/`
2. Create `bmad/axon/workflows/identity-wallet-integration/`
3. Create `bmad/axon/workflows/identity-principal-management/`

**Deliverables**:
- 3 Identity workflows
- Module-specific doc loading configs
- Identity pattern validation rules

---

### **Phase 5: Chat Module Workflows** (Week 5)

**Goal**: Implement Chat-specific workflows

**Tasks**:
1. Create `bmad/axon/workflows/chat-conversation-flow/`
2. Create `bmad/axon/workflows/chat-ai-integration/`
3. Create `bmad/axon/workflows/chat-aggregate-design/`

**Deliverables**:
- 3 Chat workflows
- Chat pattern validation rules
- Owned entity pattern templates

---

### **Phase 6: Testing & Refinement** (Week 6)

**Goal**: Test with real stories, refine based on usage

**Tasks**:
1. Test `story-to-production` with real Identity story
2. Test `identity-authentication-flow` with real feature
3. Test `chat-conversation-flow` with real feature
4. Capture learnings
5. Refine workflows based on real usage
6. Update documentation

**Deliverables**:
- Tested workflows
- Refined agent behaviors
- Updated documentation
- Initial decision knowledge base

---

## 🎯 SUCCESS METRICS

### **Expected Outcomes** (3 months after implementation)

**Efficiency Gains**:
- ✅ **80% reduction** in "AI invented non-existent method" errors
- ✅ **90% reduction** in manual code when library feature exists
- ✅ **95% pattern compliance** (Result<T>, StrongId<T>, CQRS)
- ✅ **2x faster** feature delivery with same/better quality

**Quality Improvements**:
- ✅ **Zero documentation drift** (docs updated with code)
- ✅ **90%+ test coverage** (comprehensive test generation)
- ✅ **100% build success rate** (validation before commit)

**Developer Experience**:
- ✅ **3-4 efficient checkpoints** per story (not 21+)
- ✅ **Trust AI** to work autonomously between checkpoints
- ✅ **Context continuity** (workflows remember decisions)
- ✅ **Learning accumulation** (past mistakes captured)

---

## 📝 NEXT STEPS

### **To Continue in New Window**:

1. **Load this document**: `session.md`
2. **Review**: Agent team + workflow catalog
3. **Begin**: Phase 1 (Module Foundation)
4. **Command**: `*create-module` with this design as input

### **Key Files to Reference**:
- `session.md` (this document) - Complete design
- `Docs/ENGINEERING/00-START-HERE.md` - Axon architecture
- `Docs/Libraries/00-INDEX.md` - Library index
- `bmad/bmb/workflows/create-module/` - Module creation workflow

---

## 🎯 DESIGN PRINCIPLES SUMMARY

1. **Doc-Grounded**: Every decision validated against `/Docs`
2. **Discovery-First**: Search before creating
3. **Library-Aware**: Use tools, not manual code
4. **Pattern-Compliant**: Follow established patterns exactly
5. **Checkpoint-Efficient**: 3-4 strategic approvals
6. **Doc-Maintaining**: Zero drift (continuous sync)
7. **Learning-Enabled**: Capture decisions for future
8. **Module-Aware**: Specialized workflows for Identity/Chat
9. **Brownfield-Safe**: Surgical, minimal changes
10. **Human-in-the-Loop**: Strategic approvals, not micromanagement

---

**END OF DESIGN SESSION DOCUMENTATION**

**Status**: ✅ Complete and ready for implementation  
**Next**: Begin module creation in new window  
**Created**: 2025-09-29  
**By**: Valik + BMad Builder Agent