# Pre-Flight Validation - Reusable Support Workflow

**Tier 4 Support Workflow** | Invoked by implementation workflows

Discovery-first, library-first, pattern-compliance validation executed in parallel before code generation.

---

## 🎯 Purpose

Prevents hallucination, enforces reuse, validates patterns through parallel execution of 3 specialized agents:
- **@axon-archaeologist** - Codebase discovery (search existing, find patterns)
- **@axon-library-sage** - Library validation (avoid manual code)
- **@axon-doc-oracle** - Pattern compliance (Result<T>, StrongId<T>, CQRS, ADRs)

**Core Value**: Catch issues before code generation, maximize reuse, ensure pattern compliance.

---

## 📋 When Invoked

**Invoked by** any implementation workflow at **Phase 1: Pre-Flight Validation**:

```yaml
Invoking Workflows:
  - story-implementation (Tier 2)
  - story-refactoring (Tier 2)
  - identity-workflow (Tier 3)
  - chat-workflow (Tier 3)
  - api-workflow (Tier 3)

Timing: After story understanding, before implementation
```

**Example**: story-implementation Phase 1 invokes pre-flight-validation to discover existing code, check libraries, validate patterns before proceeding to Phase 2 (Implementation).

---

## 🔄 Workflow Execution

**3 Agents Execute in Parallel** (10-15 minutes total):

```
┌─────────────────────────────────────────────────────┐
│  Pre-Flight Validation (Parallel Execution)        │
├─────────────────────────────────────────────────────┤
│                                                     │
│  Agent 1: @axon-archaeologist (Discovery)          │
│  ├─ Search 5 layers (Domain, App, Infra, API, X)  │
│  ├─ Find similar patterns (semantic search)       │
│  ├─ Calculate reuse score (High | Med | Low)      │
│  └─ Output: discovery-report.yaml                 │
│                                                     │
│  Agent 2: @axon-library-sage (Library Check)       │
│  ├─ Check 4 categories (Core, Data, Ext, Infra)   │
│  ├─ 4-factor scoring (capability, complexity...)   │
│  ├─ Recommend library vs manual                   │
│  └─ Output: library-validation.yaml               │
│                                                     │
│  Agent 3: @axon-doc-oracle (Pattern Compliance)    │
│  ├─ Validate 5 patterns (Result, StrongId, CQRS)  │
│  ├─ Check 6 ADRs (which apply, compliance %)      │
│  ├─ Score compliance (0-100%)                     │
│  └─ Output: pattern-compliance.yaml               │
│                                                     │
└─────────────────────────────────────────────────────┘
             ↓
┌─────────────────────────────────────────────────────┐
│  Consolidated Summary                               │
│  - Reuse Score: High | Med | Low                    │
│  - Library Coverage: 0-100%                         │
│  - Pattern Compliance: 0-100%                       │
│  - Recommendation: Proceed | Review | Block         │
└─────────────────────────────────────────────────────┘
             ↓
     Return to invoking workflow
           (Checkpoint 2)
```

---

## 📊 Output Reports

**3 YAML Reports Generated**:

### 1. Discovery Report (discovery-report.yaml)

```yaml
reuse_recommendations:
  REUSE: [Components usable as-is]
  EXTEND: [Components to extend]
  ADAPT: [Patterns to adapt]
  CREATE: [New components needed]

reuse_score: High  # High | Medium | Low

existing_components:
  domain: [Aggregates, entities, value objects]
  application: [Handlers, services]
  infrastructure: [Repositories, EF configs]
  api: [Endpoints]

similar_patterns:
  - pattern: AxonPrincipal.LinkWalletOwnership
    location: src/Modules/Identity/Domain/Aggregates/AxonPrincipal/Commands.cs:45
    similarity: High
    adaptation_needed: None
```

### 2. Library Validation Report (library-validation.yaml)

```yaml
library_recommendations:
  - library: FastEndpoints
    use_case: REST endpoint development
    capability_match: 95%
    recommendation: Use
    justification: Full REPR pattern support, OpenAPI generation

  - library: FluentValidation
    use_case: Request validation
    capability_match: 100%
    recommendation: Use
    justification: Comprehensive validation rules

manual_code_needed:
  - component: Custom business rule validator
    reason: Domain-specific logic
    complexity: Medium

integration_complexity: Low

overall_score:
  library_coverage: 85%
  manual_code_percentage: 15%
```

### 3. Pattern Compliance Report (pattern-compliance.yaml)

```yaml
compliance_score: 97%

pattern_breakdown:
  result_pattern: 100%  # All operations return Result<T>
  strongid_pattern: 95%  # Most IDs are StrongId<T>
  cqrs_pattern: 100%  # Commands/queries separated
  domain_events: 90%  # Events raised after mutations
  owned_entities: 100%  # EF Core OwnsMany correct

violations:
  - pattern: strongid_pattern
    description: Primitive Guid used instead of StrongId<ConversationId>
    location: src/Modules/Chat/Application/Queries/GetConversations.cs:12
    recommendation: Replace Guid with ConversationId (StrongId<Guid>)

adr_alignment:
  - adr: ADR-002 (CQRS with MediatR)
    applies: true
    compliance: 100%
  - adr: ADR-003 (Result Pattern)
    applies: true
    compliance: 100%

recommendations: [Fix StrongId usage in GetConversations query]
```

---

## 🔍 Discovery Layers (5 Layers)

**@axon-archaeologist searches**:

1. **Domain Layer**
   - Aggregates, entities, value objects
   - Domain events, specifications
   - Business rules

2. **Application Layer**
   - Commands, queries, handlers
   - Services, DTOs
   - Validators

3. **Infrastructure Layer**
   - Repositories, EF Core configurations
   - External service clients
   - Infrastructure services

4. **API Layer**
   - FastEndpoints endpoints
   - Validators, contracts
   - Request/response DTOs

5. **Cross-Module Layer**
   - Shared kernel (BuildingBlocks)
   - Cross-cutting concerns
   - Shared patterns

---

## 📚 Library Categories (4 Categories)

**@axon-library-sage validates**:

1. **Core Libraries**
   - MediatR (CQRS)
   - FluentValidation (validation)
   - FastEndpoints (API)

2. **Data Libraries**
   - EF Core (ORM)
   - Dapper (micro-ORM, if needed)

3. **External Libraries**
   - Dynamic.xyz (JWT auth)
   - OpenAI (Claude API)
   - Helius (Solana)

4. **Infrastructure Libraries**
   - OpenTelemetry (observability)
   - Polly (resilience)
   - Serilog (logging)

**4-Factor Scoring**:
- Capability match: How well library covers requirements (0-100%)
- Complexity reduction: How much manual code avoided
- Maintenance burden: Ongoing maintenance cost
- Integration cost: Effort to integrate

---

## ✅ Pattern Dimensions (5 Patterns)

**@axon-doc-oracle validates**:

1. **Result<T, Error> Pattern**
   - All operations return Result<T>
   - No exceptions in domain/application
   - Error types appropriate

2. **StrongId<T> Pattern**
   - All IDs are StrongId<T>
   - No primitive obsession (Guid, int)
   - Vogen value objects used

3. **CQRS Pattern**
   - Commands separated from queries
   - MediatR handlers implemented
   - Request/response patterns correct

4. **Domain Events Pattern**
   - Events raised after mutations
   - Event handlers implemented
   - Event naming conventions followed

5. **Owned Entities Pattern** (if applicable)
   - EF Core OwnsMany used
   - Composite keys configured
   - No independent DbSet

**ADR Validation** (6 ADRs):
- ADR-001: Modular Monolith
- ADR-002: CQRS with MediatR
- ADR-003: Result Pattern
- ADR-004: Strong IDs
- ADR-005: PostgreSQL + xmin concurrency
- ADR-006: FastEndpoints

---

## 📊 Success Criteria

- ✅ **Discovery Complete**: 100% (all 5 layers searched)
- ✅ **Reuse Score Calculated**: High | Medium | Low
- ✅ **Library Validation Complete**: 100% (all categories checked)
- ✅ **Pattern Compliance Score**: ≥ 95%
- ✅ **Execution Time**: ≤ 15 minutes
- ✅ **Parallel Execution**: All 3 agents run concurrently

---

## 🚀 Usage Example

**Scenario**: story-implementation Phase 1 for "Add wallet verification endpoint"

**Invocation**:
```yaml
Invoking Workflow: story-implementation
Phase: Phase 1 (Pre-Flight Validation)
Inputs:
  story_context:
    story_id: AXON-123
    title: Add wallet verification endpoint
    module: Identity
    acceptance_criteria: [...]
  module_context: Identity + API
  docs_loaded: [7 Identity docs, 3 API docs]
  patterns_required: [Result<T>, StrongId<T>, CQRS, REPR]
```

**Execution** (parallel, 12 minutes):
1. **@axon-archaeologist**: Discovers existing `VerifyWalletSignature` handler, `Ed25519SignatureVerifier` service → Reuse Score: **High**
2. **@axon-library-sage**: FastEndpoints for endpoint, FluentValidation for request, NSec.Cryptography for crypto → Library Coverage: **90%**
3. **@axon-doc-oracle**: Validates Result<T>, StrongId<T>, CQRS, REPR patterns → Compliance: **98%**

**Output**:
```yaml
Consolidated Summary:
  Reuse Score: High (70% reusable)
  Library Coverage: 90%
  Pattern Compliance: 98%
  Recommendation: Proceed
  Blockers: None
  Key Recommendations:
    - REUSE: VerifyWalletSignature handler (extend for new endpoint)
    - USE: FastEndpoints for REPR pattern
    - FIX: One StrongId violation in request DTO
```

**Result**: story-implementation proceeds to Checkpoint 2 with high confidence, minimal new code needed.

---

## 🔗 Related Workflows

**Tier 2 (Core - Invokers)**:
- `story-implementation` - Invokes at Phase 1
- `story-refactoring` - Invokes at Phase 1

**Tier 3 (Module - Invokers)**:
- `identity-workflow` - Invokes at Enhancement Point 2
- `chat-workflow` - Invokes at Enhancement Point 2
- `api-workflow` - Invokes at Enhancement Point 2

**Tier 4 (Support - Siblings)**:
- `doc-sync` - Documentation maintenance

---

## 📖 Configuration

**Location**: `bmad/axon/workflows/pre-flight-validation/workflow.yaml`

**Key Variables**:
- `discovery_layers`: 5 layers (Domain, Application, Infrastructure, API, Cross-Module)
- `library_categories`: 4 categories (Core, Data, External, Infrastructure)
- `pattern_dimensions`: 5 patterns (Result, StrongId, CQRS, Events, Owned Entities)

**Invocation**:
```yaml
# From any implementation workflow
pre-flight-validation:
  inputs:
    - story_context
    - module_context
    - docs_loaded
    - patterns_required
  outputs:
    - discovery-report.yaml
    - library-validation.yaml
    - pattern-compliance.yaml
  duration: 10-15 minutes
```

---

## 🛠️ Troubleshooting

**Issue**: Agents not executing in parallel
- Check: Ensure workflow executor supports concurrent agent invocation
- Fix: Use parallel execution flag in workflow engine

**Issue**: Discovery report empty
- Check: Module context correct (Identity | Chat | API)?
- Check: Source paths accessible?
- Fix: Verify module_context input, check file permissions

**Issue**: Pattern compliance score < 95%
- Check: Violations list in pattern-compliance.yaml
- Fix: Address each violation before proceeding (or accept risk at Checkpoint 2)

**Issue**: Execution time > 15 minutes
- Check: Codebase size, network latency (external docs)
- Fix: Optimize search queries, cache library docs

---

**Version:** 1.0.0  
**Last Updated:** 2025-09-30  
**Status:** ✅ Production-Ready (BMM Token-Efficient Pattern)