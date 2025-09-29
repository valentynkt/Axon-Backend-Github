# Story Refactoring - Workflow Instructions

<workflow>

<critical>The workflow execution engine is governed by: {project-root}/bmad/core/tasks/workflow.md</critical>
<critical>You MUST have already loaded and processed: {project-root}/bmad/axon/workflows/story-refactoring/workflow.yaml</critical>
<critical>This workflow extends story-implementation with EXTRA SAFETY MEASURES for brownfield refactoring</critical>

## Overview

Refactoring in brownfield codebases is **DANGEROUS**. This workflow adds:
- ✅ **Impact Analysis**: Map ALL usages of code being refactored
- ✅ **Backward Compatibility**: Design changes to not break existing code
- ✅ **Rollback Plan**: How to undo if things go wrong
- ✅ **Extra Validation**: All existing tests MUST still pass
- ✅ **5 Checkpoints**: One extra for refactoring plan approval

## Phases

### Phase 0: Story Understanding + Refactoring Plan
### Phase 1: Impact Analysis + Pre-Flight Validation
### Phase 2: Implementation (with backward compatibility)
### Phase 3: Validation (existing tests MUST pass)

---

<step n="0" goal="Initialize and load story (same as story-implementation step 0)">
<action>Read story file, parse metadata, set workflow context</action>
<action>Confirm story type is "Refactor"</action>
</step>

<step n="1" goal="Load core documentation (same as story-implementation step 1)">
<action>Load core hub + module-specific docs based on context</action>
</step>

<step n="2" goal="Understand refactoring requirements">
<action>Analyze refactoring story:
  - What code needs refactoring? (files, classes, methods)
  - Why refactor? (technical debt, performance, maintainability)
  - What's the desired end state?
  - What must NOT change? (public APIs, behavior)
</action>

<action>Identify refactoring scope:
  - Files affected
  - Classes/methods being changed
  - Public vs private APIs
  - External dependencies
</action>
</step>

<step n="3" goal="Map ALL usages of code to refactor (Archaeologist - CRITICAL)">
<action>For each file/class/method being refactored:
  1. Find ALL references in codebase
  2. Map direct usages
  3. Map indirect usages (via inheritance, composition)
  4. Map external usages (from other modules)
  5. Map test usages
</action>

<action>Generate usage map:
  - Total references: [Count]
  - Direct usages: [List with file:line]
  - Indirect usages: [List with file:line]
  - External module dependencies: [List]
  - Tests affected: [List]
  - **Blast Radius**: [Small/Medium/Large]
</action>

<critical>ALL usages MUST be mapped before continuing</critical>
</step>

<step n="4" goal="Impact analysis">
<template-output section="impact_analysis">
**Impact Analysis Report**

**Code Being Refactored:**
- Files: [List]
- Classes: [List]
- Methods: [List]
- Public APIs: [List - these are dangerous to change]

**Usage Analysis:**
- Total references: [Count]
- Modules affected: [List]
- External dependencies: [List]
- Blast radius: [Small/Medium/Large]

**Risk Assessment:**
- Breaking change risk: [Low/Medium/High]
- Backward compatibility concerns: [List]
- Migration effort: [Low/Medium/High]

**Mitigation Strategies:**
- Strategy 1: [e.g., Deprecate old API, add new API]
- Strategy 2: [e.g., Use adapter pattern for compatibility]
- Strategy 3: [e.g., Phase refactoring over 2+ stories]
</template-output>

<critical>Save to {impact_analysis}</critical>
</step>

<step n="5" goal="Design refactoring plan with backward compatibility">
<action>Design refactoring approach:
  1. Identify steps (refactor in stages, not all-at-once)
  2. Ensure backward compatibility (old code still works)
  3. Plan deprecation path (if public APIs change)
  4. Design adapter/facade if needed
  5. Plan migration strategy for consumers
</action>

<template-output section="refactoring_plan">
**Refactoring Plan**

**Goal:** [Clear statement of desired end state]

**Approach:** [Incremental/Big-bang/Hybrid]

**Phases:**
1. Phase 1: [What changes, backward compatible?]
2. Phase 2: [What changes, backward compatible?]
3. Phase 3: [Final cleanup, deprecation removal]

**Backward Compatibility:**
- Old APIs preserved: [Yes/No - if No, justify]
- Deprecation warnings added: [Yes/No]
- Migration guide provided: [Yes/No]
- Adapter pattern used: [Yes/No - describe if Yes]

**Rollback Plan:**
- How to undo: [Step-by-step reversal]
- What to restore: [Files, database, config]
- Time to rollback: [Estimate]
- Data loss risk: [Yes/No]

**Testing Strategy:**
- All existing tests MUST pass: ✅
- New tests for refactored code: [List]
- Integration tests for backward compatibility: [List]
- Manual testing needed: [List if any]
</template-output>

<critical>Save to {refactoring_plan}</critical>
</step>

---

## ✅ CHECKPOINT 1.5: REFACTORING PLAN APPROVAL (3-5 min)

<step n="6" goal="Checkpoint 1.5: User approves refactoring plan">
<action>Display impact analysis + refactoring plan from steps 4-5</action>
<ask critical="true">
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ CHECKPOINT 1.5: REFACTORING PLAN APPROVAL
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Review the refactoring plan above.

Impact:
- Blast radius: {{blast_radius}}
- Breaking change risk: {{breaking_change_risk}}
- Backward compatible: {{backward_compatible}}

Do you approve this refactoring plan?
- [c] Continue to Pre-Flight
- [e] Edit plan
- [a] Abort refactoring

Your choice:
</ask>

<action if="user_response == 'e'">
  <ask>What needs adjustment?</ask>
  <action>Update refactoring plan</action>
  <goto step="5">Regenerate plan</goto>
</action>

<critical>User MUST approve refactoring plan before continuing</critical>
</step>

---

## PHASE 1: PRE-FLIGHT VALIDATION (Similar to story-implementation, but with extra checks)

<step n="7" goal="Pre-flight validation (same as story-implementation step 5-6)">
<action>Run parallel validation: Archaeologist, Library Sage, Doc Oracle</action>
<action>Generate pre-flight package</action>
</step>

<step n="8" goal="Checkpoint 2: Pre-flight approval (same as story-implementation step 7)">
<ask>Approve pre-flight package?</ask>
</step>

---

## PHASE 2: IMPLEMENTATION (With backward compatibility enforcement)

<step n="9" goal="Generate implementation with backward compatibility">
<action>Generate refactoring implementation:
  1. Follow refactoring plan phases
  2. Preserve old APIs (if applicable)
  3. Add deprecation warnings
  4. Create adapters/facades for compatibility
  5. Update internal implementations
</action>

<action>Generate diff preview showing:
  - Old API preservation
  - New implementation
  - Backward compatibility measures
  - Deprecation warnings
</action>
</step>

<step n="10" goal="Checkpoint 3: Implementation preview approval">
<ask>Approve refactoring implementation diff?</ask>
</step>

<step n="11" goal="Apply refactoring changes">
<action>Apply all changes from approved diff</action>
<action>Verify backward compatibility by checking old API still exists</action>
</step>

---

## PHASE 3: VALIDATION (Existing tests MUST pass)

<step n="12" goal="Generate new tests for refactored code">
<action>Generate tests for refactored code (same as story-implementation step 12)</action>
</step>

<step n="13" goal="Run ALL tests (existing + new)">
<action>Execute dotnet build</action>
<action>Execute dotnet test (ALL tests, not just new ones)</action>

<critical>ALL existing tests MUST pass. If any fail:
  1. Analyze failure root cause
  2. Fix refactored code to maintain behavior
  3. Re-run tests until 100% pass
  4. NEVER change existing tests to "make them pass"
</critical>

<action>Generate test results:
  - Total tests: [Count]
  - Existing tests: [Count - these MUST all pass]
  - New tests: [Count]
  - Passed: [Count]
  - Failed: [Count - MUST be 0]
  - Skipped: [Count]
</action>
</step>

<step n="14" goal="Backward compatibility validation">
<action>Verify backward compatibility:
  1. Old APIs still callable: [Yes/No]
  2. Old behavior preserved: [Yes/No]
  3. No breaking changes: [Yes/No]
  4. Deprecation warnings present: [Yes/No]
</action>

<action if="breaking_changes_detected">
  <action>HALT - refactoring introduced breaking changes</action>
  <ask>Breaking changes detected! Options:
    1. Rollback refactoring
    2. Fix to maintain backward compatibility
    3. Acknowledge breaking change and update migration guide
  </ask>
</action>
</step>

<step n="15" goal="Final compliance check (same as story-implementation step 15)">
<action>Validate all quality gates + backward compatibility gate</action>
</step>

<step n="16" goal="Documentation sync (same as story-implementation step 16)">
<action>Update docs, add migration guide if needed</action>
</step>

<step n="17" goal="Capture refactoring decisions">
<action>Generate decision log with refactoring-specific fields:
  - Refactoring rationale
  - Blast radius
  - Backward compatibility measures
  - Rollback plan
  - Migration guide (if applicable)
</action>
</step>

---

## ✅ CHECKPOINT 4: FINAL COMMIT APPROVAL

<step n="18" goal="Checkpoint 4: Final commit approval">
<action>Display summary with refactoring metrics</action>
<ask>
Ready to commit refactoring?
- All existing tests passed: ✅
- Backward compatible: ✅
- Rollback plan ready: ✅

Proceed?
</ask>
</step>

<step n="19" goal="Create git commit with refactoring tag">
<action>Create commit with message format:

```
refactor({{module}}): {{story_title}}

Refactors {{what_was_refactored}}

Impact:
- Files changed: {{change_count}}
- Blast radius: {{blast_radius}}
- Backward compatible: ✅
- All existing tests pass: ✅

Rollback: See {rollback_plan}

Tests: {{test_count}} tests, {{coverage_percent}}% coverage
Quality Gates: ✅ 6/6 passed (includes backward compatibility)

🤖 Generated with Axon Module (BMAD)
Co-Authored-By: {{user_name}}
```
</action>
</step>

<step n="20" goal="Final summary with rollback instructions">
<action>Generate completion report including:
  - Refactoring summary
  - Rollback plan location
  - Migration guide (if applicable)
  - All quality gates passed
</action>
</step>

</workflow>