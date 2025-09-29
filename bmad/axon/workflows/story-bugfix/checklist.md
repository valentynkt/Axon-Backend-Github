# Story Bugfix Workflow - Validation Checklist

## Bug Story Analysis

- [ ] Story file loaded and type confirmed as "Bugfix"
- [ ] Bug description extracted
- [ ] Reproduction steps documented
- [ ] Expected vs actual behavior clear
- [ ] Error logs/stack traces captured (if available)
- [ ] Affected module identified

## Bug Reproduction

- [ ] Reproduction steps followed
- [ ] Bug reproduced successfully
- [ ] Reproduction consistency documented (Always/Sometimes/Once)
- [ ] Environment noted (Local/Test/Production)
- [ ] Error message captured (exact)
- [ ] Stack trace captured (if available)
- [ ] Bug MUST be reproducible to continue

## Checkpoint 1: Bug Understanding Approval

- [ ] Bug understanding summary displayed
- [ ] Expected vs actual behavior shown
- [ ] User confirmed bug reproduction
- [ ] Workflow did not continue without approval

## Root Cause Analysis

### Faulty Code Location
- [ ] Error logs/stack trace parsed for file:line
- [ ] Faulty method/class identified
- [ ] Related code searched in codebase
- [ ] Call chain mapped
- [ ] Code snippet captured (10 lines context)
- [ ] Dependencies identified
- [ ] Callers identified

### Root Cause Determination
- [ ] Root cause category identified:
  - [ ] Logic Error
  - [ ] Data Error
  - [ ] State Error
  - [ ] Race Condition
  - [ ] Integration Error
  - [ ] Configuration Error
- [ ] Clear explanation of WHY bug exists
- [ ] Faulty code shown
- [ ] Explanation of what's wrong
- [ ] Explanation of correct behavior
- [ ] Impact assessed (severity, affected users, risks)
- [ ] Root cause analysis saved

## Fix Strategy

- [ ] Minimal fix approach designed
- [ ] Smallest change identified
- [ ] No unnecessary refactoring
- [ ] Edge cases identified
- [ ] Changes required listed (file, lines, reason)
- [ ] Minimal fix rationale documented
- [ ] Regression test approach defined
- [ ] Fix strategy saved

## Checkpoint 2: Fix Strategy Approval

- [ ] Root cause analysis displayed
- [ ] Fix strategy displayed
- [ ] User approved fix strategy
- [ ] Workflow did not continue without approval

## Regression Test (Test-Driven Bugfix)

- [ ] Regression test generated FIRST (before fix)
- [ ] Test reproduces the bug
- [ ] Test location: tests/Modules/{{module}}/Regression/
- [ ] Test name: Bug_{{BugId}}_{{Description}}
- [ ] Given-When-Then format used
- [ ] Regression test executed BEFORE fix
- [ ] **Test FAILED before fix** (confirms bug exists)
- [ ] Regression test saved

## Fix Implementation

- [ ] Fix code generated following fix strategy
- [ ] Changes minimal (only faulty lines)
- [ ] Existing behavior preserved (except bug)
- [ ] Patterns applied (Result<T>, etc.)
- [ ] Diff preview generated
- [ ] Before/after shown for each file
- [ ] Minimal nature of changes verified

## Checkpoint 3: Fix Implementation Approval

- [ ] Fix diff displayed
- [ ] Regression test shown
- [ ] User approved fix implementation
- [ ] Workflow did not continue without approval
- [ ] Fix changes applied

## Regression Testing

- [ ] Regression test executed AFTER fix
- [ ] **Test PASSED after fix** (bug is fixed)
- [ ] If test failed, fix updated and retried
- [ ] Test result comparison:
  - [ ] Before fix: ❌ FAIL
  - [ ] After fix: ✅ PASS

## Full Test Suite Validation

- [ ] dotnet build executed and succeeded
- [ ] dotnet test executed for ALL tests
- [ ] **ALL existing tests passed** (MANDATORY)
- [ ] No new bugs introduced
- [ ] If existing tests failed:
  - [ ] Fix adjusted to not break behavior OR
  - [ ] Tests updated if intentional behavior change

## Bug Verification

- [ ] Original reproduction steps re-executed
- [ ] Bug no longer reproduces
- [ ] Edge cases tested
- [ ] Expected behavior confirmed
- [ ] Verification summary generated

## Final Compliance Check

- [ ] ✅ Bug reproduced: true
- [ ] ✅ Root cause identified: true
- [ ] ✅ Regression test added: true
- [ ] ✅ Regression test passes: true
- [ ] ✅ All existing tests pass: 100%
- [ ] ✅ Bug is fixed: true
- [ ] ✅ No new bugs introduced: true

## Documentation

- [ ] Known issues list updated (bug removed)
- [ ] Troubleshooting guide updated (if relevant)
- [ ] API docs updated (if bug was in public API)
- [ ] Decision log includes bugfix-specific fields:
  - [ ] Bug details (description, reproduction, severity)
  - [ ] Root cause (category, explanation, faulty code)
  - [ ] Fix applied (approach, files, lines)
  - [ ] Regression test (name, file, before/after)
  - [ ] Learning (why happened, prevention)

## Checkpoint 4: Final Commit Approval

- [ ] Bugfix summary displayed
- [ ] Root cause, fix approach shown
- [ ] Regression test PASS shown
- [ ] All existing tests PASS shown
- [ ] User approved commit

## Git Commit

- [ ] Commit created with "fix(...)" prefix
- [ ] Commit message includes:
  - [ ] Bug description
  - [ ] Root cause (category, location, explanation)
  - [ ] Fix (approach, files, lines)
  - [ ] Regression test (name, before/after)
  - [ ] All existing tests PASS
- [ ] Commit verified

## Final Deliverables

- [ ] Fixed code in correct locations
- [ ] Regression test in tests/Modules/{{module}}/Regression/
- [ ] Root cause analysis saved: {root_cause_analysis}
- [ ] Fix strategy saved: {fix_strategy}
- [ ] Regression test doc saved: {regression_test}
- [ ] Decision log saved: {decision_log_path}
- [ ] Implementation log created
- [ ] All docs updated

## Learning Captured

- [ ] Why bug happened documented
- [ ] Prevention strategy documented
- [ ] Similar bugs to check listed
- [ ] Metrics captured (fix time, test added, tests pass)

## Issues Found

### Critical Issues (Must fix)
-

### New Bugs Introduced (Must fix)
-

### Learning Points
-