# Doc-Sync - Documentation Synchronization Instructions

<workflow>

<critical>Governed by: {project-root}/bmad/core/tasks/workflow.md</critical>
<critical>Loaded config: {project-root}/bmad/axon/workflows/doc-sync/workflow.yaml</critical>
<critical>REUSABLE: Invoked by all implementation workflows to maintain zero documentation drift</critical>

## Overview

Reusable documentation synchronization workflow that detects drift and generates targeted updates to maintain alignment between code and documentation.

**Purpose**: Zero documentation drift

**Invoked by**: story-implementation (Phase 3), story-refactoring (Phase 3), story-bugfix (Phase 3), module workflows (Enhancement Point 4)

**Duration**: 5-10 minutes

---

<step n="0" goal="Initialize doc-sync context">
<action>Load {installed_path}/workflow.yaml</action>
<action>Receive inputs from invoking workflow:
- story_context (story ID, module context)
- code_changes (list of changed files)
- implementation_summary (what was implemented)
- patterns_used (Result<T>, StrongId<T>, CQRS, etc.)
</action>
<action>Identify affected documentation layers from workflow.yaml:
- Identity: 5 module docs
- Chat: 5 module docs
- API: 2 API docs
</action>
</step>

---

## DRIFT DETECTION

<step n="1" goal="Detect documentation drift (@axon-doc-oracle)">
<action>Invoke @axon-doc-oracle with task: detect-doc-drift.md

**4 Drift Types** (from workflow.yaml: drift_types):
1. **Missing**: Documentation doesn't exist for code that exists
2. **Outdated**: Documentation exists but describes old behavior
3. **Incorrect**: Documentation contradicts actual code behavior
4. **Orphaned**: Documentation exists for code that no longer exists

**Detection Strategy**:
- Compare code_changes against documentation
- Check each affected doc layer (Identity/Chat/API)
- Identify drift instances with severity (Critical | High | Medium | Low)
</action>

<output format="YAML" path="{output_folder}/{story_id}/drift-detection.yaml">
drift_summary:
  missing_count: [Count]
  outdated_count: [Count]
  incorrect_count: [Count]
  orphaned_count: [Count]
  total_count: [Count]

drifts_detected:
  - drift_type: Missing | Outdated | Incorrect | Orphaned
    description: [What's missing/wrong]
    code_location: [file:line where code exists]
    doc_location: [file:section where doc should be]
    severity: Critical | High | Medium | Low
    recommendation: [How to fix]

affected_docs:
  - doc_path: [Documentation file path]
    sections_affected: [Which sections need updates]
    update_type: New | Modify | Delete

overall_severity: Critical | High | Medium | Low
</output>
</step>

---

## DOC UPDATE GENERATION

<step n="2" goal="Generate documentation updates (@axon-doc-oracle)">
<action>Invoke @axon-doc-oracle with task: suggest-doc-updates.md

**For each drift instance**:
- Generate before/after documentation
- Provide clear reason for change
- Reference code location (file:line)
- Specify update type (New | Modify | Delete)

**Documentation Update Template** (from workflow.yaml: doc_update_recommendations.template_path):
- Use: bmad/axon/templates/doc-update-template.md
</action>

<output format="Markdown" path="{output_folder}/{story_id}/doc-updates.md">
# Documentation Updates for {story_id}

## Changes Summary
- [What changed in code]
- [Which files modified]
- [Patterns applied]

## Documentation Updates

### Update 1: [Doc file path]

**Section**: [Section name]

**Before**:
```markdown
[Old documentation text]
```

**After**:
```markdown
[New documentation text]
```

**Reason**: [Why this update is needed]

**Code Reference**: [file:line]

**Update Type**: New | Modify | Delete

---

[Repeat for each drift instance]

## Inline XML Updates

### File: [Code file path]

**Add XML comments**:
```csharp
/// <summary>
/// [Method/class description]
/// </summary>
/// <param name="paramName">[Parameter description]</param>
/// <returns>[Return value description]</returns>
```

**Location**: [file:line]

---

## Validation Checklist
- [ ] All drift instances addressed
- [ ] Before/after documentation reviewed
- [ ] Code references accurate
- [ ] XML comments complete
</output>
</step>

---

## INLINE XML DOCUMENTATION

<step n="3" goal="Validate inline XML coverage (@axon-quality-guardian)">
<action>Invoke @axon-quality-guardian with task: sync-docs.md

**Check XML documentation**:
- All public classes have <summary>
- All public methods have <summary>, <param>, <returns>
- All public properties have <summary>
- All public APIs documented (FastEndpoints endpoints)

**Generate missing XML comments** using patterns:
- Classes: Purpose, responsibilities, usage examples
- Methods: What it does, parameters, return value, exceptions (if any)
- Properties: What it represents, constraints
</action>

<action>Add inline XML updates to doc-updates.md</action>
</step>

---

## VALIDATION

<step n="4" goal="Validate documentation completeness">
<action>Validate all drift addressed:
- drift_detection.yaml: Total count
- doc-updates.md: Updates generated for each drift
- Coverage: 100% (all drift instances have updates)
</action>

<action>Validate documentation quality:
- Updates are minimal (only what changed)
- Updates are targeted (specific sections)
- Updates reference code (file:line)
- XML comments follow conventions
</action>

<critical>Zero drift = Zero drift instances in drift-detection.yaml OR all drift addressed in doc-updates.md</critical>
</step>

---

## COMPLETION

<step n="5" goal="Generate summary and return">
<output section="doc_sync_summary">
**Documentation Synchronization Complete** ✅

**Drift Detection**:
- Total Drift: [Count]
- Missing: [Count]
- Outdated: [Count]
- Incorrect: [Count]
- Orphaned: [Count]
- Severity: [Critical | High | Medium | Low]

**Documentation Updates Generated**:
- Affected Docs: [Count] files
- Update Types: [New: X, Modify: Y, Delete: Z]
- Inline XML: [Count] comments to add/update

**Zero Drift Achieved**: Yes | No (if No, explain)

**Files to Update**:
- [List of documentation files to edit]
- [List of code files to add XML comments]
</output>

<action>Return control to invoking workflow with:
- drift-detection.yaml (drift report)
- doc-updates.md (update recommendations)
- Doc sync summary
- Zero drift status (Yes | No)
</action>

<critical>Invoking workflow will apply documentation updates before final commit</critical>
</step>

</workflow>