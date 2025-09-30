# Axon Module - #yolo Mode Guide

**Fast-track workflow execution for experienced users** ⚡

---

## 🎯 What is #yolo Mode?

**#yolo** (You Only Live Once) is a fast-track execution mode that minimizes prompts and skips optional steps while maintaining **critical checkpoints** for safety.

### Key Characteristics

**What #yolo SKIPS**:
- ❌ Optional validation steps
- ❌ Elicitation menus
- ❌ Detailed explanations
- ❌ Verbose logging
- ❌ Step-by-step confirmations

**What #yolo KEEPS**:
- ✅ **4 Critical checkpoints** (Understanding, Pre-flight, Implementation, Final)
- ✅ **Pattern validation**
- ✅ **Test generation**
- ✅ **Build & test execution**
- ✅ **Documentation sync**

---

## ⚡ Enabling #yolo Mode

### Method 1: Command-Line Flag

```bash
# Enable #yolo in agent command
@axon-story-orchestrator implement-story #yolo
Input: Docs/PROCESS/active-stories/STORY-XXX.md
```

### Method 2: Workflow Configuration

```yaml
# In workflow.yaml
execution_mode: "#yolo"

# Options:
execution_options:
  skip_optional: true
  skip_elicitation: true
  minimize_prompts: true
  verbose: false
```

### Method 3: Environment Variable

```bash
# Set globally for session
export AXON_MODE="yolo"

# Run workflow
@axon-story-orchestrator implement-story
```

---

## 📊 #yolo vs Normal Mode Comparison

| Feature | Normal Mode | #yolo Mode |
|---------|-------------|------------|
| **Optional steps** | Asks user | Skips |
| **Elicitation** | Shows menu | Skips |
| **Explanations** | Detailed | Minimal |
| **Checkpoint 1** | ✅ Shows | ✅ Shows |
| **Checkpoint 2** | ✅ Shows | ✅ Shows |
| **Checkpoint 3** | ✅ Shows | ✅ Shows |
| **Checkpoint 4** | ✅ Shows | ✅ Shows |
| **Duration** | 40-70 min | 30-50 min (25% faster) |
| **Human review** | 11-18 min | 8-12 min (30% faster) |

---

## ✅ When to Use #yolo Mode

### Perfect For

1. ✅ **Simple stories** (Complexity: Simple)
   - Straightforward feature additions
   - Clear requirements
   - Well-understood patterns

2. ✅ **Repetitive tasks**
   - Similar to previous stories
   - Known patterns
   - Low risk

3. ✅ **Time-sensitive work**
   - Hot fixes
   - Urgent features
   - Sprint deadlines

4. ✅ **Experienced users**
   - Familiar with Axon workflows
   - Understand patterns deeply
   - Can review code quickly

5. ✅ **Trusted domains**
   - Well-documented areas
   - Stable code
   - Low complexity

---

## ❌ When NOT to Use #yolo Mode

### Avoid For

1. ❌ **Complex refactoring**
   - Multi-layer changes
   - Breaking changes
   - High coupling

2. ❌ **New domain areas**
   - Unfamiliar code
   - Experimental features
   - Unclear requirements

3. ❌ **High-risk changes**
   - Security-critical code
   - Performance-critical paths
   - Database migrations

4. ❌ **Learning/training**
   - First-time users
   - Understanding patterns
   - Exploring capabilities

5. ❌ **Cross-cutting concerns**
   - Multiple module changes
   - Architecture modifications
   - Shared infrastructure

---

## 🔧 Implementation Guide

### Workflow Configuration

#### story-orchestrator/workflow.yaml

```yaml
# Add execution mode support
execution_mode: "{user_input|yolo_flag}"  # Default: "normal"

# Conditional logic
skip_optional_steps:
  condition: "execution_mode == '#yolo'"
  value: true

skip_elicitation:
  condition: "execution_mode == '#yolo'"
  value: true

minimize_prompts:
  condition: "execution_mode == '#yolo'"
  value: true
```

#### story-implementation/instructions.md

```xml
<!-- Conditional optional step -->
<step n="5" optional="true" goal="Detailed analysis">
  <check>If NOT #yolo mode → Ask user</check>
  <check>If #yolo mode → Skip this step</check>

  <action if="execution_mode != '#yolo'">
    Perform detailed analysis
  </action>
</step>

<!-- Conditional elicitation -->
<step n="8" goal="Enhancement options">
  <elicit-required if="execution_mode != '#yolo'">
    Show 5 enhancement options
  </elicit-required>

  <action if="execution_mode == '#yolo'">
    Use default enhancements, skip menu
  </action>
</step>

<!-- Critical checkpoint (NEVER skip) -->
<step n="10" goal="Pre-flight approval">
  <ask critical="true">
    <!-- This always shows, even in #yolo mode -->
    ✅ CHECKPOINT 2: PRE-FLIGHT APPROVAL
    Approve proceeding?
    [c] Continue | [e] Edit
  </ask>
</step>
```

---

## 🎯 4 Critical Checkpoints

**These ALWAYS run, even in #yolo mode:**

### Checkpoint 1: Story Understanding (1 min)

**Purpose**: Verify story interpretation

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ CHECKPOINT 1: STORY UNDERSTANDING
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Story: Add Auto-Revoke Old Credentials
Module: Identity
Complexity: Medium

Key Requirements:
1. Revoke credentials > 90 days old
2. Update IdentityCredential entity
3. Create domain command + handler

Technical Approach:
- Domain: AxonPrincipal.RevokeStaleCredentials()
- Application: RevokeStaleCredentialsCommandHandler
- Infrastructure: Migration for new columns

Approve?
[c] Continue | [e] Edit | [a] Abort
```

**In #yolo mode**: Shows same info, user can still edit/abort

---

### Checkpoint 2: Pre-Flight Approval (3-5 min)

**Purpose**: Validate discovery, library, pattern analysis

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ CHECKPOINT 2: PRE-FLIGHT APPROVAL
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Discovery Summary:
- REUSE: AxonPrincipal command pattern (50+ methods)
- EXTEND: Add RevokeStaleCredentials() method
- CREATE: CredentialRevokedEvent

Library Validation:
- No external libraries needed
- Uses existing EF Core + domain patterns

Pattern Compliance:
- Result<T>: ✅ Required
- CQRS: ✅ Command pattern
- Domain Events: ✅ Event emission
- Compliance: 100%

Approve proceeding?
[c] Continue | [e] Edit | [a] Abort
```

**In #yolo mode**: Same checkpoint, faster review expected

---

### Checkpoint 3: Implementation Preview (5-10 min)

**Purpose**: Review code diff before applying

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ CHECKPOINT 3: IMPLEMENTATION PREVIEW
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Files to Modify (3):
- src/.../AxonPrincipal.Commands.cs (+45 lines)
- src/.../IdentityCredential.cs (+8 lines)
- src/.../IdentityWriteDbContext.cs (+12 lines)

Files to Create (4):
- CredentialRevokedEvent.cs (18 lines)
- RevokeStaleCredentialsCommand.cs (12 lines)
- RevokeStaleCredentialsCommandHandler.cs (52 lines)
- Migration: AddCredentialRevocation (28 lines)

Total: +175 lines, 7 files affected

Pattern Compliance: 100% ✅
Estimated Coverage: 92%

Approve changes?
[c] Continue | [e] Edit | [a] Abort
```

**In #yolo mode**: Diff preview still shown, critical safety check

---

### Checkpoint 4: Final Approval (2 min)

**Purpose**: Validate quality gates before commit

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ CHECKPOINT 4: FINAL COMMIT APPROVAL
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Quality Gates:
- Pattern Compliance: 100% ✅
- Test Coverage: 94% ✅
- AC Coverage: 100% (5/5) ✅
- Build Success: ✅
- Tests Passed: 18/18 ✅
- Doc Drift: 0 ✅

Documentation Updates:
- modules/identity/01-domain-model.md ✅
- Decision log created ✅

Ready to commit?
[c] Commit | [d] Defer | [r] Review
```

**In #yolo mode**: All quality gates still validated

---

## 🚀 Performance Improvements

### Time Savings

**Normal Mode**:
```
Phase 0: Understanding       2 min (AI) + 1 min (human)
Phase 1: Pre-flight         10 min (AI) + 5 min (human)
Phase 2: Implementation     15 min (AI) + 10 min (human)
Phase 3: Validation          8 min (AI) + 2 min (human)
─────────────────────────────────────────────────────
Total:                      35 min (AI) + 18 min (human)
                            53 minutes total
```

**#yolo Mode**:
```
Phase 0: Understanding       1 min (AI) + 1 min (human)
Phase 1: Pre-flight          6 min (AI) + 3 min (human)
Phase 2: Implementation     10 min (AI) + 6 min (human)
Phase 3: Validation          5 min (AI) + 2 min (human)
─────────────────────────────────────────────────────
Total:                      22 min (AI) + 12 min (human)
                            34 minutes total
```

**Improvement**: ~36% faster (19 minutes saved)

---

## 📋 #yolo Checklist

Before using #yolo mode, verify:

- ✅ Story complexity is Simple or Medium
- ✅ Requirements are clear and complete
- ✅ You're familiar with the target module
- ✅ Patterns are well-established
- ✅ Risk is low to medium
- ✅ You can review code quickly
- ✅ Tests can be validated efficiently

If ANY item is ❌, use Normal mode instead.

---

## 🎯 #yolo Best Practices

### 1. Start with Normal Mode

**First story in a new area**: Always use Normal mode
- Understand patterns
- Learn domain
- Build context

**Later stories**: Switch to #yolo for similar work

---

### 2. Review Checkpoints Carefully

**Even in #yolo mode**:
- Read checkpoint summaries thoroughly
- Verify pattern compliance
- Review code diffs line-by-line
- Check test coverage

**Don't rush checkpoints** - they're your safety net

---

### 3. Know Your Escape Hatches

**At any checkpoint**, you can:
- **[e] Edit**: Switch back to detailed mode
- **[a] Abort**: Stop workflow safely
- **[r] Review**: Request more detail

**Use them** when something looks wrong

---

### 4. Monitor Quality Metrics

**Track these over time**:
```yaml
yolo_metrics:
  stories_completed: 50
  success_rate: 94%  # Should be > 90%
  avg_duration: 35 min
  pattern_compliance: 98%  # Should be > 95%
  test_coverage: 91%  # Should be > 90%
  rework_rate: 8%  # Should be < 15%
```

**If metrics decline**: Return to Normal mode

---

### 5. Calibrate Your Judgment

**Start conservative**:
- Week 1-2: Normal mode only
- Week 3-4: #yolo for Simple stories
- Month 2+: #yolo for Medium stories

**Never #yolo**:
- Complex stories
- Architectural changes
- Security-critical code

---

## 🔍 Troubleshooting #yolo Mode

### Issue: #yolo is Too Fast

**Symptom**: Missing important details

**Solution**:
```yaml
# Customize #yolo behavior
yolo_config:
  skip_optional: true
  skip_elicitation: true
  minimize_prompts: false  # Still show some prompts
  show_summary: true  # Show summaries of skipped steps
```

---

### Issue: #yolo is Too Slow

**Symptom**: Still taking too long

**Solution**:
```yaml
# Aggressive #yolo
yolo_config:
  skip_optional: true
  skip_elicitation: true
  minimize_prompts: true
  parallel_execution: true  # Run tasks in parallel
  cache_results: true  # Cache reusable results
```

---

### Issue: Quality Declining

**Symptom**: More bugs, lower coverage

**Solution**:
1. **Return to Normal mode** for 2-3 stories
2. **Review patterns** and refresh understanding
3. **Analyze failures**: What was missed?
4. **Adjust #yolo usage**: More conservative criteria

---

## 📊 #yolo Mode Metrics

### Track These KPIs

```yaml
kpis:
  # Success metrics
  completion_rate: "> 90%"
  first_time_right: "> 85%"
  rework_rate: "< 15%"

  # Quality metrics
  pattern_compliance: "> 95%"
  test_coverage: "> 90%"
  ac_coverage: "100%"

  # Performance metrics
  avg_duration: "30-40 min"
  time_savings: "25-35%"
  human_review_time: "8-12 min"
```

### When to Disable #yolo

**Red flags** (any 2 trigger review):
- ❌ Completion rate < 85%
- ❌ Rework rate > 20%
- ❌ Pattern compliance < 90%
- ❌ Test coverage < 85%
- ❌ Multiple failed builds
- ❌ User dissatisfaction

**Action**: Return to Normal mode, investigate root cause

---

## 🎓 #yolo Mode Maturity Model

### Level 1: Beginner (Weeks 1-4)
- **Mode**: Normal only
- **Focus**: Learning patterns
- **Stories**: All types, careful review
- **Goal**: Build foundation

### Level 2: Intermediate (Months 2-3)
- **Mode**: Normal + occasional #yolo
- **Focus**: Pattern mastery
- **Stories**: #yolo for Simple only
- **Goal**: Increase confidence

### Level 3: Advanced (Months 4-6)
- **Mode**: #yolo for Simple/Medium
- **Focus**: Efficiency
- **Stories**: #yolo except Complex
- **Goal**: Maximize throughput

### Level 4: Expert (Months 7+)
- **Mode**: #yolo default, Normal for complex
- **Focus**: Strategic use
- **Stories**: Judgment-based selection
- **Goal**: Optimal balance

---

## 💡 Pro Tips

### Tip #1: Create #yolo Presets

```yaml
# ~/.axon/presets.yaml
presets:
  yolo-simple:
    mode: yolo
    complexity: simple
    skip_optional: true
    skip_elicitation: true

  yolo-medium:
    mode: yolo
    complexity: medium
    skip_optional: true
    skip_elicitation: false  # Keep elicitation for medium

  yolo-aggressive:
    mode: yolo
    complexity: any
    parallel: true
    cache: true
```

---

### Tip #2: Use #yolo for Iterations

**First implementation**: Normal mode
**Subsequent iterations**: #yolo mode

```bash
# First pass (Normal)
@axon-story-orchestrator implement-story
Input: STORY-001.md

# Second pass after feedback (#yolo)
@axon-story-orchestrator implement-story #yolo
Input: STORY-001-v2.md
```

---

### Tip #3: Pair #yolo with Reviews

**Solo work**: Normal mode for safety
**Pair/team work**: #yolo mode with peer review

```bash
# Team workflow
1. Developer A: #yolo mode implementation
2. Developer B: Checkpoint reviews
3. Together: Final approval
```

---

### Tip #4: Monitor Your #yolo Success Rate

```bash
# Track success rate
yolo_stories_success / yolo_stories_total * 100

# Target: > 90%
# If below: Return to Normal mode
```

---

## 🎯 Summary

**#yolo Mode** is powerful when used correctly:

✅ **DO**:
- Use for simple, familiar stories
- Still review checkpoints carefully
- Track quality metrics
- Return to Normal when needed

❌ **DON'T**:
- Use for complex/risky work
- Skip checkpoint reviews
- Ignore declining metrics
- Forget you can abort

**Remember**: #yolo saves time, but **quality always comes first** ✨

---

**Module Version**: 1.0.0
**Last Updated**: 2025-09-30
**For Questions**: See TROUBLESHOOTING.md