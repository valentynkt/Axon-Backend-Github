# Axon Module - Troubleshooting Guide

**Version**: 1.0.0
**Last Updated**: 2025-09-30
**Module**: Axon Development Orchestrator

---

## 📋 Table of Contents

1. [Quick Diagnostics](#quick-diagnostics)
2. [Common Issues](#common-issues)
3. [Workflow Errors](#workflow-errors)
4. [Agent Issues](#agent-issues)
5. [Documentation Loading](#documentation-loading)
6. [Performance Issues](#performance-issues)
7. [Checkpoint Problems](#checkpoint-problems)
8. [Pattern Compliance Failures](#pattern-compliance-failures)
9. [Test Generation Issues](#test-generation-issues)
10. [Advanced Debugging](#advanced-debugging)

---

## 🔍 Quick Diagnostics

### Health Check Checklist

Run this quick checklist when encountering issues:

```bash
# 1. Verify module structure
ls bmad/axon/{agents,workflows,tasks,templates,data}

# 2. Count files (should be ~91)
find bmad/axon -name "*.yaml" -o -name "*.md" | wc -l

# 3. Validate YAML structure
yamllint bmad/axon/config.yaml
yamllint bmad/axon/workflows/*/workflow.yaml

# 4. Check documentation paths
grep -r "Docs/" bmad/axon/workflows/*/workflow.yaml

# 5. Verify agents exist
ls -la bmad/axon/agents/*.md
```

**Expected Results**:
- ✅ 91+ files (agents, workflows, tasks, templates, data)
- ✅ 6 agent files
- ✅ 10 workflow directories with YAML files
- ✅ 35 task files
- ✅ All YAML files parse correctly

---

## 🚨 Common Issues

### Issue #1: "Workflow file not found"

**Symptom**: Error when loading workflow YAML

```
Error: Cannot find workflow file at: bmad/axon/workflows/story-implementation/workflow.yaml
```

**Causes**:
1. Incorrect path (relative vs absolute)
2. Missing workflow directory
3. Typo in workflow name

**Solution**:
```bash
# Verify workflow exists
ls bmad/axon/workflows/story-implementation/workflow.yaml

# Check from project root
pwd  # Should be /Users/valentynkit/Repos/Axon-Backend

# If path is wrong, use absolute path
{project-root}/bmad/axon/workflows/story-implementation/workflow.yaml
```

**Fix**:
- Always use `{project-root}` variable in paths
- Verify current working directory
- Check spelling of workflow name

---

### Issue #2: "Config source not found"

**Symptom**: Cannot load module configuration

```
Error: config_source file not found: {project-root}/bmad/axon/config.yaml
```

**Causes**:
1. Config file missing
2. Path not resolved correctly
3. Module not installed

**Solution**:
```bash
# Check if config exists
cat bmad/axon/config.yaml | head -20

# Verify path resolution
echo $PROJECT_ROOT  # Should be set

# Re-install module if needed
# (Run installer workflow if available)
```

**Fix**:
- Ensure `bmad/axon/config.yaml` exists
- Verify `{project-root}` resolves to repository root
- Check file permissions (read access required)

---

### Issue #3: "Documentation file not found"

**Symptom**: Cannot load required documentation

```
Error: Cannot load: Docs/ENGINEERING/00-START-HERE.md
```

**Causes**:
1. Documentation not present in repository
2. Path incorrect (case sensitivity on macOS)
3. Documentation moved/renamed

**Solution**:
```bash
# Find documentation location
find Docs -name "00-START-HERE.md"

# Check all engineering docs
ls Docs/ENGINEERING/

# Verify case sensitivity
# macOS is case-insensitive but case-preserving
ls -la Docs/ENGINEERING/00-START-HERE.md
```

**Fix**:
- Update paths in workflow YAML if docs moved
- Ensure documentation exists before running workflows
- Use exact case for file names

---

### Issue #4: "Agent command not found"

**Symptom**: Agent doesn't respond to commands

```
Error: Command 'discover-code' not found in axon-archaeologist
```

**Causes**:
1. Agent file outdated (not BMM-compliant)
2. Command misspelled
3. Agent not loaded

**Solution**:
```bash
# Check agent file
cat bmad/axon/agents/axon-archaeologist.md | grep '<c cmd='

# List all commands
grep '<c cmd=' bmad/axon/agents/axon-archaeologist.md
```

**Fix**:
- Verify agent uses BMM pattern (`<c cmd="name">`)
- Check command spelling
- Reload agent file if modified

---

## 🔄 Workflow Errors

### Error: "Step execution failed"

**Symptom**: Workflow stops at specific step

```
Error: Step 5 execution failed - Cannot invoke @axon-archaeologist
```

**Debugging Steps**:
1. **Check step attributes**: `optional="true"`, `if="condition"`
2. **Verify agent reference**: Agent name correct?
3. **Check dependencies**: Previous steps completed?
4. **Read instructions**: Are instructions clear?

**Common Causes**:
- Agent invocation syntax incorrect
- Missing input parameters
- Conditional not met (`if="..."`)
- Previous step failed silently

**Solution**:
```yaml
# Correct agent invocation:
<action>Invoke @axon-archaeologist:
- discover-code
- Input: {story_context}
</action>

# Check conditional:
<step n="5" if="module == 'Identity'">
  # Only runs if module is Identity
</step>
```

---

### Error: "Template output failed"

**Symptom**: Cannot save template output

```
Error: Failed to write to: {output_folder}/preflight-package.md
```

**Causes**:
1. Output folder doesn't exist
2. No write permissions
3. Invalid path resolution

**Solution**:
```bash
# Create output directory
mkdir -p {output_folder}

# Check permissions
ls -la {output_folder}

# Verify path resolution
echo "Output folder: {output_folder}"
```

**Fix**:
- Ensure output directory exists before workflow starts
- Check file permissions (write access required)
- Use absolute paths for output

---

### Error: "Checklist validation failed"

**Symptom**: Workflow reports validation failures

```
Error: 12/50 checklist items failed validation
```

**Debugging**:
```bash
# Read checklist file
cat bmad/axon/workflows/story-implementation/checklist.md

# Check which items failed
# (Listed in validation report)
```

**Common Failures**:
- Build didn't run: `dotnet build` not executed
- Tests not generated: Missing test files
- Documentation not loaded: Paths incorrect
- Pattern compliance: Code doesn't follow patterns

**Solution**:
- Re-run failed steps manually
- Check error logs for root cause
- Validate prerequisites before continuing

---

## 🤖 Agent Issues

### Agent Not Loading

**Symptom**: Agent file loads but doesn't respond

**Checklist**:
1. ✅ Agent file is valid markdown
2. ✅ Agent has `<agent>` XML wrapper
3. ✅ Agent has `<cmds>` section
4. ✅ Commands have `cmd=""` attribute
5. ✅ Agent has `<persona>` section

**Example Valid Agent**:
```xml
<agent id="bmad/axon/agents/axon-archaeologist.md">
  <persona>
    <role>Codebase Discovery Specialist</role>
  </persona>
  <cmds>
    <c cmd="discover-code">Search codebase for existing implementations</c>
    <c cmd="map-apis">Map available APIs across layers</c>
  </cmds>
</agent>
```

---

### Agent Commands Not Working

**Symptom**: Agent loaded but commands fail

**Common Causes**:
1. Command delegates to missing workflow/task
2. Parameters not passed correctly
3. Workflow path incorrect

**Debugging**:
```xml
<!-- Check command structure -->
<c cmd="discover-code"
   run-workflow="{project-root}/bmad/axon/tasks/search-existing.md">
   Discover existing implementations
</c>

<!-- Verify workflow/task exists -->
```

**Solution**:
- Validate workflow/task path
- Check parameters are passed: `{story_context}`, `{module}`, etc.
- Ensure task file is executable

---

## 📚 Documentation Loading

### Progressive Loading Too Slow

**Symptom**: Loading 7+ documentation files takes too long

**Optimization**:
1. **Load on-demand**: Only load docs when referenced
2. **Use excerpts**: Load specific sections, not entire files
3. **Cache results**: Store loaded docs in workflow context
4. **Parallel loading**: Load multiple docs concurrently

**Example Optimized Loading**:
```yaml
# Before: Load all 7 docs upfront
identity_module_docs:
  - "{engineering_docs}/modules/identity/00-INDEX.md"
  - "{engineering_docs}/modules/identity/01-domain-model.md"
  # ... (7 files)

# After: Load only what's needed
identity_module_docs:
  - path: "{engineering_docs}/modules/identity/00-INDEX.md"
    sections: ["Overview", "Key Concepts"]  # Only load these sections
  - path: "{engineering_docs}/modules/identity/01-domain-model.md"
    load_if: "{requires_domain_changes}"  # Conditional loading
```

---

### Documentation Drift Detected

**Symptom**: Doc-sync reports multiple drift points

```
Warning: 8 documentation drift points detected
- modules/identity/01-domain-model.md: Missing CredentialRevokedEvent
- modules/identity/05-api-contracts.md: Missing /revoke-stale endpoint
```

**Solution**:
1. **Review drift report**: Understand what changed
2. **Update docs**: Manually or via doc-sync workflow
3. **Validate updates**: Ensure accuracy
4. **Re-run workflow**: Verify zero drift

**Doc-Sync Workflow**:
```bash
# Run doc-sync workflow
Execute: bmad/axon/workflows/doc-sync/workflow.yaml

# Inputs:
- code_changes: [List of files modified]
- affected_docs: [List of docs to update]
```

---

## ⚡ Performance Issues

### Workflow Taking Too Long

**Symptom**: Workflow exceeds estimated duration significantly

**Benchmarks**:
- Story Understanding: ~1-2 minutes
- Pre-Flight: ~3-5 minutes
- Implementation: ~10-20 minutes (depending on complexity)
- Validation: ~5-10 minutes

**Total**: 40-70 minutes for medium complexity story

**Optimization Strategies**:

#### 1. Reduce Context Size
```yaml
# Limit doc loading
max_doc_lines: 500  # Truncate large docs
load_summary_only: true  # Load summaries, not full content
```

#### 2. Use #yolo Mode
```yaml
# Skip optional steps and elicitation
execution_mode: "#yolo"
skip_optional: true
skip_elicitation: true
minimize_prompts: true
```

#### 3. Batch Operations
```yaml
# Batch tool calls instead of sequential
<action>
  Read: [file1, file2, file3]  # Parallel reads
  Grep: [pattern1, pattern2]   # Parallel searches
</action>
```

#### 4. Cache Results
```yaml
# Store reusable results
cached_data:
  pattern_catalog: "{data}/pattern-catalog.yaml"  # Load once
  library_capabilities: "{data}/library-capabilities.yaml"
```

---

### Token Usage Too High

**Symptom**: Workflow uses excessive context tokens

**Monitoring**:
```bash
# Check token usage in logs
# (Look for token budget warnings)
```

**Optimization**:
1. **Summarize docs**: Use exec summaries instead of full text
2. **Prune context**: Remove unused variables
3. **Lazy loading**: Load docs only when needed
4. **Use references**: Store paths, not content

**Example**:
```yaml
# Before: Store full content
identity_docs_content: |
  [Full 5,000 line documentation]

# After: Store reference
identity_docs_ref: "{engineering_docs}/modules/identity/00-INDEX.md"
# Load section when needed:
- Load: {identity_docs_ref}#key-concepts
```

---

## ✅ Checkpoint Problems

### Checkpoint Not Triggering

**Symptom**: Workflow doesn't stop at checkpoint

**Causes**:
1. `<ask critical="true">` not present
2. #yolo mode enabled (skips checkpoints)
3. Instructions don't have checkpoint tag

**Solution**:
```xml
<!-- Ensure checkpoint has ask tag -->
<step n="6" goal="Approve pre-flight">
<ask critical="true">
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ CHECKPOINT 2: PRE-FLIGHT APPROVAL
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Approve proceeding?
[c] Continue | [e] Edit | [a] Abort
</ask>
</step>
```

**Check #yolo mode**:
```yaml
# In workflow.yaml
execution_mode: "normal"  # NOT #yolo
```

---

### User Input Not Captured

**Symptom**: Workflow continues without user approval

**Causes**:
1. `<ask>` tag missing `critical="true"`
2. Workflow doesn't wait for response
3. Response not parsed correctly

**Solution**:
```xml
<!-- Always use critical="true" for checkpoints -->
<ask critical="true">
Your approval message here?
[c] Continue | [e] Edit
</ask>

<!-- Wait for response -->
<action>Parse user response: {user_input}</action>
<check>If {user_input} == 'e' → Go to edit mode</check>
<check>If {user_input} == 'c' → Continue</check>
```

---

## 🎯 Pattern Compliance Failures

### Result<T, Error> Violations

**Symptom**: Pattern compliance < 95%

```
Error: 3 Result<T> violations found
- AxonPrincipal.RevokeStaleCredentials() returns void (should return Result<T>)
```

**Solution**:
```csharp
// ❌ Wrong:
public void RevokeStaleCredentials(TimeSpan maxAge)
{
    // ...
}

// ✅ Correct:
public Result<Unit, Error> RevokeStaleCredentials(TimeSpan maxAge)
{
    // ...
    return Result<Unit>.Success(Unit.Value);
}
```

---

### CQRS Violations

**Symptom**: Commands and queries mixed

```
Error: Query method mutates state
- GetMyPrincipal() modifies LastSeen timestamp
```

**Solution**:
```csharp
// ❌ Wrong: Query modifies state
public AxonUser GetMyPrincipal()
{
    user.LastSeen = DateTime.UtcNow;  // Mutation in query!
    return user;
}

// ✅ Correct: Separate command
public AxonUser GetMyPrincipal()  // Pure query
{
    return user;
}

public Result<Unit, Error> UpdateLastSeen()  // Separate command
{
    LastSeen = DateTime.UtcNow;
    return Result<Unit>.Success(Unit.Value);
}
```

---

### StrongId<T> Violations

**Symptom**: Primitive IDs used instead of StrongId

```
Error: Primitive Guid used instead of StrongId<T>
- AxonUserId should be StrongId<AxonUser>, not Guid
```

**Solution**:
```csharp
// ❌ Wrong:
public Guid AxonUserId { get; set; }

// ✅ Correct:
public AxonUserId Id { get; set; }  // Where AxonUserId : StrongId<AxonUser>
```

---

## 🧪 Test Generation Issues

### Tests Not Compiling

**Symptom**: Generated tests fail to compile

**Common Causes**:
1. Missing using statements
2. Incorrect test base class
3. Wrong assertion library (should be Shouldly)
4. Test fixtures not set up

**Solution**:
```csharp
// Ensure correct structure:
using Shouldly;  // Not FluentAssertions
using NUnit.Framework;

[TestFixture]
public class AxonPrincipalTests : IdentityTestBase  // Correct base class
{
    [Test]
    public void Given_StaleCredentials_When_Revoke_Then_Success()
    {
        // Arrange
        var principal = CreateTestPrincipal();

        // Act
        var result = principal.RevokeStaleCredentials(TimeSpan.FromDays(90));

        // Assert
        result.IsSuccess.ShouldBeTrue();  // Shouldly syntax
    }
}
```

---

### Test Coverage < 90%

**Symptom**: Code coverage below target

**Strategies**:
1. **Add error path tests**: Test all failure scenarios
2. **Add edge cases**: Boundary conditions, null inputs
3. **Add integration tests**: Test across layers
4. **Add AC tests**: One test per acceptance criterion

**Coverage Report**:
```bash
# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Generate report
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage-report
```

---

## 🔧 Advanced Debugging

### Enable Verbose Logging

**In workflow.yaml**:
```yaml
debug_mode: true
verbose_logging: true
log_level: "debug"  # trace, debug, info, warn, error
```

**In instructions.md**:
```xml
<debug>Log variable values: {story_id}, {module}, {complexity}</debug>
<debug>Log workflow state: {current_step}, {phase}</debug>
```

---

### Workflow State Inspection

**Check current state**:
```yaml
workflow_state:
  current_phase: "Phase 2: Implementation"
  current_step: 12
  variables:
    story_id: "STORY-001"
    module: "Identity"
    complexity: "Medium"
  outputs_generated:
    - preflight-package.md
    - implementation-diff.md
```

---

### Manual Step Execution

**Skip to specific step**:
```xml
<goto step="5">Skip to pre-flight validation</goto>
```

**Repeat failed step**:
```xml
<repeat step="8">Retry test generation</repeat>
```

---

## 📞 Getting Help

### When Stuck

1. **Check this guide**: Search for your issue
2. **Review checklist**: Validate prerequisites
3. **Check logs**: Look for error details
4. **Simplify**: Try with simpler story first
5. **Report issue**: Document and escalate

### Reporting Bugs

**Include**:
- Workflow name and version
- Story ID and type
- Error message (full text)
- Steps to reproduce
- Expected vs actual behavior
- System info (OS, Claude version)

---

## 📊 Health Monitoring

### Workflow Health Metrics

**Monitor these indicators**:
```yaml
health_metrics:
  workflow_success_rate: "> 95%"
  average_duration: "40-70 minutes"
  checkpoint_timing: "11-18 minutes"
  pattern_compliance: "> 95%"
  test_coverage: "> 90%"
  doc_drift: "0"
```

**Red Flags**:
- ❌ Success rate < 90%
- ❌ Duration > 2x estimate
- ❌ Pattern compliance < 90%
- ❌ Test coverage < 80%
- ❌ Frequent workflow crashes

---

## 🎓 Best Practices

### Prevention

1. **Validate inputs**: Check story quality before starting
2. **Use #yolo sparingly**: Only for trusted scenarios
3. **Monitor performance**: Track duration and token usage
4. **Keep docs updated**: Run doc-sync regularly
5. **Test incrementally**: Don't wait until the end

### Recovery

1. **Save state**: Checkpoint outputs persist
2. **Resume from checkpoint**: Don't start from scratch
3. **Partial commits**: Commit working pieces
4. **Learn from failures**: Update decision log

---

**Last Updated**: 2025-09-30
**Module Version**: 1.0.0
**For Support**: See main README.md