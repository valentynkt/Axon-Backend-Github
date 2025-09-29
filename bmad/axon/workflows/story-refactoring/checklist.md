# Story Refactoring Workflow - Validation Checklist

## Refactoring Story Analysis

- [ ] Story file loaded and type confirmed as "Refactor"
- [ ] Refactoring scope identified (files, classes, methods)
- [ ] Refactoring rationale clear (why refactor?)
- [ ] Desired end state documented

## Usage Mapping (CRITICAL)

- [ ] ALL references to code being refactored found
- [ ] Direct usages mapped with file:line
- [ ] Indirect usages mapped (inheritance, composition)
- [ ] External module dependencies identified
- [ ] Test usages found
- [ ] Blast radius calculated (Small/Medium/Large)
- [ ] Usage map complete before continuing

## Impact Analysis

- [ ] Code being refactored listed (files, classes, methods, APIs)
- [ ] Public APIs identified (dangerous to change)
- [ ] Total references counted
- [ ] Modules affected listed
- [ ] Breaking change risk assessed (Low/Medium/High)
- [ ] Backward compatibility concerns documented
- [ ] Migration effort estimated
- [ ] Mitigation strategies defined
- [ ] Impact analysis saved

## Refactoring Plan

- [ ] Refactoring goal clearly stated
- [ ] Approach selected (Incremental/Big-bang/Hybrid)
- [ ] Phases defined with backward compatibility per phase
- [ ] Backward compatibility strategy documented:
  - [ ] Old APIs preserved OR justified why not
  - [ ] Deprecation warnings added if APIs changing
  - [ ] Migration guide provided if breaking changes
  - [ ] Adapter pattern used if needed
- [ ] Rollback plan documented:
  - [ ] Step-by-step reversal instructions
  - [ ] Files/database/config to restore listed
  - [ ] Time to rollback estimated
  - [ ] Data loss risk assessed
- [ ] Testing strategy defined (existing tests MUST pass)
- [ ] Refactoring plan saved

## Checkpoint 1.5: Refactoring Plan Approval

- [ ] Impact analysis displayed to user
- [ ] Refactoring plan displayed to user
- [ ] Blast radius, risk, compatibility shown
- [ ] User explicitly approved plan
- [ ] Workflow did not continue without approval

## Pre-Flight Validation (Same as story-implementation)

- [ ] Parallel validation completed (Archaeologist, Library Sage, Doc Oracle)
- [ ] Pre-flight package generated
- [ ] User approved pre-flight

## Implementation (Backward Compatibility Enforced)

- [ ] Refactoring phases followed from plan
- [ ] Old APIs preserved (if applicable)
- [ ] Deprecation warnings added (if applicable)
- [ ] Adapters/facades created for compatibility (if needed)
- [ ] Internal implementations updated
- [ ] Diff preview shows backward compatibility measures
- [ ] User approved implementation diff
- [ ] Changes applied
- [ ] Backward compatibility verified (old API still exists)

## Validation (Existing Tests MUST Pass)

- [ ] New tests generated for refactored code
- [ ] dotnet build executed and succeeded
- [ ] dotnet test executed for ALL tests (existing + new)
- [ ] **ALL existing tests passed** (MANDATORY - 0 failures)
- [ ] New tests passed
- [ ] Total test count: [existing + new]
- [ ] Test coverage ≥ 90%

### Backward Compatibility Validation (MANDATORY)

- [ ] Old APIs still callable: Yes
- [ ] Old behavior preserved: Yes
- [ ] No breaking changes detected
- [ ] Deprecation warnings present (if APIs changing)
- [ ] If breaking changes detected:
  - [ ] User acknowledged and chose action (rollback/fix/accept)
  - [ ] Migration guide updated if accepted

## Final Compliance Check

- [ ] Pattern compliance ≥ 95%
- [ ] Test coverage ≥ 90%
- [ ] AC coverage = 100%
- [ ] Build success = 100%
- [ ] **All existing tests pass = 100%** (MANDATORY)
- [ ] **Backward compatibility = 100%** (MANDATORY)
- [ ] **No breaking changes** (MANDATORY)
- [ ] All 6 quality gates passed

## Documentation

- [ ] Docs updated for refactored code
- [ ] Migration guide created (if breaking changes)
- [ ] Rollback plan documented and saved
- [ ] Decision log includes refactoring-specific fields:
  - [ ] Refactoring rationale
  - [ ] Blast radius
  - [ ] Backward compatibility measures
  - [ ] Rollback plan reference
  - [ ] Migration guide reference (if applicable)

## Checkpoint 4: Final Commit Approval

- [ ] Summary displayed with refactoring metrics
- [ ] All existing tests passed: ✅
- [ ] Backward compatible: ✅
- [ ] Rollback plan ready: ✅
- [ ] User approved commit

## Git Commit

- [ ] Commit created with "refactor(...)" prefix
- [ ] Commit message includes:
  - [ ] What was refactored
  - [ ] Impact summary (files, blast radius)
  - [ ] Backward compatible: ✅
  - [ ] All existing tests pass: ✅
  - [ ] Rollback plan reference
  - [ ] Test metrics
  - [ ] Quality gates (6/6)
- [ ] Commit verified

## Final Deliverables

- [ ] Refactored code in correct locations
- [ ] All tests (existing + new) in correct locations
- [ ] Impact analysis saved: {impact_analysis}
- [ ] Refactoring plan saved: {refactoring_plan}
- [ ] Rollback plan saved: {rollback_plan}
- [ ] Decision log saved: {decision_log_path}
- [ ] Migration guide created (if applicable)
- [ ] Implementation log created
- [ ] All docs updated

## Safety Checks PASSED

- [ ] ✅ All existing tests pass (0 failures)
- [ ] ✅ Backward compatible (old code still works)
- [ ] ✅ No breaking changes OR acknowledged
- [ ] ✅ Rollback plan ready
- [ ] ✅ Impact analysis complete
- [ ] ✅ Blast radius known

## Issues Found

### Critical Issues (Must fix before commit)
-

### Breaking Changes (Must address)
-

### Technical Debt Created
-