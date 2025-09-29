# Story Bugfix - Workflow Instructions

<workflow>

<critical>The workflow execution engine is governed by: {project-root}/bmad/core/tasks/workflow.md</critical>
<critical>You MUST have already loaded and processed: {project-root}/bmad/axon/workflows/story-bugfix/workflow.yaml</critical>
<critical>This workflow focuses on ROOT CAUSE ANALYSIS first, then MINIMAL FIX, then REGRESSION TEST</critical>

## Overview

Bugfix workflow is different from story-implementation:
- ✅ **Diagnostic Focus**: Find root cause before fixing
- ✅ **Minimal Fix**: Change only what's necessary
- ✅ **Regression Test**: Mandatory test to prevent recurrence
- ✅ **Faster**: Simpler than full feature implementation
- ✅ **Root Cause Learning**: Capture why bug happened

## Phases

### Phase 0: Bug Understanding + Reproduction
### Phase 1: Root Cause Analysis + Fix Strategy
### Phase 2: Minimal Fix Implementation
### Phase 3: Regression Testing + Validation

---

## PHASE 0: BUG UNDERSTANDING

<step n="0" goal="Initialize and load bug story">
<action>Read story file, parse metadata, confirm Type: Bugfix</action>
<action>Extract bug details:
  - Bug description
  - Reproduction steps
  - Expected vs actual behavior
  - Error logs/stack traces
  - Affected module/component
</action>
</step>

<step n="1" goal="Load minimal documentation (faster than story-implementation)">
<action>Load only essential docs:
  - Core hub (00-START-HERE, QUICK-REFERENCE)
  - Module-specific docs IF bug is module-specific
  - Skip full doc loading (bug context is narrower)
</action>
</step>

<step n="2" goal="Reproduce the bug">
<action>Follow reproduction steps from story:
  1. Set up test scenario
  2. Execute steps to trigger bug
  3. Observe actual behavior
  4. Confirm bug reproduces consistently
</action>

<action if="bug_cannot_reproduce">
  <ask>Bug cannot be reproduced. Possible reasons:
    1. Already fixed in another story
    2. Environment-specific issue
    3. Reproduction steps incomplete

    What should we do?
    - [i] Investigate further
    - [c] Close as cannot reproduce
    - [u] Update reproduction steps
  </ask>
</action>

<action>Generate reproduction summary:
  - Bug reproduced: [Yes/No]
  - Reproduction consistency: [Always/Sometimes/Once]
  - Environment: [Local/Test/Production]
  - Error message: [Exact error]
  - Stack trace: [If available]
</action>

<critical>Bug MUST be reproducible before continuing</critical>
</step>

---

## ✅ CHECKPOINT 1: BUG UNDERSTANDING APPROVAL (1 min)

<step n="3" goal="Checkpoint 1: User confirms bug reproduction">
<action>Display bug understanding and reproduction summary</action>
<ask critical="true">
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ CHECKPOINT 1: BUG UNDERSTANDING APPROVAL
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Bug: {{story_title}}
Module: {{module}}
Reproduced: {{bug_reproduced}}

Expected: {{expected_behavior}}
Actual: {{actual_behavior}}

Continue to root cause analysis?
- [c] Continue
- [e] Edit/clarify reproduction
- [a] Abort (bug cannot reproduce)

Your choice:
</ask>
</step>

---

## PHASE 1: ROOT CAUSE ANALYSIS

<step n="4" goal="Locate faulty code (Archaeologist + Doc Oracle)">
<action>Use error logs/stack trace to find faulty code:
  1. Parse stack trace for file:line references
  2. Identify method/class where error occurs
  3. Search codebase for related code
  4. Map call chain leading to error
</action>

<action>Analyze faulty code:
  - File: [path]
  - Class/Method: [name]
  - Line number: [line]
  - Code snippet: [show 10 lines before/after]
  - Dependencies: [what this code depends on]
  - Callers: [who calls this code]
</action>
</step>

<step n="5" goal="Determine root cause">
<action>Investigate why bug exists:
  1. Logic error: Wrong condition, wrong calculation
  2. Data error: Invalid input not handled
  3. State error: Incorrect state management
  4. Race condition: Timing/concurrency issue
  5. Integration error: External service/database issue
  6. Configuration error: Wrong config value
</action>

<template-output section="root_cause_analysis">
**Root Cause Analysis**

**Bug:** {{story_title}}
**File:** {{faulty_file}}:{{line_number}}
**Method:** {{faulty_method}}

**Root Cause:**
[Clear explanation of WHY bug exists]

**Root Cause Category:**
- [ ] Logic Error (wrong condition/calculation)
- [ ] Data Error (invalid input not handled)
- [ ] State Error (incorrect state management)
- [ ] Race Condition (timing/concurrency)
- [ ] Integration Error (external service)
- [ ] Configuration Error (wrong config)

**Faulty Code:**
```csharp
[Show exact code that causes the bug]
```

**Why It's Wrong:**
[Explain what's wrong with this code]

**How It Should Work:**
[Explain correct behavior]

**Impact:**
- Severity: [Critical/High/Medium/Low]
- Affected users: [Estimate]
- Data loss risk: [Yes/No]
- Security risk: [Yes/No]
</template-output>

<critical>Save to {root_cause_analysis}</critical>
</step>

<step n="6" goal="Design fix strategy (minimal fix approach)">
<action>Design the MINIMAL fix:
  1. What is the smallest change to fix the bug?
  2. Can we fix without refactoring? (Yes = do it)
  3. Does fix introduce new complexity? (Minimize)
  4. Are there edge cases to handle?
</action>

<template-output section="fix_strategy">
**Fix Strategy**

**Approach:** [Minimal fix / Small refactor / Larger fix]

**Changes Required:**
1. File: {{file_1}}
   - Change: {{change_description_1}}
   - Lines affected: {{lines_1}}
   - Reason: {{reason_1}}

2. File: {{file_2}} (if needed)
   - Change: {{change_description_2}}
   - Lines affected: {{lines_2}}
   - Reason: {{reason_2}}

**Minimal Fix Rationale:**
[Why this is the smallest fix that solves the bug]

**Edge Cases:**
- Edge case 1: [How fix handles it]
- Edge case 2: [How fix handles it]

**Regression Test:**
- Test name: {{test_name}}
- Test approach: [Given-When-Then]
- Expected: Test fails before fix, passes after fix
</template-output>

<critical>Save to {fix_strategy}</critical>
</step>

---

## ✅ CHECKPOINT 2: FIX STRATEGY APPROVAL (2-3 min)

<step n="7" goal="Checkpoint 2: User approves fix strategy">
<action>Display root cause analysis + fix strategy</action>
<ask critical="true">
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ CHECKPOINT 2: FIX STRATEGY APPROVAL
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Root Cause: {{root_cause_summary}}
Fix Approach: {{fix_approach}}
Files to change: {{file_count}}

Approve this fix strategy?
- [c] Continue to implementation
- [e] Edit strategy
- [a] Abort bugfix

Your choice:
</ask>
</step>

---

## PHASE 2: MINIMAL FIX IMPLEMENTATION

<step n="8" goal="Write regression test FIRST (Test-Driven Bugfix)">
<action>Generate regression test that:
  1. Reproduces the bug
  2. Currently FAILS (confirms bug exists)
  3. Will PASS after fix is applied
</action>

<action>Create test file:
  - Location: tests/Modules/{{module}}/Regression/
  - Name: {{BugName}}RegressionTest.cs
  - Given-When-Then format
  - Clear test name: Bug_{{BugId}}_{{Description}}
</action>

<action>Run regression test BEFORE fix:
  - Expected: Test FAILS (reproduces bug)
  - If test passes, bug doesn't exist or test is wrong
</action>

<template-output section="regression_test">
**Regression Test**

File: tests/Modules/{{module}}/Regression/{{TestFile}}.cs

```csharp
[Test]
public void Bug_{{story_id}}_{{ShortDescription}}()
{
    // Given: [Setup that triggers bug]
    // When: [Action that causes bug]
    // Then: [Expected correct behavior]
}
```

**Test Status Before Fix:**
- Expected: ❌ FAIL (reproduces bug)
- Actual: {{test_result_before}}
</template-output>

<critical>Save to {regression_test}</critical>
<critical>Regression test MUST fail before fix</critical>
</step>

<step n="9" goal="Apply minimal fix">
<action>Generate fix code following fix strategy:
  - Change only faulty lines
  - Keep changes minimal
  - Preserve existing behavior (except bug)
  - Follow patterns (Result<T>, etc.)
</action>

<action>Generate diff preview:
  - Show before/after for each file
  - Highlight minimal nature of changes
  - Verify patterns applied
</action>
</step>

---

## ✅ CHECKPOINT 3: FIX IMPLEMENTATION APPROVAL (3-5 min)

<step n="10" goal="Checkpoint 3: User approves fix diff">
<action>Display fix diff + regression test</action>
<ask critical="true">
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ CHECKPOINT 3: FIX IMPLEMENTATION APPROVAL
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Fix Summary:
- Files changed: {{file_count}}
- Lines changed: {{line_count}}
- Approach: Minimal fix

Regression test: {{test_name}}
- Status before fix: ❌ FAIL (expected)

Approve fix implementation?
- [c] Continue - Apply fix
- [e] Edit fix
- [a] Abort

Your choice:
</ask>
</step>

<step n="11" goal="Apply fix">
<action>Apply fix changes to code files</action>
</step>

---

## PHASE 3: REGRESSION TESTING & VALIDATION

<step n="12" goal="Run regression test AFTER fix">
<action>Execute regression test:
  - dotnet test --filter "{{regression_test_name}}"
  - Expected: ✅ PASS (bug is fixed)
  - If still fails, fix is incomplete
</action>

<action if="regression_test_fails">
  <action>Fix is incomplete - debug further</action>
  <action>Analyze why test still fails</action>
  <action>Update fix</action>
  <goto step="11">Reapply fix</goto>
</action>

<action>Generate regression test result:
  - Test name: {{test_name}}
  - Before fix: ❌ FAIL
  - After fix: ✅ PASS
  - Bug is fixed: ✅
</action>
</step>

<step n="13" goal="Run ALL existing tests (ensure no new bugs)">
<action>Execute full test suite:
  - dotnet build
  - dotnet test (ALL tests)
  - Verify all existing tests still pass
</action>

<action if="existing_tests_fail">
  <action>Fix introduced new bugs!</action>
  <action>Identify which tests fail</action>
  <action>Determine if fix broke something else</action>
  <ask>Existing tests failed. Options:
    1. Adjust fix to not break existing behavior
    2. Update tests if behavior change is intentional
  </ask>
</action>

<critical>ALL existing tests MUST pass</critical>
</step>

<step n="14" goal="Verify bug is actually fixed">
<action>Manually verify bug fix:
  1. Follow original reproduction steps
  2. Confirm bug no longer reproduces
  3. Test edge cases
  4. Verify expected behavior
</action>

<action>Generate verification summary:
  - Bug reproduced before fix: ✅
  - Bug reproduces after fix: ❌ (fixed)
  - Edge cases tested: [List]
  - Expected behavior confirmed: ✅
</action>
</step>

<step n="15" goal="Final compliance check">
<action>Validate quality gates:
  - ✅ Bug reproduced: true
  - ✅ Root cause identified: true
  - ✅ Regression test added: true
  - ✅ Regression test passes: true
  - ✅ All existing tests pass: 100%
  - ✅ Bug is fixed: true
  - ✅ No new bugs introduced: true
</action>
</step>

<step n="16" goal="Documentation sync">
<action>Update docs if needed:
  - Known issues list (remove this bug)
  - Troubleshooting guide (add solution if relevant)
  - API docs (if bug was in public API)
</action>
</step>

<step n="17" goal="Capture bugfix decisions and learning">
<action>Generate decision log with bugfix-specific fields:

```yaml
story_id: {{story_id}}
story_title: {{story_title}}
type: bugfix
date: {{date}}

bug_details:
  description: {{bug_description}}
  reproduction_steps: {{reproduction_steps}}
  affected_module: {{module}}
  severity: {{severity}}

root_cause:
  category: {{category}}  # Logic/Data/State/Race/Integration/Configuration
  explanation: {{root_cause}}
  faulty_file: {{file}}:{{line}}
  faulty_code: {{code_snippet}}

fix_applied:
  approach: Minimal fix
  files_changed: {{file_count}}
  lines_changed: {{line_count}}
  fix_description: {{fix_summary}}

regression_test:
  test_name: {{test_name}}
  test_file: {{test_file}}
  before_fix: FAIL
  after_fix: PASS

learning:
  why_bug_happened: {{why}}
  how_to_prevent: {{prevention_strategy}}
  similar_bugs_to_check: {{similar_patterns}}

metrics:
  bug_fix_time: {{time_estimate}}
  regression_test_added: true
  all_tests_pass: true
```
</action>

<action>Save to {decision_log_path}</action>
</step>

---

## ✅ CHECKPOINT 4: FINAL COMMIT APPROVAL (1-2 min)

<step n="18" goal="Checkpoint 4: User approves bugfix commit">
<action>Display bugfix summary</action>
<ask critical="true">
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ CHECKPOINT 4: FINAL COMMIT APPROVAL
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Bugfix Complete!

Bug: {{story_title}} ({{story_id}})
Root Cause: {{root_cause_category}}
Fix: {{fix_approach}}

Results:
- Files changed: {{file_count}}
- Regression test: ✅ PASS
- All existing tests: ✅ PASS
- Bug fixed: ✅

Ready to commit?
- [c] Commit
- [r] Review details
- [e] Edit
- [d] Defer

Your choice:
</ask>
</step>

<step n="19" goal="Create git commit with bugfix tag">
<action>Create commit with message format:

```
fix({{module}}): {{story_title}}

Fixes bug {{story_id}}: {{bug_description}}

Root Cause:
- Category: {{root_cause_category}}
- Location: {{file}}:{{line}}
- Explanation: {{root_cause_summary}}

Fix:
- Approach: Minimal fix
- Files changed: {{file_count}}
- Lines changed: {{line_count}}

Regression Test:
- Test: {{test_name}}
- Before fix: ❌ FAIL
- After fix: ✅ PASS

All existing tests: ✅ PASS

🤖 Generated with Axon Module (BMAD)
Co-Authored-By: {{user_name}}
```
</action>
</step>

<step n="20" goal="Final summary with learning">
<action>Generate completion report:

**Bugfix Complete! 🐛→✅**

Bug: {{story_title}} ({{story_id}})
Module: {{module}}
Severity: {{severity}}
Status: ✅ Fixed

**Root Cause:**
- Category: {{category}}
- File: {{file}}:{{line}}
- Explanation: {{root_cause}}

**Fix Applied:**
- Approach: Minimal fix
- Files: {{file_count}} changed
- Lines: {{line_count}} changed

**Validation:**
- ✅ Regression test added: {{test_name}}
- ✅ Regression test passes
- ✅ All existing tests pass
- ✅ Bug verified fixed

**Learning:**
- Why bug happened: {{why}}
- Prevention strategy: {{prevention}}

**Artifacts:**
- Root cause analysis: {root_cause_analysis}
- Fix strategy: {fix_strategy}
- Regression test: {regression_test}
- Decision log: {decision_log_path}

**Git Commit:**
- SHA: {{commit_sha}}
- Message: fix({{module}}): {{story_title}}

Bugfix workflow complete! 🎉
</action>
</step>

</workflow>