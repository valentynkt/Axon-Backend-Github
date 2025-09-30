# Pre-Flight Validation - Support Workflow Instructions

<workflow>

<critical>Governed by: {project-root}/bmad/core/tasks/workflow.md</critical>
<critical>Loaded config: {project-root}/bmad/axon/workflows/pre-flight-validation/workflow.yaml</critical>
<critical>REUSABLE: Invoked by story-implementation, story-refactoring, module workflows</critical>

## Overview

Reusable pre-flight validation executing 3 agents **in parallel** for discovery-first, library-first, and pattern-compliance validation before code generation.

**Purpose**: Prevent hallucination, enforce reuse, validate patterns

**Invoked by**: story-implementation (Phase 1), story-refactoring (Phase 1), identity-workflow (Enhancement Point 2), chat-workflow (Enhancement Point 2), api-workflow (Enhancement Point 2)

**Duration**: 10-15 minutes (parallel execution)

---

<step n="0" goal="Initialize pre-flight context">
<action>Load {installed_path}/workflow.yaml</action>
<action>Receive inputs from invoking workflow:
- story_context (metadata, ACs, tech spec)
- module_context (Identity | Chat | API | Cross-cutting)
- docs_loaded (list)
- patterns_required (list)
</action>
<action>Set output paths for 3 reports (discovery, library, pattern compliance)</action>
</step>

---

## PARALLEL EXECUTION: 3 AGENTS

<step n="1" goal="Execute 3 agents in parallel">
<action>Launch ALL 3 agents concurrently (do not wait for sequential completion):

**Agent 1: @axon-archaeologist** (Discovery)
- Search existing: 5 layers (Domain, Application, Infrastructure, API, Cross-Module)
- Find similar: Semantic similarity search
- Map APIs: Available endpoints, services, repositories
- Calculate reuse score: High | Medium | Low
- Output: discovery-report.yaml

**Agent 2: @axon-library-sage** (Library Validation)
- Check libraries: 4 categories (Core, Data, External, Infrastructure)
- 4-factor scoring: Capability, complexity, maintenance, integration
- Recommend: Library-first vs manual code
- Output: library-validation.yaml

**Agent 3: @axon-doc-oracle** (Pattern Compliance)
- Validate patterns: 5 dimensions (Result<T>, StrongId<T>, CQRS, Domain Events, Owned Entities)
- Check ADRs: Which ADRs apply (6 total)
- Score compliance: 0-100%
- Output: pattern-compliance.yaml
</action>

<critical>All 3 agents MUST execute in parallel, not sequentially</critical>
</step>

---

## DISCOVERY REPORT (@axon-archaeologist)

<step n="2" goal="Codebase discovery (parallel with steps 3-4)">
<action>Invoke @axon-archaeologist with task: search-existing.md

**5-Layer Search** (from workflow.yaml: discovery_layers):
1. **Domain**: Aggregates, entities, value objects, domain events, specifications
2. **Application**: Commands, queries, handlers, services, DTOs
3. **Infrastructure**: Repositories, EF Core configs, external service clients
4. **API**: Endpoints, validators, contracts
5. **Cross-Module**: Shared kernel, building blocks

**Search Strategy**:
- Search by module context (Identity | Chat | API)
- Find similar patterns (semantic search)
- Map available APIs (services, repositories, endpoints)
- Identify reusable components
</action>

<output format="YAML" path="{output_folder}/{story_id}/discovery-report.yaml">
reuse_recommendations:
  REUSE: [Components that can be used as-is]
  EXTEND: [Components that can be extended with minimal changes]
  ADAPT: [Patterns that can be adapted]
  CREATE: [New components needed]

reuse_score: High | Medium | Low

existing_components:
  domain: [Aggregates, entities, value objects found]
  application: [Handlers, services found]
  infrastructure: [Repositories, services found]
  api: [Endpoints found]

similar_patterns:
  - pattern: [Pattern name]
    location: [file:line]
    similarity: [High | Medium | Low]
    adaptation_needed: [Description]
</output>
</step>

---

## LIBRARY VALIDATION (@axon-library-sage)

<step n="3" goal="Library capabilities check (parallel with steps 2, 4)">
<action>Invoke @axon-library-sage with task: suggest-approach.md

**4 Library Categories** (from workflow.yaml: library_categories):
1. **Core**: MediatR, FluentValidation, FastEndpoints
2. **Data**: EF Core, Dapper
3. **External**: Dynamic.xyz, OpenAI, Helius
4. **Infrastructure**: OpenTelemetry, Polly, Serilog

**4-Factor Scoring**:
- Capability match: How well library covers requirements
- Complexity reduction: How much manual code avoided
- Maintenance burden: Ongoing maintenance cost
- Integration cost: Effort to integrate
</action>

<output format="YAML" path="{output_folder}/{story_id}/library-validation.yaml">
library_recommendations:
  - library: [Library name]
    use_case: [What it solves]
    capability_match: [0-100%]
    recommendation: Use | Extend | Manual
    justification: [Why]

manual_code_needed:
  - component: [What must be written manually]
    reason: [Why no library covers this]
    complexity: [Low | Medium | High]

integration_complexity: Low | Medium | High

overall_score:
  library_coverage: [0-100%]
  manual_code_percentage: [0-100%]
</output>
</step>

---

## PATTERN COMPLIANCE (@axon-doc-oracle)

<step n="4" goal="Pattern validation (parallel with steps 2-3)">
<action>Invoke @axon-doc-oracle with task: validate-patterns.md

**5 Pattern Dimensions** (from workflow.yaml: pattern_dimensions):
1. **Result<T, Error>**: All operations return Result<T>
2. **StrongId<T>**: All IDs are StrongId<T>
3. **CQRS**: Commands, queries, handlers, MediatR
4. **Domain Events**: Event raising, handlers
5. **Owned Entities**: EF Core OwnsMany, composite keys (if applicable)

**ADR Validation** (6 ADRs):
- ADR-001: Modular Monolith
- ADR-002: CQRS with MediatR
- ADR-003: Result Pattern
- ADR-004: Strong IDs
- ADR-005: PostgreSQL + xmin concurrency
- ADR-006: FastEndpoints
</action>

<output format="YAML" path="{output_folder}/{story_id}/pattern-compliance.yaml">
compliance_score: [0-100%]

pattern_breakdown:
  result_pattern: [0-100%]
  strongid_pattern: [0-100%]
  cqrs_pattern: [0-100%]
  domain_events: [0-100%]
  owned_entities: [0-100%]

violations:
  - pattern: [Pattern name]
    description: [What's wrong]
    location: [Where]
    recommendation: [How to fix]

adr_alignment:
  - adr: [ADR number]
    applies: true | false
    compliance: [0-100%]

recommendations: [How to achieve 95%+ compliance]
</output>
</step>

---

## CONSOLIDATION

<step n="5" goal="Wait for all 3 agents to complete">
<action>Wait for parallel execution completion:
- discovery-report.yaml (from @axon-archaeologist)
- library-validation.yaml (from @axon-library-sage)
- pattern-compliance.yaml (from @axon-doc-oracle)
</action>

<critical>Do NOT proceed until all 3 reports exist</critical>
</step>

<step n="6" goal="Generate consolidated summary">
<output section="pre_flight_summary">
**Pre-Flight Validation Complete** ✅

**Discovery** (@axon-archaeologist):
- Reuse Score: [High | Medium | Low]
- REUSE: [Count] components
- EXTEND: [Count] components
- CREATE: [Count] new components

**Library Validation** (@axon-library-sage):
- Library Coverage: [0-100%]
- Manual Code: [0-100%]
- Integration Complexity: [Low | Medium | High]

**Pattern Compliance** (@axon-doc-oracle):
- Compliance Score: [0-100%]
- Violations: [Count]
- ADRs Applied: [Count]/6

**Overall Assessment**:
- Ready for Implementation: Yes | No
- Blockers: [List if any]
- Recommendations: [Key recommendations]
</output>
</step>

---

## COMPLETION

<step n="7" goal="Return to invoking workflow">
<action>Return control to invoking workflow with:
- All 3 reports (discovery, library, pattern compliance)
- Consolidated summary
- Approval recommendation (Proceed | Review | Block)
</action>

<critical>Invoking workflow will present Checkpoint 2 for user approval</critical>
</step>

</workflow>