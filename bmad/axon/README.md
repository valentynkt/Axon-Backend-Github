# Axon Development Orchestrator

**Brownfield-aware AI development with library-first implementation for .NET + Clean Architecture + DDD + CQRS**

---

## 🎯 Overview

The **Axon Module** is a specialized BMAD implementation workflow system designed for brownfield .NET development with Clean Architecture + DDD + CQRS patterns. It extends BMM (BMAD Method Module) by providing implementation-focused workflows while leveraging BMM's proven planning capabilities.

### Key Innovation: Hybrid Lifecycle Approach

```
BMM Planning Phase
  ↓ (Product Brief → PRD → Tech Spec)
Axon Implementation Phase
  ↓ (Doc-grounded, Library-first, Pattern-compliant)
Production Code
```

---

## 🚀 Core Capabilities

### AI Pain Points Solved

1. ✅ **Discovery-First**: Searches codebase BEFORE creating (prevents hallucinations)
2. ✅ **Library-First**: Checks library capabilities before manual code
3. ✅ **Pattern-Compliant**: Validates against ADRs and established patterns
4. ✅ **Doc-Synchronized**: Zero documentation drift
5. ✅ **Checkpoint-Efficient**: 4 strategic checkpoints (11-18 min total)

---

## 🦸 Agents (6 Specialized)

| Agent | Role | Primary Responsibility |
|-------|------|----------------------|
| **Story Orchestrator** 🎯 | Master Coordinator | Routing, checkpoints, decisions |
| **Doc Oracle** 📚 | Documentation Intelligence | Progressive loading, ADR validation |
| **Archaeologist** 🔍 | Codebase Discovery | Search existing, map APIs, prevent reinvention |
| **Library Sage** 🛠️ | Library Expertise | Check capabilities, suggest approaches |
| **Implementation Surgeon** ⚙️ | Code Generation | Surgical changes, pattern compliance |
| **Quality Guardian** ✅ | Testing & Validation | Test generation, doc sync, learning capture |

---

## 📋 Workflows (9 Logical)

### Tier 1: Master Orchestration
- **story-orchestrator** - Routes stories to appropriate workflow

### Tier 2: Core Implementation
- **story-implementation** - Feature development (4-phase)
- **story-refactoring** - Safe brownfield refactoring
- **story-bugfix** - Root cause analysis + fix

### Tier 3: Module-Specialized
- **identity-workflow** - Identity module (auth, wallets, principals)
- **chat-workflow** - Chat module (conversations, messages, AI)
- **api-workflow** - API/FastEndpoints specialization

### Tier 4: Support
- **pre-flight-validation** - Reusable discovery + library + pattern validation
- **doc-sync** - Autonomous documentation maintenance

---

## 🎯 4 Strategic Checkpoints

| # | Checkpoint | Time | Purpose |
|---|------------|------|---------|
| 1 | Understanding Approval | 1 min | Verify story comprehension |
| 2 | Pre-Flight Approval | 3-5 min | Validate discovery + library + pattern |
| 3 | Implementation Review | 5-10 min | Review generated code |
| 4 | Commit Approval | 2 min | Final validation before commit |

**Total**: 11-18 minutes human review per story

---

## 📚 Installation

```bash
# Module is located at:
bmad/axon/

# Configuration:
bmad/axon/config.yaml

# To load an agent:
Load: bmad/axon/agents/{agent-name}.md

# To run a workflow:
Execute: bmad/axon/workflows/{workflow-name}/workflow.yaml
```

---

## 🔥 Quick Start

### Using Story Orchestrator (Main Entry Point)

```
1. Load the Story Orchestrator agent
2. Provide a story.md file (see templates/story-template.md)
3. Agent analyzes and routes to appropriate workflow
4. Follow 4 strategic checkpoints
5. Receive: Code + Tests + Updated Docs + Decision Log
```

### Example Story Flow

```
Story: "Add wallet auto-revocation after 90 days"
  ↓
Story Orchestrator analyzes:
  - Type: Feature
  - Module: Identity
  ↓
Routes to: identity-workflow
  ↓
Phase 0: Understanding (Doc Oracle loads Identity docs)
  ✅ Checkpoint 1: Understanding Approval
  ↓
Phase 1: Pre-Flight (Parallel execution)
  - Archaeologist: Search existing revocation logic
  - Library Sage: Check if library supports auto-revocation
  - Doc Oracle: Validate against WalletOwnership owned entity pattern
  ✅ Checkpoint 2: Pre-Flight Approval
  ↓
Phase 2: Implementation
  - Implementation Surgeon: Generate code (surgical, minimal)
  - Detect doc drift
  ✅ Checkpoint 3: Implementation Review
  ↓
Phase 3: Validation
  - Quality Guardian: Generate tests (90%+ coverage)
  - Run build + tests
  - Update docs (zero drift)
  - Capture decision log
  ✅ Checkpoint 4: Commit Approval
  ↓
Output:
  - src/Modules/Identity/Domain/Entities/WalletOwnership.cs (updated)
  - tests/Modules/Identity/Domain/WalletOwnershipTests.cs (new)
  - Docs/ENGINEERING/modules/identity/01-domain-model.md (updated)
  - Docs/PROCESS/active-stories/decisions/story-123-decisions.yaml
```

---

## 📁 Module Structure

```
bmad/axon/
├── config.yaml                       # Complete module configuration
├── README.md                         # This file
│
├── agents/                           # 6 specialized agents
│   ├── axon-story-orchestrator.md
│   ├── axon-doc-oracle.md
│   ├── axon-archaeologist.md
│   ├── axon-library-sage.md
│   ├── axon-implementation-surgeon.md
│   └── axon-quality-guardian.md
│
├── workflows/                        # 9 workflows
│   ├── story-orchestrator/
│   ├── story-implementation/
│   ├── story-refactoring/
│   ├── story-bugfix/
│   ├── identity-workflow/
│   ├── chat-workflow/
│   ├── api-workflow/
│   ├── pre-flight-validation/
│   └── doc-sync/
│
├── templates/                        # Story and context templates
│   ├── story-template.md
│   ├── story-context-template.json
│   ├── decision-log-template.yaml
│   └── doc-update-template.md
│
├── data/                            # Supporting data
│   ├── pattern-catalog.yaml
│   ├── library-capabilities.yaml
│   └── module-boundaries.yaml
│
└── _module-installer/               # Installation config
    └── install-module-config.yaml
```

---

## 🔧 Configuration

Key settings in `config.yaml`:

```yaml
axon_settings:
  checkpoint_count: 4
  yolo_mode_available: true           # Skip checkpoints for trusted stories
  progressive_doc_loading: true       # Hub-and-spoke doc loading
  pattern_validation_strict: true     # Enforce Result<T>, StrongId<T>, CQRS
  library_first_enforcement: true     # Check library before manual code
  discovery_first_mandate: true       # Search before creating
```

---

## 📊 Success Metrics (Target)

### Efficiency Gains
- **80% reduction** in AI hallucination errors
- **90% reduction** in manual code when library exists
- **95% pattern compliance** (Result<T>, StrongId<T>, CQRS)
- **2x faster** feature delivery

### Quality Improvements
- **Zero documentation drift** (continuous sync)
- **90%+ test coverage** (comprehensive test generation)
- **100% AC coverage** (one test per acceptance criterion)
- **100% build success rate** (validation before commit)

### Developer Experience
- **4 strategic checkpoints** (not 21+)
- **11-18 minutes** average human review per story
- **Progressive doc loading** (no context overload)

---

## 🔗 Integration with BMM

**Optional but Recommended:**

```
Phase 1: Planning (BMM)
  @analyst → Brainstorm + Product Brief
  @po → PRD (Requirements + Epics)
  @architect → Tech Spec (Architecture + Design)
  ↓
  Output: tech-spec.md

Phase 2: Implementation (Axon)
  @axon-story-orchestrator → Story Breakdown
  @axon-* agents → Implementation
  ↓
  Output: Code + Tests + Docs + Decisions

Phase 3: Production
  Deployed code with full documentation
```

**Handoff Format**: tech-spec.md sections → Story template references

---

## 📖 Documentation

### Core Documentation (Always Load)
- `Docs/ENGINEERING/00-START-HERE.md`
- `Docs/ENGINEERING/guides/patterns/00-QUICK-REFERENCE.md` 🔥
- `Docs/Libraries/00-INDEX.md`

### Module Documentation (Context-Loaded)
- **Identity**: 5 docs (domain, auth, API, schema)
- **Chat**: 5 docs (domain, flows, API, schema)

### Pattern Documentation
- `Docs/ENGINEERING/guides/patterns/cqrs.md`
- `Docs/ENGINEERING/guides/patterns/domain-modeling.md`

### ADRs (6 Architectural Decisions)
- 001: Modular Monolith
- 002: CQRS + MediatR
- 003: Result Pattern
- 004: Strong IDs
- 005: PostgreSQL
- 006: FastEndpoints

---

## 🛠️ Development Status

**Current Phase**: Phase 1 - Foundation ✅
**Version**: 1.0.0
**Last Updated**: 2025-09-30

**Implementation Roadmap**:
- ✅ Phase 1: Module Foundation (Complete)
- ⏳ Phase 2: Agent Creation (6 agents)
- ⏳ Phase 3: Core Workflows (4 workflows)
- ⏳ Phase 4: Module Workflows (3 workflows)
- ⏳ Phase 5: Support Workflows (2 workflows)
- ⏳ Phase 6: Templates & Data
- ⏳ Phase 7: Integration Testing
- ⏳ Phase 8: Documentation & Polish

---

## 🤝 Contributing

To extend this module:

1. **Add new agents** using BMAD Core v6 agent XML format
2. **Add new workflows** using BMAD workflow.yaml structure
3. **Update config.yaml** with new component counts
4. **Test with real stories** (Identity, Chat, API examples)

---

## 📋 Design Document

Complete design specification:
`Docs/PROCESS/bmad/axon-module-design-complete.md` (1,626 lines)

---

## 👤 Author

**Created by**: Valik
**Date**: 2025-09-30
**BMAD Version**: 6.0.0
**Design**: Hybrid BMM + Axon approach

---

## 📜 License

Part of Axon Backend project.

---

**🚀 Ready to transform brownfield AI development with doc-grounded, library-first, pattern-compliant workflows!**