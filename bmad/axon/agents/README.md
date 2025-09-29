# Axon Agents

**Status**: 🚧 Phase 2 - Agent Creation (Pending)

---

## Overview

This directory will contain 6 specialized Axon agents that work together to implement brownfield-aware, library-first, pattern-compliant development workflows.

---

## Planned Agents (6)

### Priority 1 Agents

#### 1. **Axon Story Orchestrator** 🎯
- **File**: `axon-story-orchestrator.md`
- **Role**: Master coordinator for story lifecycle
- **Responsibilities**:
  - Parse story + acceptance criteria
  - Load BMM tech-spec reference (if exists)
  - Route to appropriate workflow
  - Manage 4 strategic checkpoints
  - Capture decisions and learning
- **Commands**: 5 commands planned
- **Status**: ⏳ Pending

#### 2. **Axon Doc Oracle** 📚
- **File**: `axon-doc-oracle.md`
- **Role**: Documentation intelligence & validation specialist
- **Responsibilities**:
  - Progressive doc loading (hub-and-spoke)
  - Validate against ADRs
  - Detect doc drift
  - Pattern compliance checking
- **Commands**: 5 commands planned
- **Status**: ⏳ Pending

### Priority 2 Agents

#### 3. **Axon Archaeologist** 🔍
- **File**: `axon-archaeologist.md`
- **Role**: Codebase discovery specialist
- **Responsibilities**:
  - Search existing implementations
  - Map available APIs
  - Find reusable patterns
  - Prevent reinvention
- **Commands**: 5 commands planned
- **Status**: ⏳ Pending

#### 4. **Axon Library Sage** 🛠️
- **File**: `axon-library-sage.md`
- **Role**: Library-first implementation specialist
- **Responsibilities**:
  - Check library capabilities
  - Suggest out-of-box solutions
  - Prevent manual reimplementation
  - Ensure correct library usage
- **Commands**: 5 commands planned
- **Status**: ⏳ Pending

### Priority 3 Agents

#### 5. **Axon Implementation Surgeon** ⚙️
- **File**: `axon-implementation-surgeon.md`
- **Role**: Precise code generation expert
- **Responsibilities**:
  - Generate code after validation
  - Extend existing code surgically
  - Follow patterns exactly
  - Make minimal brownfield-safe changes
- **Commands**: 6 commands planned
- **Status**: ⏳ Pending

#### 6. **Axon Quality Guardian** ✅
- **File**: `axon-quality-guardian.md`
- **Role**: Testing & validation specialist
- **Responsibilities**:
  - Generate comprehensive tests
  - Validate against acceptance criteria
  - Run build & tests
  - Capture learning
  - Update docs
- **Commands**: 7 commands planned
- **Status**: ⏳ Pending

---

## Implementation Timeline

**Phase 2 Estimated Effort**: 12-16 hours

**Priority Order**:
1. Story Orchestrator (Priority 1) - Main entry point
2. Doc Oracle (Priority 1) - Required for all workflows
3. Archaeologist (Priority 2) - Discovery-first mandate
4. Library Sage (Priority 2) - Library-first enforcement
5. Implementation Surgeon (Priority 3) - Code generation
6. Quality Guardian (Priority 3) - Testing & validation

---

## Agent Format

All agents will follow **BMAD Core v6** XML format:

```xml
<agent id="bmad/axon/agents/{agent-name}.md" name="{Agent Name}" title="{Agent Title}" icon="🎯">
  <activation critical="MANDATORY">
    <!-- Activation instructions -->
  </activation>

  <persona>
    <role>Agent role description</role>
    <identity>Agent identity</identity>
    <communication_style>Communication style</communication_style>
  </persona>

  <cmds>
    <!-- Agent commands -->
  </cmds>
</agent>
```

---

## Documentation References

**Design Document**: `Docs/PROCESS/bmad/axon-module-design-complete.md`

**Agent Specifications**: See Section "🦸 AGENT SPECIFICATIONS" in design doc

**Configuration**: See `bmad/axon/config.yaml` → `agents` section

---

## Next Steps

1. Review agent specifications in design document
2. Start with Story Orchestrator agent (Priority 1)
3. Implement commands using BMAD Core v6 format
4. Test agent in isolation
5. Proceed to next priority agent

---

**Created**: 2025-09-30
**Status**: Phase 2 Pending
**Total Agents Planned**: 6
**Total Commands Planned**: 33