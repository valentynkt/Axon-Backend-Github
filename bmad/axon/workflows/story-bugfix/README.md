# Story Bugfix Workflow

**Version**: 1.0
**Author**: Axon Module
**Type**: Diagnostic + Fix Workflow
**Complexity**: Medium
**Estimated Duration**: 20-40 minutes (AI work) + 7-11 minutes (human checkpoints)

---

## Purpose

Resolve bugs with a **diagnostic-first approach**:
- ✅ **Root Cause Analysis**: Understand WHY before fixing
- ✅ **Minimal Fix**: Change only what's necessary
- ✅ **Regression Test**: Mandatory test to prevent recurrence
- ✅ **Test-Driven**: Write failing test BEFORE fix
- ✅ **Learning Capture**: Document why bug happened

**Faster than story-implementation** (simpler scope, diagnostic focus)

---

## When to Use

Use this workflow when:
- ✅ Story type is **Bugfix** (not Feature or Refactor)
- ✅ Resolving a reported issue or error
- ✅ Bug is reproducible
- ✅ Need root cause understanding (not just quick patch)
- ✅ Want to prevent bug recurrence

**Do NOT use for:**
- ❌ Adding new features (use `story-implementation`)
- ❌ Refactoring code (use `story-refactoring`)
- ❌ Cannot reproduce bug (needs investigation first)

---

## Key Differences from story-implementation

| Aspect | story-implementation | story-bugfix |
|--------|---------------------|--------------|
| **Focus** | Build new | Fix broken |
| **Scope** | Full feature (4 layers) | Minimal fix (1-2 files) |
| **Analysis** | Discovery + Library | **Root Cause Analysis** |
| **Testing** | Comprehensive suite | **Regression Test (mandatory)** |
| **Approach** | Bottom-up layers | **Minimal fix** |
| **Duration** | 40-70 min | 20-40 min (faster) |
| **Human Time** | 11-18 min (4 checkpoints) | 7-11 min (4 checkpoints, shorter) |
| **Pre-Flight** | Full validation | Lighter (diagnostic focus) |
| **Learning** | Decisions + patterns | **Why bug happened + prevention** |

---

## Workflow Phases

### Phase 0: Bug Understanding + Reproduction
- Load bug story
- Reproduce bug (MANDATORY)
- Confirm bug exists
- **Checkpoint 1**: Bug understanding approval

### Phase 1: Root Cause Analysis + Fix Strategy
- Locate faulty code (stack trace, search)
- Determine root cause (WHY bug exists)
- Design minimal fix strategy
- **Checkpoint 2**: Fix strategy approval

### Phase 2: Minimal Fix Implementation
- Write regression test FIRST (Test-Driven)
- Test FAILS (reproduces bug)
- Apply minimal fix
- **Checkpoint 3**: Fix implementation approval

### Phase 3: Regression Testing + Validation
- Run regression test AFTER fix (MUST PASS)
- Run ALL existing tests (MUST PASS)
- Verify bug is fixed
- **Checkpoint 4**: Final commit approval
- Create git commit with root cause

**Total**: 4 checkpoints (same as story-implementation, but faster)

---

## Prerequisites

### Bugfix Story Requirements

```markdown
# Story: Fix Auth Token Expiry Bug

**Story ID**: AXON-088
**Module**: Identity
**Type**: Bugfix  # CRITICAL: Must be "Bugfix"
**Priority**: Critical
**Severity**: High

## Bug Description

**What's wrong:** Auth tokens expire immediately instead of after 1 hour

**Expected Behavior:**
- Tokens should be valid for 1 hour
- Users should stay logged in

**Actual Behavior:**
- Tokens expire within 1 minute
- Users get logged out immediately

## Reproduction Steps

1. Login with valid credentials
2. Wait 2 minutes
3. Make authenticated API call
4. Observe: 401 Unauthorized error

## Error Logs

```
System.UnauthorizedAccessException: Token has expired
  at JwtValidator.ValidateToken(String token)
  at AuthenticationMiddleware.Invoke(HttpContext context)
```

## Environment

- Occurred in: Test + Production
- First reported: 2025-09-28
- Affected users: All users
...
```

---

## Usage

**From Story Orchestrator:**
```
@axon-story-orchestrator
*implement-story
[Provide bugfix story file]
# Orchestrator detects Type: Bugfix → routes to story-bugfix
```

**Direct:**
```
workflow: bmad/axon/workflows/story-bugfix/workflow.yaml
story_file: path/to/bugfix-story-088.md
```

---

## Outputs

### Bugfix-Specific Artifacts

1. **Root Cause Analysis** (`{output_folder}/root-cause-analysis.md`)
   - Bug description
   - Faulty code location (file:line)
   - Root cause category
   - Why bug exists
   - How it should work
   - Impact assessment

2. **Fix Strategy** (`{output_folder}/fix-strategy.md`)
   - Minimal fix approach
   - Files to change
   - Edge cases
   - Regression test plan

3. **Regression Test** (`{output_folder}/regression-test.md`)
   - Test name
   - Test file location
   - Before fix: ❌ FAIL
   - After fix: ✅ PASS
   - Prevents bug recurrence

4. **Decision Log** (with bugfix-specific fields)
   - Bug details
   - Root cause
   - Fix applied
   - Regression test
   - Learning (why + prevention)

### Regression Test File

Location: `tests/Modules/{{module}}/Regression/`

```csharp
[TestFixture]
public class AuthTokenExpiryRegressionTests
{
    [Test]
    public void Bug_AXON088_TokenShouldBeValidForOneHour()
    {
        // Given: User logs in
        var user = TestHelpers.CreateUser();
        var token = _authService.GenerateToken(user);

        // When: 2 minutes pass (should still be valid)
        _testClock.Advance(TimeSpan.FromMinutes(2));

        // Then: Token should still be valid
        var result = _authService.ValidateToken(token);
        result.IsSuccess.ShouldBeTrue();
    }
}
```

### Git Commit Format

```
fix(identity): Auth tokens expire immediately

Fixes bug AXON-088: Tokens expiring in 1 minute instead of 1 hour

Root Cause:
- Category: Logic Error
- Location: TokenGenerator.cs:42
- Explanation: ExpiresIn was set to 60 seconds instead of 3600 seconds (typo)

Fix:
- Approach: Minimal fix
- Files changed: 1 (TokenGenerator.cs)
- Lines changed: 1 (changed 60 to 3600)

Regression Test:
- Test: Bug_AXON088_TokenShouldBeValidForOneHour
- Before fix: ❌ FAIL (token expired in 1 min)
- After fix: ✅ PASS (token valid for 1 hour)

All existing tests: ✅ PASS

🤖 Generated with Axon Module (BMAD)
Co-Authored-By: Valik
```

---

## Test-Driven Bugfix Workflow

This workflow uses **Test-Driven Bugfix** approach:

1. **Write Regression Test** (that reproduces bug)
2. **Run Test** → ❌ FAILS (confirms bug exists)
3. **Apply Fix** (minimal code change)
4. **Run Test** → ✅ PASSES (confirms bug fixed)
5. **Run ALL Tests** → ✅ PASS (no new bugs)

**Benefits:**
- Proves bug exists
- Proves fix works
- Prevents regression forever
- Documents bug for future

---

## Root Cause Categories

The workflow classifies bugs into 6 categories:

| Category | Description | Example |
|----------|-------------|---------|
| **Logic Error** | Wrong condition or calculation | `if (age > 18)` should be `>=` |
| **Data Error** | Invalid input not handled | No null check |
| **State Error** | Incorrect state management | Race condition in state |
| **Race Condition** | Timing/concurrency issue | Thread safety bug |
| **Integration Error** | External service issue | API timeout not handled |
| **Configuration Error** | Wrong config value | Wrong connection string |

**Learning:** Capturing root cause category helps identify patterns and prevent future bugs.

---

## Example: Quick Bugfix Flow

### Story: "Fix Token Expiry Bug"

**Phase 0: Reproduction** (2 min)
- Reproduce: Login → Wait 2 min → API call fails ✅
- Confirmed: Bug reproduces consistently

**Checkpoint 1**: User approves

**Phase 1: Root Cause** (5 min)
- Located: TokenGenerator.cs:42
- Found: `ExpiresIn = 60` (seconds)
- Should be: `ExpiresIn = 3600` (seconds)
- Root Cause: Logic Error (typo)
- Fix: Change 1 line

**Checkpoint 2**: User approves

**Phase 2: Fix** (5 min)
- Write regression test → ❌ FAILS
- Change 60 to 3600
- Regression test → ✅ PASSES

**Checkpoint 3**: User approves

**Phase 3: Validation** (3 min)
- All tests → ✅ PASS
- Bug verified fixed ✅

**Checkpoint 4**: User approves

**Result**: Bug fixed in 15 min + 4 checkpoints (7-11 min human review) = 22-26 min total

---

## Troubleshooting

### Issue: Bug cannot reproduce

**Symptoms**: Following reproduction steps doesn't trigger bug
**Solution**:
1. Check environment (local vs test vs production)
2. Verify data setup (might be data-specific)
3. Check if already fixed in another commit
4. Ask for more detailed reproduction steps
5. Consider closing as "cannot reproduce"

### Issue: Regression test passes before fix

**Symptoms**: Test should fail but passes
**Solution**:
1. Bug is already fixed OR
2. Test is not actually testing the bug OR
3. Reproduction steps are wrong
→ Investigate and update test or story

### Issue: Fix breaks existing tests

**Symptoms**: Regression test passes, but other tests fail
**Solution**:
1. Fix has unintended side effects
2. Adjust fix to be more surgical
3. Or update tests if behavior change is intentional (rare)

### Issue: Root cause unclear

**Symptoms**: Can reproduce bug but don't understand why
**Solution**:
1. Use debugger to step through code
2. Add logging to faulty area
3. Consult with domain expert
4. Review git history (when was bug introduced?)

---

## Configuration

Same as story-implementation, plus:

```yaml
# Bugfix-specific settings
root_cause_analysis_required: true
regression_test_mandatory: true
minimal_fix_approach: true

# Success criteria
success_metrics:
  bug_reproduced: "true"
  root_cause_identified: "true"
  regression_test_added: "true"
  regression_test_passes: "true"
  all_existing_tests_pass: "100%"
```

---

## Success Metrics

- **Bug Resolution**: 100% (bug no longer reproduces)
- **Regression Prevention**: 100% (regression test added)
- **No New Bugs**: 100% (all existing tests pass)
- **Root Cause Learning**: 100% (documented)
- **Fix Time**: 3x faster than story-implementation

---

## Related Workflows

- **story-orchestrator**: Routes to this workflow for Type: Bugfix
- **story-implementation**: Different focus (build new)
- **story-refactoring**: Different focus (improve code)

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-09-30 | Initial release (Phase 3 implementation) |

---

**Status**: ✅ **Production-Ready** (Phase 3 Complete - Diagnostic Bugfix)
**Motto**: 🐛→✅ **Understand, Test, Fix, Learn**