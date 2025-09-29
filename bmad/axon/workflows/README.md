# Axon Workflows

**Status**: 🚧 Phases 3-5 - Workflow Creation (Pending)

---

## Overview

This directory will contain 9 specialized Axon workflows organized into 4 tiers, providing comprehensive brownfield development capabilities from story routing to documentation synchronization.

---

## Workflow Architecture (9 Workflows)

### Tier 1: Master Orchestration (1 Workflow)

#### **story-orchestrator/**
- **Purpose**: Master routing workflow
- **Type**: Orchestrator
- **Complexity**: Medium
- **Status**: ⏳ Phase 3 Pending
- **Responsibilities**:
  - Parse story.md file
  - Load BMM tech-spec (if referenced)
  - Detect story type (Feature/Refactor/Bugfix)
  - Detect module context (Identity/Chat/API/Cross-cutting)
  - Route to appropriate implementation workflow
- **Files to Create**:
  - `workflow.yaml` - Configuration
  - `instructions.md` - Routing logic
  - `README.md` - Documentation
  - `checklist.md` - Validation

---

### Tier 2: Core Implementation (3 Workflows)

#### **story-implementation/**
- **Purpose**: Feature development (greenfield in brownfield)
- **Type**: Full lifecycle
- **Complexity**: High
- **Status**: ⏳ Phase 3 Pending
- **4-Phase Structure**:
  - Phase 0: Story Understanding (Doc-Grounding)
  - Phase 1: Pre-Flight Validation (Discovery + Library + Pattern)
  - Phase 2: Implementation (Code Generation)
  - Phase 3: Validation (Tests + Doc Sync + Learning)
- **Checkpoints**: 4 strategic checkpoints
- **Files to Create**:
  - `workflow.yaml`
  - `instructions.md` (COMPLETE Phase 0-3 specs)
  - `template.md` (story template)
  - `README.md`
  - `checklist.md`

#### **story-refactoring/**
- **Purpose**: Safe brownfield refactoring
- **Type**: Brownfield-safe
- **Complexity**: High (danger zone)
- **Status**: ⏳ Phase 3 Pending
- **Key Features**:
  - Extra discovery (map ALL usages)
  - Impact analysis
  - Backward compatibility design
  - Extra validation (all existing tests pass)
  - Rollback plan
- **Files to Create**:
  - `workflow.yaml`
  - `instructions.md` (brownfield safety)
  - `README.md`
  - `checklist.md`

#### **story-bugfix/**
- **Purpose**: Root cause analysis + fix
- **Type**: Diagnostic + fix
- **Complexity**: Medium
- **Status**: ⏳ Phase 3 Pending
- **Phases**:
  - Root Cause Analysis
  - Fix Strategy
  - Implementation
  - Validation
- **Files to Create**:
  - `workflow.yaml`
  - `instructions.md` (RCA approach)
  - `README.md`
  - `checklist.md`

---

### Tier 3: Module-Specialized (3 Workflows)

#### **identity-workflow/**
- **Purpose**: Identity module specialization
- **Type**: Module-specific enhancement
- **Complexity**: High
- **Status**: ⏳ Phase 4 Pending
- **Module Context**:
  - Auth, wallets, principals
  - JWT validation, wallet signatures
  - Multi-provider credential management
  - Owned entity patterns
- **Doc Loading**: 5 Identity module docs + 1 library guide
- **Files to Create**:
  - `workflow.yaml` (extends story-implementation)
  - `instructions.md` (Identity context)
  - `README.md`
  - `checklist.md`

#### **chat-workflow/**
- **Purpose**: Chat module specialization
- **Type**: Module-specific enhancement
- **Complexity**: High
- **Status**: ⏳ Phase 4 Pending
- **Module Context**:
  - Conversations, messages, AI integration
  - Business rules (20+ rules)
  - Message ordering, turn-taking
  - Owned entity patterns
- **Doc Loading**: 5 Chat module docs + 3 library guides
- **Files to Create**:
  - `workflow.yaml` (extends story-implementation)
  - `instructions.md` (Chat context)
  - `README.md`
  - `checklist.md`

#### **api-workflow/**
- **Purpose**: API/FastEndpoints specialization
- **Type**: Module-specific enhancement
- **Complexity**: Medium
- **Status**: ⏳ Phase 4 Pending
- **Module Context**:
  - FastEndpoints vertical slice pattern
  - Request/Response mapping
  - Validation integration
  - API versioning
- **Doc Loading**: 1 API doc + 2 library guides
- **Files to Create**:
  - `workflow.yaml` (extends story-implementation)
  - `instructions.md` (API context)
  - `README.md`
  - `checklist.md`

---

### Tier 4: Support Workflows (2 Workflows)

#### **pre-flight-validation/**
- **Purpose**: Reusable discovery + library + pattern validation
- **Type**: Validation component
- **Complexity**: Medium
- **Status**: ⏳ Phase 5 Pending
- **Core Innovation**: Runs BEFORE code generation
- **Phases**:
  - Codebase Discovery (Archaeologist)
  - Library Capability Check (Library Sage)
  - Pattern Compliance (Doc Oracle)
- **Output**: Combined pre-flight package
- **Files to Create**:
  - `workflow.yaml`
  - `instructions.md` (parallel validation)
  - `README.md`
  - `checklist.md`

#### **doc-sync/**
- **Purpose**: Documentation maintenance & drift prevention
- **Type**: Maintenance workflow
- **Complexity**: Low
- **Status**: ⏳ Phase 5 Pending
- **Phases**:
  - Scan codebase for changes
  - Identify affected docs
  - Detect drift
  - Suggest updates
  - Apply updates
  - Validate accuracy
- **Execution**: Autonomous or with approval
- **Files to Create**:
  - `workflow.yaml`
  - `instructions.md` (drift detection)
  - `README.md`
  - `checklist.md`

---

## Workflow Structure (Standard)

Each workflow directory contains:

```
workflow-name/
├── workflow.yaml         # Configuration & variables
├── instructions.md       # Step-by-step execution instructions
├── README.md            # Documentation
├── checklist.md         # Validation checklist
└── template.md          # Output template (if document workflow)
```

---

## Implementation Timeline

| Phase | Workflows | Effort | Status |
|-------|-----------|--------|--------|
| **Phase 3** | 4 workflows (Tier 1 + Tier 2) | 16-20 hrs | ⏳ Pending |
| **Phase 4** | 3 workflows (Tier 3) | 12-16 hrs | ⏳ Pending |
| **Phase 5** | 2 workflows (Tier 4) | 6-8 hrs | ⏳ Pending |

**Total Estimated Effort**: 34-44 hours

---

## Workflow Routing Logic

```
story-orchestrator analyzes story.md:

Story Type Detection:
  - Feature → story-implementation
  - Refactor → story-refactoring
  - Bugfix → story-bugfix

Module Context Detection:
  - Identity → enhance with identity-workflow
  - Chat → enhance with chat-workflow
  - API/Endpoint → enhance with api-workflow
  - Cross-cutting → use base workflow

Example Routes:
  - "Add wallet verification" → Feature + Identity → identity-workflow
  - "Optimize message query" → Refactor + Chat → story-refactoring + chat-workflow
  - "Fix auth token expiry" → Bugfix + Identity → story-bugfix + identity-workflow
```

---

## Documentation References

**Design Document**: `Docs/PROCESS/bmad/axon-module-design-complete.md`

**Workflow Specifications**: See Section "📋 WORKFLOW CATALOG" in design doc

**Configuration**: See `bmad/axon/config.yaml` → `workflows` section

---

## Workflow Execution Engine

All workflows execute via **BMAD Core v6 Workflow Engine**:

**Engine**: `bmad/core/tasks/workflow.md`

**Key Features**:
- Step-by-step execution
- Variable resolution
- Checkpoint management
- Template output handling
- Elicitation support
- #yolo mode for trusted stories

---

## Next Steps

1. Review workflow specifications in design document
2. Complete Phase 3: Core workflows (story-orchestrator, story-implementation, story-refactoring, story-bugfix)
3. Complete Phase 4: Module workflows (identity, chat, api)
4. Complete Phase 5: Support workflows (pre-flight-validation, doc-sync)
5. Test end-to-end with real stories

---

**Created**: 2025-09-30
**Status**: Phases 3-5 Pending
**Total Workflows Planned**: 9
**Total Files to Create**: ~36 files (4 files × 9 workflows)