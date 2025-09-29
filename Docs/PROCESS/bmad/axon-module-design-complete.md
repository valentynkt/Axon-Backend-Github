# Axon Module - Complete Design Specification

**Design Date**: 2025-09-29
**Designer**: Valik + BMad Builder Agent
**Version**: 1.1 - Implementation in Progress
**Status**: 🚧 **Phase 1 Complete** | Phase 2 In Progress
**Last Updated**: 2025-09-30

---

## 📋 TABLE OF CONTENTS

1. [Implementation Progress](#implementation-progress) 🆕
2. [Executive Summary](#executive-summary)
3. [Design Decisions](#design-decisions)
4. [Architecture Overview](#architecture-overview)
5. [Integration with BMM](#integration-with-bmm)
6. [Module Structure](#module-structure)
7. [Agent Specifications](#agent-specifications)
8. [Workflow Catalog](#workflow-catalog)
9. [Story Template](#story-template)
10. [Implementation Roadmap](#implementation-roadmap)
11. [Success Metrics](#success-metrics)

---

## 🚧 IMPLEMENTATION PROGRESS

**Overall Status**: Phase 1 Complete (12.5% of total implementation)

### Phase Completion Summary

| Phase | Status | Progress | Deliverables | Completion Date |
|-------|--------|----------|--------------|-----------------|
| **Phase 1: Module Foundation** | ✅ Complete | 100% | 5 files, 745 lines | 2025-09-30 |
| **Phase 2: Agent Creation** | ⏳ Pending | 0% | 0/6 agents | - |
| **Phase 3: Core Workflows** | ⏳ Pending | 0% | 0/4 workflows | - |
| **Phase 4: Module Workflows** | ⏳ Pending | 0% | 0/3 workflows | - |
| **Phase 5: Support Workflows** | ⏳ Pending | 0% | 0/2 workflows | - |
| **Phase 6: Templates & Data** | ⏳ Pending | 0% | 0/7 files | - |
| **Phase 7: Integration Testing** | ⏳ Pending | 0% | 0/5 stories | - |
| **Phase 8: Documentation** | ⏳ Pending | 0% | - | - |

**Total Progress**: 1/8 phases complete (12.5%)

### Phase 1 Achievements ✅

**Created Files**:
- `bmad/axon/config.yaml` (234 lines) - Complete module configuration
- `bmad/axon/README.md` (334 lines) - Comprehensive documentation
- `bmad/axon/_module-installer/install-module-config.yaml` (177 lines) - Installer config
- `bmad/axon/templates/.gitkeep` - Template directory placeholder
- `bmad/axon/data/.gitkeep` - Data directory placeholder

**Created Directories**:
- `bmad/axon/agents/` (empty, Phase 2)
- `bmad/axon/workflows/` (empty, Phases 3-5)
- `bmad/axon/tasks/` (empty, optional)
- `bmad/axon/templates/` (Phase 6)
- `bmad/axon/data/` (Phase 6)
- `bmad/axon/_module-installer/assets/`

**Configuration Highlights**:
- 252 settings across 8 major sections
- 6 agents defined (awaiting implementation)
- 9 workflows defined in 4 tiers (awaiting implementation)
- BMM integration configured
- All doc references verified against actual files
- Success metrics defined

**Quality Metrics**:
- ✅ YAML structure validated
- ✅ All documentation paths verified
- ✅ Comprehensive README with examples
- ✅ Complete installer configuration
- ✅ Production-ready foundation

### Next Phase: Agent Creation (Phase 2)

**Priority Order**:
1. Agent 1: Story Orchestrator (Priority 1)
2. Agent 2: Doc Oracle (Priority 1)
3. Agent 3: Archaeologist (Priority 2)
4. Agent 4: Library Sage (Priority 2)
5. Agent 5: Implementation Surgeon (Priority 3)
6. Agent 6: Quality Guardian (Priority 3)

**Estimated Time**: 12-16 hours

---

## 🎯 EXECUTIVE SUMMARY

### **What is the Axon Module?**

The **Axon Module** is a specialized BMAD implementation workflow system designed for **brownfield .NET development** with Clean Architecture + DDD + CQRS patterns. It extends BMM (BMAD Method Module) by providing **implementation-focused workflows** while leveraging BMM's proven planning capabilities.

### **Key Innovation**

**Hybrid Lifecycle Approach**:
- **Planning Phase**: Uses existing BMM workflows (Product Brief → PRD → Tech Spec)
- **Implementation Phase**: Uses new Axon-specific workflows with:
  - Doc-grounded validation
  - Discovery-first approach (search before creating)
  - Library-first implementation (use tools, not manual code)
  - Pattern compliance (Result<T>, StrongId<T>, CQRS)
  - 4 strategic checkpoints (not 21+)

### **Core Problem Solved**

**AI Pain Points in Brownfield Development**:
1. ❌ AI invents non-existent methods/APIs
2. ❌ AI writes manual code when libraries exist
3. ❌ AI violates established patterns
4. ❌ Documentation drifts from code
5. ❌ Too many checkpoints slow development

**Axon Solution**:
1. ✅ Discovery-first (Archaeologist agent searches codebase)
2. ✅ Library-first (Library Sage checks capabilities)
3. ✅ Pattern-compliance (Doc Oracle validates)
4. ✅ Continuous doc sync (no drift)
5. ✅ 4 batched checkpoints (efficient workflow)

---

## 🎲 DESIGN DECISIONS

### **Decision 1: Extend BMM vs Create New Module**

**✅ CHOSEN**: **Hybrid Approach (Option 3)**

**Rationale**:
- BMM already has excellent planning workflows (1-analysis, 2-plan, 3-solutioning)
- Axon needs specialized implementation for brownfield .NET + patterns
- Clear separation: BMM = Planning, Axon = Implementation

**Integration Flow**:
```
BMM Planning Phase
  ↓ (outputs: tech-spec.md, PRD.md)
Axon Implementation Phase
  ↓ (outputs: code, tests, updated docs)
Production
```

---

### **Decision 2: Agent Architecture**

**✅ CHOSEN**: **6 Specialized Agents**

**Rationale**:
- Better decomposition than 1-3 agents
- Expert domain focus (docs, discovery, library, implementation, quality)
- Clear responsibilities
- Parallel execution where possible

**Agents**:
1. **Axon Story Orchestrator** - Master coordinator
2. **Axon Doc Oracle** - Documentation intelligence
3. **Axon Archaeologist** - Codebase discovery
4. **Axon Library Sage** - Library expertise
5. **Axon Implementation Surgeon** - Code generation
6. **Axon Quality Guardian** - Testing & validation

---

### **Decision 3: Workflow Strategy**

**✅ CHOSEN**: **9 Logical Workflows** (Not 1, Not 10+)

**Rationale**:
- 1 Master Orchestrator (routing)
- 3 Core Implementation (feature/refactor/bugfix)
- 3 Module-Specific (Identity/Chat/API)
- 2 Support (pre-flight, doc-sync)

**Tier Structure**:
```
Tier 1: Master Orchestration (1)
  └── story-orchestrator

Tier 2: Core Implementation (3)
  ├── story-implementation
  ├── story-refactoring
  └── story-bugfix

Tier 3: Module-Specialized (3)
  ├── identity-workflow
  ├── chat-workflow
  └── api-workflow

Tier 4: Support Workflows (2)
  ├── pre-flight-validation
  └── doc-sync
```

---

### **Decision 4: Checkpoint Strategy**

**✅ CHOSEN**: **4 Strategic Checkpoints**

**Rationale**:
- From session.md: 4 checkpoints balances efficiency and control
- Batched, not scattered
- Strategic points: Understanding → Pre-flight → Code Review → Commit

**Checkpoints**:
1. **Checkpoint 1**: Story Understanding Approval (1 min)
2. **Checkpoint 2**: Pre-Flight Approval (3-5 min)
3. **Checkpoint 3**: Implementation Review (5-10 min)
4. **Checkpoint 4**: Final Commit Approval (2 min)

**Total Time**: 11-18 minutes per story (human review only)

**Optional**: #yolo mode to skip checkpoints for trusted stories

---

### **Decision 5: Story Template Format**

**✅ CHOSEN**: **BMM-Style with Full Context**

**Rationale**:
- Proven BMM template structure (PRD/Tech Spec inspired)
- Full context for AI (architecture alignment, NFRs)
- References to BMM tech-spec (if created during planning)

**Template Sections**:
- Story header (ID, title, module, status)
- User story statement
- Acceptance criteria (testable)
- Technical context (patterns, architecture alignment)
- Tech spec reference (BMM handoff)
- Dependencies & integrations
- Test strategy
- Risks & assumptions

---

### **Decision 6: Doc Loading Strategy**

**✅ CHOSEN**: **Progressive Disclosure**

**Rationale**:
- Don't load all 120+ docs upfront (context overload)
- Start minimal, expand on-demand
- Hub-and-spoke pattern (core hub + contextual spokes)

**Loading Strategy**:
```yaml
Always Loaded (Core Hub):
  - Docs/ENGINEERING/00-START-HERE.md
  - Docs/ENGINEERING/guides/patterns/00-QUICK-REFERENCE.md  # Contains 80% of patterns, error handling, validation
  - Docs/Libraries/00-INDEX.md
  - Docs/ENGINEERING/guides/architecture/adrs/00-INDEX.md  # ADR catalog

Loaded on Module Context (ACTUAL FILES):
  - Identity: 5 module docs (00-INDEX, 01-domain-model, 03-authentication, 05-api-contracts, 06-database-schema)
  - Chat: 5 module docs (00-INDEX, 01-domain-model, 03-messaging-flows, 05-api-contracts, 06-database-schema)
  # Note: Files 02,04,07,08,09 were deleted from modules - only 5 files exist per module

Loaded on Pattern Need (ONLY 2 FILES):
  - CQRS: guides/patterns/cqrs.md
  - Domain Modeling: guides/patterns/domain-modeling.md
  # Note: error-handling.md and validation.md are now in QUICK-REFERENCE

Loaded on Library Need:
  - MediatR: Libraries/MediatR/IMPLEMENTATION_GUIDE.md
  # Note: 15 library guides available in Libraries/ directory

Loaded on ADR Need (6 ADRs):
  - guides/architecture/adrs/001-modular-monolith.md
  - guides/architecture/adrs/002-cqrs-mediatr.md
  - guides/architecture/adrs/003-result-pattern.md
  - guides/architecture/adrs/004-strong-ids.md
  - guides/architecture/adrs/005-postgresql.md
  - guides/architecture/adrs/006-fastendpoints.md
```

---

## 🏗️ ARCHITECTURE OVERVIEW

### **Complete Development Lifecycle**

```
═══════════════════════════════════════════════════════════════
                    PLANNING PHASE (BMM)
═══════════════════════════════════════════════════════════════
Epic/Feature Concept
    ↓
[BMM: @analyst] → Brainstorm + Product Brief
    ↓ (output: product-brief.md)
[BMM: @po] → PRD (Requirements + Epics)
    ↓ (output: PRD.md, epics.md)
[BMM: @architect] → Tech Spec (Architecture + Design)
    ↓ (output: tech-spec.md)

═══════════════════════════════════════════════════════════════
              HANDOFF (Tech Spec → Stories)
═══════════════════════════════════════════════════════════════

[BMM or Manual] → Story Breakdown
    ↓ (output: story-001.md, story-002.md, ...)
    Each story references tech-spec.md

═══════════════════════════════════════════════════════════════
                 IMPLEMENTATION PHASE (AXON)
═══════════════════════════════════════════════════════════════
Story Input (story.md + tech-spec reference)
    ↓
[AXON: @axon-story-orchestrator] → Analyze & Route
    ↓
[AXON Workflow] → 4-Phase Implementation:

    Phase 0: Story Understanding (Doc-Grounding)
      ↓ Checkpoint 1: Understanding Approval

    Phase 1: Pre-Flight Validation (Discovery + Library + Pattern)
      ↓ Checkpoint 2: Pre-Flight Approval

    Phase 2: Implementation (Code Generation)
      ↓ Checkpoint 3: Code Review

    Phase 3: Validation (Tests + Doc Sync + Learning)
      ↓ Checkpoint 4: Commit Approval

    ↓
    Output: Code + Tests + Updated Docs + Decision Log

═══════════════════════════════════════════════════════════════
```

### **Workflow Routing Logic**

```yaml
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

## 🔗 INTEGRATION WITH BMM

### **What BMM Provides** (Keep & Reuse)

**Planning Workflows** ✅:
- `bmad/bmm/workflows/1-analysis/brainstorm-project/` - Idea generation
- `bmad/bmm/workflows/1-analysis/product-brief/` - Product vision
- `bmad/bmm/workflows/2-plan/prd/` - Requirements document
- `bmad/bmm/workflows/3-solutioning/tech-spec/` - Technical specification

**Planning Agents** ✅:
- `@analyst` - Analysis & research
- `@po` - Product owner perspective
- `@architect` - Architecture design

### **What Axon Adds** (New)

**Implementation Workflows** 🆕:
- `bmad/axon/workflows/story-orchestrator/` - Master routing
- `bmad/axon/workflows/story-implementation/` - Feature development
- `bmad/axon/workflows/story-refactoring/` - Safe refactoring
- `bmad/axon/workflows/story-bugfix/` - Issue resolution
- `bmad/axon/workflows/identity-workflow/` - Identity module specialization
- `bmad/axon/workflows/chat-workflow/` - Chat module specialization
- `bmad/axon/workflows/api-workflow/` - API/FastEndpoints specialization
- `bmad/axon/workflows/pre-flight-validation/` - Reusable validation
- `bmad/axon/workflows/doc-sync/` - Documentation maintenance

**Implementation Agents** 🆕:
- `@axon-story-orchestrator` - Story lifecycle coordinator
- `@axon-doc-oracle` - Documentation intelligence
- `@axon-archaeologist` - Codebase discovery
- `@axon-library-sage` - Library expertise
- `@axon-implementation-surgeon` - Code generation
- `@axon-quality-guardian` - Testing & validation

### **Handoff Points**

**BMM → Axon**:
```yaml
BMM Output (tech-spec.md):
  sections:
    - Overview & Objectives
    - System Architecture Alignment
    - Detailed Design (services, models, APIs)
    - Non-Functional Requirements
    - Acceptance Criteria (authoritative)
    - Dependencies & Integrations
    - Risks & Assumptions
    - Test Strategy

Axon Story Template References:
  tech_spec_path: "path/to/tech-spec.md"

  sections_used:
    - Acceptance Criteria → Story ACs
    - Detailed Design → Implementation guidance
    - Architecture Alignment → Pattern compliance
    - Test Strategy → Test generation guidance
```

**Axon → Production**:
```yaml
Axon Output:
  - Code files (src/Modules/...)
  - Test files (tests/Modules/...)
  - Updated docs (Docs/ENGINEERING/...)
  - Decision log (Docs/PROCESS/active-stories/.../decisions.yaml)
```

---

## 📁 MODULE STRUCTURE

### **Complete Directory Tree**

```
bmad/axon/
├── config.yaml                         # Module configuration
├── README.md                           # Module documentation
│
├── agents/                             # 6 Axon agents
│   ├── axon-story-orchestrator.md           # Master coordinator
│   ├── axon-doc-oracle.md                   # Doc intelligence
│   ├── axon-archaeologist.md                # Codebase discovery
│   ├── axon-library-sage.md                 # Library expertise
│   ├── axon-implementation-surgeon.md       # Code generation
│   └── axon-quality-guardian.md             # Testing & validation
│
├── workflows/                          # 9 Axon workflows
│   ├── story-orchestrator/                  # Tier 1: Master
│   │   ├── workflow.yaml
│   │   ├── instructions.md
│   │   ├── README.md
│   │   └── checklist.md
│   │
│   ├── story-implementation/                # Tier 2: Core
│   │   ├── workflow.yaml
│   │   ├── instructions.md
│   │   ├── template.md (story template)
│   │   ├── README.md
│   │   └── checklist.md
│   │
│   ├── story-refactoring/                   # Tier 2: Core
│   │   ├── workflow.yaml
│   │   ├── instructions.md
│   │   ├── README.md
│   │   └── checklist.md
│   │
│   ├── story-bugfix/                        # Tier 2: Core
│   │   ├── workflow.yaml
│   │   ├── instructions.md
│   │   ├── README.md
│   │   └── checklist.md
│   │
│   ├── identity-workflow/                   # Tier 3: Module
│   │   ├── workflow.yaml
│   │   ├── instructions.md
│   │   ├── README.md
│   │   └── checklist.md
│   │
│   ├── chat-workflow/                       # Tier 3: Module
│   │   ├── workflow.yaml
│   │   ├── instructions.md
│   │   ├── README.md
│   │   └── checklist.md
│   │
│   ├── api-workflow/                        # Tier 3: Module
│   │   ├── workflow.yaml
│   │   ├── instructions.md
│   │   ├── README.md
│   │   └── checklist.md
│   │
│   ├── pre-flight-validation/               # Tier 4: Support
│   │   ├── workflow.yaml
│   │   ├── instructions.md
│   │   ├── README.md
│   │   └── checklist.md
│   │
│   └── doc-sync/                            # Tier 4: Support
│       ├── workflow.yaml
│       ├── instructions.md
│       ├── README.md
│       └── checklist.md
│
├── templates/                          # Axon templates
│   ├── story-template.md                    # BMM-style story format
│   ├── story-context-template.json          # Agent context
│   ├── decision-log-template.yaml           # Learning capture
│   └── doc-update-template.md               # Doc sync
│
├── data/                               # Supporting data
│   ├── pattern-catalog.yaml                 # Axon patterns
│   ├── library-capabilities.yaml            # Library feature map
│   └── module-boundaries.yaml               # Module context
│
└── _module-installer/                  # Installation
    ├── install-module-config.yaml
    └── assets/
```

### **Configuration File** (`config.yaml`)

```yaml
# Axon Module Configuration
module_name: Axon Development Orchestrator
module_code: axon
author: Valik
description: "Brownfield-aware AI development with library-first implementation for .NET + Clean Architecture + DDD + CQRS"

# Module paths
module_root: "{project-root}/bmad/axon"
installer_path: "{project-root}/bmad/axon/_module-installer"

# Integration with BMM
bmm_integration:
  enabled: true
  tech_spec_default_path: "{project-root}/Docs/BMAD"
  handoff_format: "tech-spec.md"

# Axon project paths
project_paths:
  docs_root: "{project-root}/Docs"
  engineering_docs: "{project-root}/Docs/ENGINEERING"
  libraries_docs: "{project-root}/Docs/Libraries"
  process_docs: "{project-root}/Docs/PROCESS"
  source_root: "{project-root}/src"
  tests_root: "{project-root}/tests"

# Component counts
agents:
  count: 6
  list:
    - axon-story-orchestrator
    - axon-doc-oracle
    - axon-archaeologist
    - axon-library-sage
    - axon-implementation-surgeon
    - axon-quality-guardian

workflows:
  count: 9
  tiers:
    master: [story-orchestrator]
    core: [story-implementation, story-refactoring, story-bugfix]
    module: [identity-workflow, chat-workflow, api-workflow]
    support: [pre-flight-validation, doc-sync]

# Module-specific settings
axon_settings:
  checkpoint_count: 4
  yolo_mode_available: true
  progressive_doc_loading: true
  pattern_validation_strict: true
  library_first_enforcement: true

# Output configuration
output_folder: "{project-root}/Docs/PROCESS/active-stories"
decision_log_folder: "{output_folder}/decisions"
story_template_path: "{module_root}/templates/story-template.md"

# Execution hints
execution:
  default_mode: interactive
  checkpoints_enabled: true
  parallel_validation: true  # Archaeologist + Library Sage + Doc Oracle
```

---

## 🦸 AGENT SPECIFICATIONS

### **Agent 1: Axon Story Orchestrator** 🎯

**File**: `bmad/axon/agents/axon-story-orchestrator.md`

**Role**: Master coordinator for story lifecycle  
**Personality**: Project manager who ensures smooth flow  
**Agent Type**: Module (orchestrator with commands)

**Core Responsibilities**:
- Parse story + acceptance criteria
- Load BMM tech-spec reference (if exists)  
- Route to appropriate workflow
- Manage 4 strategic checkpoints
- Capture decisions and learning
- Coordinate between specialist agents

**Mandatory Doc Loading** (Progressive):
```yaml
always_loaded:
  - Docs/ENGINEERING/00-START-HERE.md
  - Docs/ENGINEERING/guides/patterns/00-QUICK-REFERENCE.md
  - Docs/ENGINEERING/guides/architecture/system-overview.md  # Module boundaries
  - bmad/axon/templates/story-template.md
```

**Commands**: `*implement-story`, `*route`, `*checkpoint`, `*status`, `*capture-decision`

---

### **Agent 2: Axon Doc Oracle** 📚

**File**: `bmad/axon/agents/axon-doc-oracle.md`

**Role**: Documentation intelligence & validation specialist  
**Personality**: Scholar librarian who knows every doc  
**Agent Type**: Expert (consulting specialist)

**Core Responsibilities**:
- Progressive doc loading
- Validate against ADRs
- Detect doc drift
- Pattern compliance checking

**Doc Loading Configuration**:
```yaml
core_hub:
  - Docs/ENGINEERING/00-START-HERE.md
  - Docs/ENGINEERING/guides/patterns/00-QUICK-REFERENCE.md  # Contains error handling, validation
  - Docs/Libraries/00-INDEX.md

pattern_spokes:
  - Docs/ENGINEERING/guides/patterns/cqrs.md
  - Docs/ENGINEERING/guides/patterns/domain-modeling.md

architecture_spokes:
  - Docs/ENGINEERING/guides/architecture/system-overview.md
  - Docs/ENGINEERING/guides/architecture/tech-stack.md
  - Docs/ENGINEERING/guides/architecture/adrs/00-INDEX.md

adr_catalog:  # 6 ADRs available, load on demand
  - Docs/ENGINEERING/guides/architecture/adrs/001-modular-monolith.md
  - Docs/ENGINEERING/guides/architecture/adrs/002-cqrs-mediatr.md
  - Docs/ENGINEERING/guides/architecture/adrs/003-result-pattern.md
  - Docs/ENGINEERING/guides/architecture/adrs/004-strong-ids.md
  - Docs/ENGINEERING/guides/architecture/adrs/005-postgresql.md
  - Docs/ENGINEERING/guides/architecture/adrs/006-fastendpoints.md
```

**Commands**: `*load-context`, `*validate-against-docs`, `*detect-drift`, `*query-adr`, `*compliance-score`

---

### **Agent 3: Axon Archaeologist** 🔍

**File**: `bmad/axon/agents/axon-archaeologist.md`

**Role**: Codebase discovery specialist  
**Personality**: Detective finding existing treasure  
**Agent Type**: Expert (search & analysis)

**Core Responsibilities**:
- Search existing implementations
- Map available APIs
- Find reusable patterns
- Prevent reinvention

**Commands**: `*search-existing`, `*map-apis`, `*find-pattern`, `*discover-similar`, `*reuse-report`

---

### **Agent 4: Axon Library Sage** 🛠️

**File**: `bmad/axon/agents/axon-library-sage.md`

**Role**: Library-first implementation specialist  
**Personality**: Wise craftsperson with tool mastery  
**Agent Type**: Expert (library knowledge)

**Core Responsibilities**:
- Check library capabilities
- Suggest out-of-box solutions
- Prevent manual reimplementation
- Ensure correct library usage

**Commands**: `*check-library`, `*suggest-approach`, `*show-pattern`, `*validate-usage`, `*library-capabilities`

---

### **Agent 5: Axon Implementation Surgeon** ⚙️

**File**: `bmad/axon/agents/axon-implementation-surgeon.md`

**Role**: Precise code generation expert  
**Personality**: Surgical specialist with minimal changes  
**Agent Type**: Expert (execution specialist)

**Core Responsibilities**:
- Generate code after validation
- Extend existing code surgically
- Follow patterns exactly
- Make minimal brownfield-safe changes

**Commands**: `*implement`, `*extend`, `*apply-pattern`, `*diff-preview`, `*inline-docs`, `*integrate-library`

---

### **Agent 6: Axon Quality Guardian** ✅

**File**: `bmad/axon/agents/axon-quality-guardian.md`

**Role**: Testing & validation specialist  
**Personality**: QA expert ensuring excellence  
**Agent Type**: Expert (validation & testing)

**Core Responsibilities**:
- Generate comprehensive tests
- Validate against acceptance criteria
- Run build & tests
- Capture learning
- Update docs

**Commands**: `*generate-tests`, `*validate-implementation`, `*run-build`, `*run-tests`, `*compliance-check`, `*capture-decision`, `*update-docs`

---


## 📋 WORKFLOW CATALOG

### **Tier 1: Master Orchestration**

#### **Workflow 1: story-orchestrator** 🎯

**File**: `bmad/axon/workflows/story-orchestrator/`  
**Purpose**: Route stories to appropriate workflow  
**Type**: Master coordinator  
**Complexity**: Medium

**Responsibilities**:
- Parse story.md file
- Load BMM tech-spec (if referenced)
- Detect story type (Feature/Refactor/Bugfix)
- Detect module context (Identity/Chat/API/Cross-cutting)
- Route to appropriate implementation workflow
- Manage post-execution summary

**Routing Logic**:
```
Feature + Identity → identity-workflow
Feature + Chat → chat-workflow
Feature + API → api-workflow
Feature + Cross-cutting → story-implementation
Refactor + Any → story-refactoring
Bugfix + Any → story-bugfix
```

---

### **Tier 2: Core Implementation Workflows**

#### **Workflow 2: story-implementation** 🚀

**File**: `bmad/axon/workflows/story-implementation/`  
**Purpose**: Implement new features (greenfield in brownfield)  
**Type**: Full lifecycle implementation  
**Complexity**: High

**4-Phase Workflow**:
1. **Phase 0: Story Understanding** (Doc-Grounding)
   - Agents: Story Orchestrator + Doc Oracle
   - Load context progressively
   - ✅ Checkpoint 1: Understanding Approval

2. **Phase 1: Pre-Flight Validation** (Discovery + Library + Pattern)
   - Agents: Archaeologist + Library Sage + Doc Oracle (parallel)
   - Search existing code
   - Check library capabilities
   - Validate pattern compliance
   - ✅ Checkpoint 2: Pre-Flight Approval

3. **Phase 2: Implementation** (Code Generation)
   - Agent: Implementation Surgeon
   - Generate diff preview
   - ✅ Checkpoint 3: Preview Approval
   - Generate code with inline docs
   - Detect doc drift
   - ✅ Checkpoint 3b: Implementation Review

4. **Phase 3: Validation** (Tests + Doc Sync + Learning)
   - Agent: Quality Guardian
   - Generate comprehensive tests
   - Run build + tests
   - Final compliance check
   - Update documentation
   - Capture decision log
   - ✅ Checkpoint 4: Final Commit Approval

**Output**:
- Code files (src/Modules/...)
- Test files (tests/Modules/...)
- Updated docs (Docs/ENGINEERING/...)
- Decision log (Docs/PROCESS/active-stories/decisions/)

---

#### **Workflow 3: story-refactoring** 🔧

**File**: `bmad/axon/workflows/story-refactoring/`  
**Purpose**: Safely improve existing code  
**Type**: Brownfield refactoring  
**Complexity**: High (danger zone)

**Key Differences from story-implementation**:
- Extra discovery: Map ALL usages of code to refactor
- Impact analysis: What breaks if we change X?
- Backward compatibility: Design with compatibility in mind
- Extra validation: All existing tests MUST still pass
- Rollback plan: How to undo if things go wrong

**Additional Checkpoint**: Refactor Plan Approval (before implementation)

---

#### **Workflow 4: story-bugfix** 🐛

**File**: `bmad/axon/workflows/story-bugfix/`  
**Purpose**: Issue resolution with root cause analysis  
**Type**: Diagnostic + fix  
**Complexity**: Medium

**Workflow Phases**:
1. **Root Cause Analysis**
   - Reproduce issue
   - Identify faulty code
   - Understand why it broke

2. **Fix Strategy**
   - Determine fix approach
   - Assess impact
   - Plan tests

3. **Implementation**
   - Apply minimal fix
   - Add regression test
   - Update docs if needed

4. **Validation**
   - Verify issue resolved
   - All tests pass
   - No new issues introduced

---

### **Tier 3: Module-Specialized Workflows**

#### **Workflow 5: identity-workflow** 🔑

**File**: `bmad/axon/workflows/identity-workflow/`  
**Purpose**: Identity module specialization (auth, wallets, principals)  
**Type**: Module-specific enhancement  
**Complexity**: High

**Module-Specific Doc Loading** (5 module docs + 1 library):
```yaml
identity_module_docs:
  - Docs/ENGINEERING/modules/identity/00-INDEX.md
  - Docs/ENGINEERING/modules/identity/01-domain-model.md
  - Docs/ENGINEERING/modules/identity/03-authentication.md
  - Docs/ENGINEERING/modules/identity/05-api-contracts.md
  - Docs/ENGINEERING/modules/identity/06-database-schema.md

identity_library_docs:
  - Docs/Libraries/dynamic_auth/IMPLEMENTATION_GUIDE.md

identity_integration_docs:
  - Docs/ENGINEERING/integrations/00-INDEX.md
  # Note: dynamic-xyz/authentication-flow.md is scaffold only, no real content yet
```

**Identity-Specific Challenges**:
- JWT validation (JWKS, Dynamic.xyz specifics)
- Wallet signature verification (Ed25519 for Solana)
- Multi-provider credential management
- Principal resolution (deterministic)
- Auto-revocation logic
- Owned entity patterns (WalletOwnership, IdentityCredential)

**Extends**: story-implementation (adds Identity context)

---

#### **Workflow 6: chat-workflow** 💬

**File**: `bmad/axon/workflows/chat-workflow/`  
**Purpose**: Chat module specialization (conversations, messages, AI)  
**Type**: Module-specific enhancement  
**Complexity**: High

**Module-Specific Doc Loading** (5 module docs + 3 libraries):
```yaml
chat_module_docs:
  - Docs/ENGINEERING/modules/chat/00-INDEX.md
  - Docs/ENGINEERING/modules/chat/01-domain-model.md
  - Docs/ENGINEERING/modules/chat/03-messaging-flows.md
  - Docs/ENGINEERING/modules/chat/05-api-contracts.md
  - Docs/ENGINEERING/modules/chat/06-database-schema.md

chat_library_docs:
  - Docs/Libraries/OpenAI/IMPLEMENTATION_GUIDE.md
  - Docs/Libraries/ModelContextProtocol/IMPLEMENTATION_GUIDE.md
  - Docs/Libraries/ModelContextProtocolAspNetCore/IMPLEMENTATION_GUIDE.md
```

**Chat-Specific Challenges**:
- Conversation aggregate patterns
- Message ordering and turn-taking
- Business rules (20+ rules in Domain/Rules/)
- Owned entity patterns (Message as owned entity)
- AI integration (Claude API, MCP servers, streaming)
- Async processing
- SSE (Server-Sent Events) for streaming

**Extends**: story-implementation (adds Chat context)

---

#### **Workflow 7: api-workflow** 🌐

**File**: `bmad/axon/workflows/api-workflow/`  
**Purpose**: API/FastEndpoints specialization  
**Type**: Module-specific enhancement  
**Complexity**: Medium

**Module-Specific Doc Loading** (1 API doc + 2 libraries):
```yaml
api_docs:
  - Docs/ENGINEERING/api/00-INDEX.md  # Primary API documentation

api_library_docs:
  - Docs/Libraries/FastEndpoints/IMPLEMENTATION_GUIDE.md
  - Docs/Libraries/FluentValidation/IMPLEMENTATION_GUIDE.md

# Note: rest-conventions.md does not exist as separate file
# API conventions are documented in 00-INDEX.md and QUICK-REFERENCE.md
```

**API-Specific Challenges**:
- FastEndpoints vertical slice pattern
- Request/Response mapping
- Validation integration (FluentValidation)
- API versioning
- Error response format
- OpenAPI documentation

**Extends**: story-implementation (adds API context)

---

### **Tier 4: Support Workflows**

#### **Workflow 8: pre-flight-validation** 🔍

**File**: `bmad/axon/workflows/pre-flight-validation/`  
**Purpose**: Reusable discovery + library + pattern validation  
**Type**: Validation component  
**Complexity**: Medium

**This is the CORE INNOVATION** - runs BEFORE code generation

**Phases**:
1. **Codebase Discovery** (Archaeologist)
   - Search existing implementations
   - Map available APIs
   - Find similar patterns

2. **Library Capability Check** (Library Sage)
   - Check if library solves requirement
   - Recommend library vs manual

3. **Pattern Compliance** (Doc Oracle)
   - Validate against patterns
   - Check ADRs
   - Assess compliance

**Output**: Combined pre-flight package (discovery + library + compliance + plan)

**Used By**: All implementation workflows

---

#### **Workflow 9: doc-sync** 📚

**File**: `bmad/axon/workflows/doc-sync/`  
**Purpose**: Documentation maintenance & drift prevention  
**Type**: Maintenance workflow  
**Complexity**: Low

**Workflow Phases**:
1. Scan codebase for recent changes (Archaeologist)
2. Identify docs potentially affected (Doc Oracle)
3. Detect drift (code vs docs discrepancies)
4. Suggest updates
5. Apply updates (Quality Guardian)
6. Validate docs still accurate

**Execution**: Can run autonomously or with final approval

**When to Run**:
- After major feature completion
- Scheduled maintenance (weekly)
- When doc drift detected during implementation

---

## 📄 STORY TEMPLATE

### **Axon Story Template** (BMM-Inspired)

**File**: `bmad/axon/templates/story-template.md`

**Format**: Full context (BMM tech-spec inspired)

```markdown
# Story: {{story_title}}

**Story ID**: {{story_id}}
**Status**: Draft | Approved | In Progress | Completed
**Module**: Identity | Chat | API | Cross-cutting
**Story Type**: Feature | Refactor | Bugfix
**Priority**: High | Medium | Low
**Complexity**: Simple | Medium | Complex
**Created**: {{date}}
**Last Updated**: {{date}}

---

## User Story

**As a** {{role}}
**I want** {{capability}}
**So that** {{business_value}}

---

## Technical Context

### Architecture Alignment

**Related Tech Spec**: {{tech_spec_path}} (if from BMM planning)

**Patterns Required**:
- Result<T, Error> error handling
- StrongId<T> for entity IDs
- CQRS (command/query separation)
- Domain events (if applicable)

**Module Boundaries**:
- Primary Module: {{module_name}}
- Dependencies: {{module_dependencies}}

**Relevant ADRs**:
- {{adr_id}}: {{adr_title}}

---

## Acceptance Criteria

**Testable Criteria** (one test per AC):

1. **AC1**: {{acceptance_criterion_1}}
   - Test: Given {{context}}, When {{action}}, Then {{expected_result}}

2. **AC2**: {{acceptance_criterion_2}}
   - Test: Given {{context}}, When {{action}}, Then {{expected_result}}

3. **AC3**: {{acceptance_criterion_3}}
   - Test: Given {{context}}, When {{action}}, Then {{expected_result}}

---

## Detailed Design

### Services & Components
{{#if tech_spec_reference}}
See Tech Spec: {{tech_spec_path}}#detailed-design
{{else}}
- Service/Class: {{service_name}}
  - Methods: {{methods}}
  - Dependencies: {{dependencies}}
{{/if}}

### Data Models
{{#if tech_spec_reference}}
See Tech Spec: {{tech_spec_path}}#data-models
{{else}}
- Entity: {{entity_name}}
  - Properties: {{properties}}
  - Relationships: {{relationships}}
{{/if}}

### APIs/Interfaces
{{#if tech_spec_reference}}
See Tech Spec: {{tech_spec_path}}#apis-interfaces
{{else}}
- Endpoint: {{method}} {{path}}
  - Request: {{request_model}}
  - Response: {{response_model}}
{{/if}}

---

## Dependencies & Integrations

**External Dependencies**:
- Library: {{library_name}} ({{purpose}})

**Module Dependencies**:
- Module: {{module_name}} ({{integration_point}})

**Database Changes**:
- Migration needed: {{yes|no}}
- New tables/columns: {{changes}}

---

## Test Strategy

### Unit Tests
- Test all new methods
- Test all domain logic
- Test error paths

### Integration Tests
- Test end-to-end workflows
- Test database integration
- Test external service integration

### AC Coverage Tests
- One test per acceptance criterion
- Clear naming: Given_When_Then format

---

## Risks & Assumptions

### Risks
- Risk: {{risk_description}}
  - Mitigation: {{mitigation_strategy}}

### Assumptions
- Assumption: {{assumption_description}}
  - Validation: {{how_to_validate}}

### Open Questions
- Question: {{question}}
  - Owner: {{who_will_answer}}

---

## Non-Functional Requirements

### Performance
- Requirement: {{performance_target}}

### Security
- Requirement: {{security_consideration}}

### Observability
- Logging: {{logging_requirements}}
- Metrics: {{metrics_to_track}}
- Tracing: {{tracing_requirements}}

---

## Implementation Notes

**Estimated Complexity**: {{simple|medium|complex}}
**Estimated LOC**: ~{{loc_estimate}}
**Estimated Duration**: {{time_estimate}}

**Implementation Approach**:
{{implementation_notes}}

---

## Dev Agent Record

**Context Reference**:
- Story Context: {{path_to_story_context_json}}

**Implementation Log**:
- Started: {{start_date}}
- Completed: {{completion_date}}
- Developer: AI-driven (Axon agents)

**Decision Log**:
- Decisions: {{path_to_decisions_yaml}}

**Documentation Updates**:
{{#docs_updated}}
- {{doc_path}} - {{section_updated}}
{{/docs_updated}}

---

## Related Stories/Epics

**Epic**: {{epic_id}} - {{epic_title}}

**Related Stories**:
- {{story_id}}: {{story_title}} ({{relationship}})

**Blocked By**: {{blocker_story_ids}}
**Blocks**: {{blocked_story_ids}}

---

## Change History

| Date | Change | Author |
|------|--------|--------|
| {{date}} | Story created | {{author}} |
| {{date}} | {{change_description}} | {{author}} |

```

---


## 🗺️ IMPLEMENTATION ROADMAP

### **Phase 1: Module Foundation** ✅ **COMPLETE** (2025-09-30)

**Goal**: Create Axon module structure with configuration

**Status**: ✅ **COMPLETE** - All deliverables created and validated

**Tasks Completed**:
1. ✅ Created `bmad/axon/` directory structure (8 directories)
2. ✅ Created `bmad/axon/config.yaml` with complete configuration (234 lines)
3. ✅ Created `bmad/axon/README.md` with comprehensive documentation (334 lines)
4. ✅ Created `bmad/axon/templates/` directory with placeholder
5. ✅ Created `bmad/axon/data/` directory with placeholder
6. ✅ Set up installer configuration (177 lines)

**Actual Deliverables**:
- ✅ Module skeleton complete (8 directories: agents/, workflows/, tasks/, templates/, data/, _module-installer/assets/)
- ✅ Configuration file ready (`config.yaml` - 234 lines, 252 settings across 8 sections)
- ✅ Comprehensive documentation (`README.md` - 334 lines with examples, quick start, metrics)
- ✅ Installer configuration (`install-module-config.yaml` - 177 lines)
- ✅ Total: 745 lines of configuration and documentation

**Files Created**:
```
bmad/axon/
├── config.yaml                            (234 lines) ✅
├── README.md                              (334 lines) ✅
├── templates/.gitkeep                     ✅
├── data/.gitkeep                          ✅
└── _module-installer/
    └── install-module-config.yaml         (177 lines) ✅
```

**Quality Validation**:
- ✅ All doc references verified against actual codebase files
- ✅ YAML structure validated
- ✅ README comprehensive with examples
- ✅ Installer configuration complete with post-install messaging

**Estimated Effort**: 4-6 hours
**Actual Effort**: ~30 minutes (AI-accelerated) ⚡
**Completion Date**: 2025-09-30

---

### **Phase 2: Agent Creation** (Week 1-2)

**Goal**: Implement all 6 Axon agents with BMAD Core v6 compliance

**Tasks - Agent 1: Story Orchestrator** (Priority 1):
1. Create `bmad/axon/agents/axon-story-orchestrator.md`
2. Define agent XML structure (BMAD Core v6)
3. Implement commands: `*implement-story`, `*route`, `*checkpoint`, `*status`, `*capture-decision`
4. Define doc loading config (progressive)
5. Test routing logic

**Tasks - Agent 2: Doc Oracle** (Priority 1):
1. Create `bmad/axon/agents/axon-doc-oracle.md`
2. Define hub-and-spoke doc loading
3. Implement commands: `*load-context`, `*validate-against-docs`, `*detect-drift`, `*query-adr`
4. Define confidence scoring logic
5. Test doc loading and validation

**Tasks - Agent 3: Archaeologist** (Priority 2):
1. Create `bmad/axon/agents/axon-archaeologist.md`
2. Define search patterns (domain, aggregates, services, etc.)
3. Implement commands: `*search-existing`, `*map-apis`, `*find-pattern`
4. Test codebase discovery

**Tasks - Agent 4: Library Sage** (Priority 2):
1. Create `bmad/axon/agents/axon-library-sage.md`
2. Define library knowledge base
3. Implement commands: `*check-library`, `*suggest-approach`, `*show-pattern`
4. Test library recommendations

**Tasks - Agent 5: Implementation Surgeon** (Priority 3):
1. Create `bmad/axon/agents/axon-implementation-surgeon.md`
2. Define pattern compliance rules
3. Implement commands: `*implement`, `*extend`, `*diff-preview`, `*inline-docs`
4. Test code generation

**Tasks - Agent 6: Quality Guardian** (Priority 3):
1. Create `bmad/axon/agents/axon-quality-guardian.md`
2. Define testing strategy
3. Implement commands: `*generate-tests`, `*run-build`, `*run-tests`, `*capture-decision`
4. Test test generation

**Deliverables**:
- ✅ 6 fully functional agents
- ✅ All commands implemented
- ✅ Doc loading configs defined
- ✅ Agent persona and communication styles

**Estimated Effort**: 12-16 hours

---

### **Phase 3: Core Workflows** (Week 2-3)

**Goal**: Implement Tier 1 & Tier 2 workflows

**Tier 1: Master Orchestration**:
1. Create `bmad/axon/workflows/story-orchestrator/`
   - workflow.yaml
   - instructions.md (routing logic)
   - README.md
   - checklist.md
2. Implement story type detection (Feature/Refactor/Bugfix)
3. Implement module context detection (Identity/Chat/API)
4. Test routing to different workflows

**Tier 2: Core Implementation**:
1. Create `bmad/axon/workflows/story-implementation/`
   - workflow.yaml (4-phase structure)
   - instructions.md (COMPLETE Phase 0-3 specs)
   - template.md (story template)
   - README.md
   - checklist.md
2. Implement Phase 0: Story Understanding
3. Implement Phase 1: Pre-Flight Validation (parallel agents)
4. Implement Phase 2: Implementation (code generation)
5. Implement Phase 3: Validation (tests + doc sync)
6. Test complete workflow end-to-end

7. Create `bmad/axon/workflows/story-refactoring/`
   - workflow.yaml
   - instructions.md (brownfield safety)
   - README.md
   - checklist.md

8. Create `bmad/axon/workflows/story-bugfix/`
   - workflow.yaml
   - instructions.md (root cause analysis)
   - README.md
   - checklist.md

**Deliverables**:
- ✅ 4 workflows (1 master + 3 core)
- ✅ Complete workflow specifications
- ✅ Validation checklists
- ✅ End-to-end testing

**Estimated Effort**: 16-20 hours

---

### **Phase 4: Module-Specialized Workflows** (Week 3-4)

**Goal**: Implement Tier 3 workflows (Identity, Chat, API)

**Identity Workflow**:
1. Create `bmad/axon/workflows/identity-workflow/`
   - workflow.yaml (extends story-implementation)
   - instructions.md (Identity-specific context)
   - README.md
   - checklist.md
2. Define Identity doc loading (auth, wallets, principals)
3. Add Identity-specific validation (owned entities, Dynamic.xyz)
4. Test with real Identity story

**Chat Workflow**:
1. Create `bmad/axon/workflows/chat-workflow/`
   - workflow.yaml (extends story-implementation)
   - instructions.md (Chat-specific context)
   - README.md
   - checklist.md
2. Define Chat doc loading (conversations, messages, AI)
3. Add Chat-specific validation (business rules, owned entities)
4. Test with real Chat story

**API Workflow**:
1. Create `bmad/axon/workflows/api-workflow/`
   - workflow.yaml (extends story-implementation)
   - instructions.md (API-specific context)
   - README.md
   - checklist.md
2. Define API doc loading (FastEndpoints, validation)
3. Add API-specific validation (endpoints, contracts)
4. Test with real API story

**Deliverables**:
- ✅ 3 module-specific workflows
- ✅ Module context loading
- ✅ Specialized validation rules
- ✅ Real-world testing

**Estimated Effort**: 12-16 hours

---

### **Phase 5: Support Workflows** (Week 4)

**Goal**: Implement Tier 4 workflows (reusable components)

**Pre-Flight Validation Workflow**:
1. Create `bmad/axon/workflows/pre-flight-validation/`
   - workflow.yaml
   - instructions.md (parallel validation)
   - README.md
   - checklist.md
2. Extract reusable validation logic
3. Test integration with story-implementation

**Doc Sync Workflow**:
1. Create `bmad/axon/workflows/doc-sync/`
   - workflow.yaml
   - instructions.md (drift detection)
   - README.md
   - checklist.md
2. Implement autonomous doc sync
3. Test scheduled maintenance

**Deliverables**:
- ✅ 2 support workflows
- ✅ Reusable validation component
- ✅ Doc maintenance automation

**Estimated Effort**: 6-8 hours

---

### **Phase 6: Templates & Data Files** (Week 4-5)

**Goal**: Create supporting templates and data

**Templates**:
1. Complete story template (`templates/story-template.md`)
2. Story context template (`templates/story-context-template.json`)
3. Decision log template (`templates/decision-log-template.yaml`)
4. Doc update template (`templates/doc-update-template.md`)

**Data Files**:
1. Pattern catalog (`data/pattern-catalog.yaml`)
2. Library capabilities map (`data/library-capabilities.yaml`)
3. Module boundaries (`data/module-boundaries.yaml`)

**Deliverables**:
- ✅ 4 templates
- ✅ 3 data files
- ✅ All with real examples

**Estimated Effort**: 4-6 hours

---

### **Phase 7: Integration Testing** (Week 5)

**Goal**: End-to-end testing with real stories

**Test Scenarios**:
1. **Identity Feature Story**: "Add auto-revoke old credentials"
   - Test complete workflow
   - Verify 4 checkpoints
   - Check doc updates
   - Validate decision log

2. **Chat Feature Story**: "Add message ordering validation"
   - Test Chat workflow
   - Verify business rules compliance
   - Check owned entity patterns

3. **API Feature Story**: "Add new FastEndpoint for user search"
   - Test API workflow
   - Verify FastEndpoints patterns
   - Check validation integration

4. **Refactoring Story**: "Extract wallet verification to service"
   - Test refactoring workflow
   - Verify backward compatibility
   - Check all existing tests pass

5. **Bugfix Story**: "Fix credential duplicate check"
   - Test bugfix workflow
   - Verify root cause analysis
   - Check regression test added

**Deliverables**:
- ✅ 5 real stories tested
- ✅ All workflows validated
- ✅ Issues identified and fixed
- ✅ Performance metrics captured

**Estimated Effort**: 12-16 hours

---

### **Phase 8: Documentation & Refinement** (Week 5-6)

**Goal**: Complete documentation and final refinements

**Documentation**:
1. Complete `bmad/axon/README.md`
2. Document each workflow (README.md per workflow)
3. Document each agent (inline in agent files)
4. Create quick-start guide
5. Create troubleshooting guide

**Refinement**:
1. Review and optimize checkpoint timing
2. Optimize doc loading (reduce context size)
3. Refine agent coordination
4. Polish error messages
5. Add #yolo mode option

**Deliverables**:
- ✅ Complete documentation
- ✅ Quick-start guide
- ✅ Troubleshooting guide
- ✅ Performance optimizations
- ✅ #yolo mode support

**Estimated Effort**: 8-10 hours

---

### **Total Implementation Effort**

**Estimated Total**: 74-98 hours (2-2.5 weeks of full-time work)

**Critical Path**:
1. Phase 1: Foundation (prerequisite for all)
2. Phase 2: Agents (prerequisite for workflows)
3. Phase 3: Core Workflows (prerequisite for specialized)
4. Phase 4: Module Workflows (can parallelize)
5. Phase 5-8: Can parallelize with Phase 4

**Recommended Sequence**:
- Week 1: Phases 1-2 (Foundation + Agents)
- Week 2: Phase 3 (Core Workflows)
- Week 3: Phase 4 + 5 (Module + Support Workflows)
- Week 4: Phase 6 + 7 (Templates + Testing)
- Week 5: Phase 8 (Documentation + Polish)

---

## 📊 SUCCESS METRICS

### **Efficiency Gains** (3 months after implementation)

**AI Hallucination Reduction**:
- ✅ **80% reduction** in "AI invented non-existent method" errors
- ✅ **90% reduction** in "AI wrote manual code when library exists" errors
- ✅ **95% pattern compliance** (Result<T>, StrongId<T>, CQRS)

**Development Speed**:
- ✅ **2x faster** feature delivery with same/better quality
- ✅ **50% faster** refactoring (safe brownfield changes)
- ✅ **3x faster** bug fixes (root cause analysis + regression tests)

---

### **Quality Improvements**

**Documentation**:
- ✅ **Zero documentation drift** (docs updated with code)
- ✅ **100% doc-code alignment** (continuous sync)

**Testing**:
- ✅ **90%+ test coverage** (comprehensive test generation)
- ✅ **100% AC coverage** (one test per acceptance criterion)
- ✅ **100% build success rate** (validation before commit)

**Pattern Compliance**:
- ✅ **95%+ Result<T> usage** (no exceptions in domain)
- ✅ **95%+ StrongId<T> usage** (type-safe IDs)
- ✅ **100% CQRS compliance** (command/query separation)
- ✅ **100% domain event usage** (where applicable)

---

### **Developer Experience**

**Checkpoint Efficiency**:
- ✅ **4 strategic checkpoints** per story (not 21+)
- ✅ **11-18 minutes** total human review time
- ✅ **Trust AI** to work autonomously between checkpoints

**Context Continuity**:
- ✅ **Progressive doc loading** (no context overload)
- ✅ **Learning accumulation** (past mistakes captured)
- ✅ **Decision traceability** (YAML decision logs)

**Workflow Satisfaction**:
- ✅ **High confidence** in AI recommendations (doc-grounded)
- ✅ **Predictable outcomes** (pattern-compliant code)
- ✅ **Zero doc maintenance burden** (automated sync)

---

### **Measurable KPIs**

**Baseline (Before Axon)**:
- Stories completed per week: 3-4
- Bug rate: 15-20% of stories
- Doc drift incidents: 5-10 per month
- Manual code when library exists: 30-40% of features
- Pattern violations: 20-30% of code

**Target (After Axon, 3 months)**:
- Stories completed per week: 6-8 (2x improvement)
- Bug rate: 3-5% of stories (75% reduction)
- Doc drift incidents: 0-1 per month (90% reduction)
- Manual code when library exists: 5-10% (75% reduction)
- Pattern violations: 2-5% of code (90% reduction)

---

## 🎯 NEXT STEPS

### **Immediate Actions**

1. **Review & Approve Design**
   - Read complete design document
   - Validate against pain points from session.md
   - Approve or request changes

2. **Begin Implementation**
   - Start with Phase 1: Module Foundation
   - Create directory structure
   - Set up config.yaml

3. **Prioritize Agents**
   - Start with Story Orchestrator (Priority 1)
   - Then Doc Oracle (Priority 1)
   - Build remaining agents in sequence

4. **Test Early & Often**
   - Test each agent individually
   - Test workflows incrementally
   - Real stories as soon as possible

---

## 📝 DESIGN PRINCIPLES (SUMMARY)

1. **Doc-Grounded**: Every decision validated against `/Docs`
2. **Discovery-First**: Search before creating (Archaeologist)
3. **Library-Aware**: Use tools, not manual code (Library Sage)
4. **Pattern-Compliant**: Follow established patterns exactly (Doc Oracle)
5. **Checkpoint-Efficient**: 4 strategic approvals, batched
6. **Doc-Maintaining**: Zero drift (continuous sync)
7. **Learning-Enabled**: Capture decisions for future (Quality Guardian)
8. **Module-Aware**: Specialized workflows for Identity/Chat/API
9. **Brownfield-Safe**: Surgical, minimal changes (Implementation Surgeon)
10. **Human-in-the-Loop**: Strategic approvals, not micromanagement

---

## 🎉 CONCLUSION

This design provides a **complete, production-ready architecture** for the Axon Module, addressing all pain points identified in the original session.md while incorporating BMM's proven planning workflows.

**Key Achievements**:
✅ **Hybrid lifecycle** (BMM planning + Axon implementation)
✅ **6 specialized agents** with clear responsibilities
✅ **9 logical workflows** (master + core + module + support)
✅ **4 strategic checkpoints** (efficient, not bureaucratic)
✅ **Progressive doc loading** (hub-and-spoke pattern)
✅ **BMM-style story template** (full context)
✅ **Complete implementation roadmap** (8-phase plan)
✅ **Measurable success metrics** (2x faster, 90% compliance)

**Status**: 🚧 **IMPLEMENTATION IN PROGRESS**

**Current Phase**: ✅ **Phase 1 Complete** (2025-09-30)
- Module foundation created
- 745 lines of configuration and documentation
- All deliverables validated
- Production-ready foundation

**Next**: Begin Phase 2 (Agent Creation) - Story Orchestrator + Doc Oracle

---

**END OF DESIGN DOCUMENT**

**Document**: `Docs/PROCESS/bmad/axon-module-design-complete.md`
**Version**: 1.1 - Implementation in Progress
**Design Date**: 2025-09-29
**Implementation Started**: 2025-09-30
**Designer**: Valik + BMad Builder Agent
**Implementation Status**: Phase 1 Complete (12.5% total progress)

