#

 Story Orchestrator Workflow

**Version**: 1.0
**Author**: Axon Module
**Type**: Routing/Meta Workflow
**Complexity**: Low
**Estimated Duration**: 2-5 minutes

---

## Purpose

The **Story Orchestrator** is the master entry point for all Axon story implementation. It intelligently routes stories to the appropriate implementation workflow based on story type and module context.

Think of it as a smart router that:
- ✅ Analyzes story characteristics
- ✅ Routes to appropriate Tier 2 workflow (story-implementation/story-refactoring/story-bugfix)
- ✅ Enhances with Tier 3 module context (Identity/Chat/API)
- ✅ Tracks routing decisions
- ✅ Summarizes execution results

---

## When to Use

Use this workflow as the **primary entry point** for all story implementation:
- ✅ You have a story file (story-NNN.md) ready
- ✅ Story type is clear (Feature/Refactor/Bugfix)
- ✅ Module context is known (Identity/Chat/API/Cross-cutting)
- ✅ You want intelligent routing to the right workflow

**Always use Story Orchestrator** instead of calling implementation workflows directly.

---

## Routing Logic

### Story Type → Tier 2 Workflow

| Story Type | Target Workflow | Purpose |
|------------|----------------|---------|
| **Feature** | story-implementation | New functionality (greenfield in brownfield) |
| **Refactor** | story-refactoring | Code improvement, restructuring |
| **Bugfix** | story-bugfix | Issue resolution, root cause analysis |

### Module Context → Tier 3 Enhancement

| Module | Enhancement | Adds |
|--------|-------------|------|
| **Identity** | identity-workflow | Auth, wallets, credentials context (5 docs) |
| **Chat** | chat-workflow | Conversations, messages, AI context (5 docs) |
| **API** | api-workflow | FastEndpoints, REST patterns context (3 docs) |
| **Cross-cutting** | none | Core patterns only (no module docs) |

### Routing Matrix Examples

```
"Add wallet auto-revocation"
  → Feature + Identity
  → story-implementation + identity-workflow

"Optimize message query performance"
  → Refactor + Chat
  → story-refactoring + chat-context

"Fix auth token expiry bug"
  → Bugfix + Identity
  → story-bugfix + identity-context

"Add FastEndpoint for user search"
  → Feature + API
  → story-implementation + api-workflow

"Refactor Result<T> error handling"
  → Refactor + Cross-cutting
  → story-refactoring (no module enhancement)
```

---

## Prerequisites

### Required Input

**Story File** (`story-NNN.md`) with:
- Story ID
- Story title
- Module field (Identity/Chat/API/Cross-cutting)
- Story type field (Feature/Refactor/Bugfix)
- Priority
- Complexity
- User story statement
- Acceptance criteria

### Example Story Metadata

```markdown
# Story: Add Wallet Auto-Revocation

**Story ID**: AXON-042
**Module**: Identity
**Type**: Feature
**Priority**: High
**Complexity**: Medium
...
```

---

## Usage

### From Axon Story Orchestrator Agent

```
@axon-story-orchestrator
*implement-story
[Provide story file path: stories/axon-042.md]
```

### Direct Workflow Execution

```
[Invoke workflow]
workflow: bmad/axon/workflows/story-orchestrator/workflow.yaml
story_file: path/to/story-042.md
```

---

## Workflow Steps

| Step | Goal | Duration |
|------|------|----------|
| 1 | Load and analyze story | 30 sec |
| 2 | Detect story type (Feature/Refactor/Bugfix) | 15 sec |
| 3 | Detect module context (Identity/Chat/API) | 15 sec |
| 4 | Determine final routing decision | 15 sec |
| 5 | Display routing decision, get user approval | 1 min |
| 6 | Save routing decision log | 15 sec |
| 7 | Invoke target workflow | 30-60 min (target workflow time) |
| 8 | Post-execution summary | 1 min |
| 9 | Update routing decision log with results | 30 sec |

**Total Orchestrator Time**: 2-5 minutes (excluding target workflow execution)
**Total End-to-End**: 35-65 minutes (includes target workflow)

---

## Outputs

### Routing Decision Log

File: `{output_folder}/routing-decisions/{{story_id}}-routing.yaml`

```yaml
story_id: AXON-042
story_title: "Add Wallet Auto-Revocation"
date: 2025-09-30

routing_analysis:
  story_type_detected: Feature
  module_context_detected: identity
  priority: High
  complexity: Medium

routing_decision:
  target_workflow: story-implementation
  enhancement_workflow: identity-workflow
  workflow_path: "{project-root}/bmad/axon/workflows/story-implementation/workflow.yaml"
  rationale: "Feature story for Identity module requires story-implementation with Identity specialization"

execution:
  routed_by: story-orchestrator
  routed_at: 2025-09-30 10:30:00
  user_approved: true

execution_results:
  status: Success
  files_created: 8
  files_modified: 3
  tests_generated: 15
  test_coverage: 92%
  quality_gates_passed: 5/5
  commit_sha: abc123def
  ai_time_minutes: 45
  human_time_minutes: 15
  total_time_minutes: 60
  completed_at: 2025-09-30 11:30:00
```

### Post-Execution Summary

Displayed to user after target workflow completes:

```
Story Implementation Complete! 🎉

Story: Add Wallet Auto-Revocation (AXON-042)
Workflow: story-implementation
Status: Success

Results:
- Files: 8 new + 3 modified
- Tests: 15 tests, 92% coverage
- Quality Gates: 5/5 passed
- Commit: abc123def

Time Breakdown:
- AI Work: 45 minutes
- Human Review: 15 minutes (4 checkpoints)
- Total: 60 minutes

Artifacts:
- Implementation log: Docs/PROCESS/active-stories/AXON-042/implementation-log.md
- Decision log: Docs/PROCESS/active-stories/AXON-042/decisions.yaml
- Routing decision: Docs/PROCESS/routing-decisions/AXON-042-routing.yaml

Next Steps:
1. Review commit: git log -1
2. Push to remote: git push origin feature/axon-042
3. Create PR (if needed)

Story orchestration complete! 🚀
```

---

## Routing Decision Approval

The orchestrator asks for user approval before invoking the target workflow:

```
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🎯 STORY ROUTING DECISION
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Story: Add Wallet Auto-Revocation (AXON-042)
Type: Feature
Module: identity

Routing:
- Primary Workflow: story-implementation
- Module Enhancement: identity-workflow

Proceed with this workflow?
- [c] Continue - Execute story-implementation
- [r] Re-route - Choose different workflow
- [a] Abort - Cancel story implementation

Your choice:
```

---

## Configuration

Configured in `bmad/axon/config.yaml`:

```yaml
# Routing targets
workflows:
  tiers:
    master: [story-orchestrator]
    core: [story-implementation, story-refactoring, story-bugfix]
    module: [identity-workflow, chat-workflow, api-workflow]

# Output paths
output_folder: "{project-root}/Docs/PROCESS/active-stories"
routing_log_folder: "{output_folder}/routing-decisions"
```

---

## Troubleshooting

### Issue: Story type ambiguous

**Symptoms**: Orchestrator cannot determine Feature vs Refactor vs Bugfix
**Solution**:
1. Check story file has clear "Type:" field
2. If missing, orchestrator will ask user to clarify
3. Use keywords in title: "Add" (Feature), "Refactor" (Refactor), "Fix" (Bugfix)

### Issue: Module context unclear

**Symptoms**: Module detected as "cross-cutting" when it should be specific
**Solution**:
1. Set "Module:" field explicitly in story file
2. Include module-specific keywords in story content (auth/wallet for Identity, message/conversation for Chat)
3. Orchestrator will ask for clarification if truly ambiguous

### Issue: Workflow not found

**Symptoms**: Error invoking target workflow
**Solution**:
1. Verify workflow exists at: `bmad/axon/workflows/{{target}}/workflow.yaml`
2. Check Phase 3 completion: story-implementation, story-refactoring, story-bugfix must exist
3. For Phase 4 workflows (identity/chat/api), they may not be created yet

### Issue: Routing decision log not saved

**Symptoms**: Cannot find {output_folder}/routing-decisions/{{story_id}}-routing.yaml
**Solution**:
1. Check output_folder exists
2. Create routing-decisions directory if missing: `mkdir -p Docs/PROCESS/routing-decisions`
3. Verify write permissions

---

## Related Workflows

**This workflow routes to:**
- **story-implementation**: Feature development (Tier 2)
- **story-refactoring**: Safe brownfield refactoring (Tier 2)
- **story-bugfix**: Issue resolution (Tier 2)

**With optional module enhancements:**
- **identity-workflow**: Identity specialization (Tier 3)
- **chat-workflow**: Chat specialization (Tier 3)
- **api-workflow**: API specialization (Tier 3)

**Called by:**
- **Axon Story Orchestrator Agent** (`@axon-story-orchestrator` command `*implement-story`)

---

## Success Metrics

- **Routing Accuracy**: 95%+ correct workflow selection
- **User Confirmation**: 1 approval required (routing decision)
- **Overhead**: 2-5 minutes (minimal routing time)
- **Decision Traceability**: 100% (all decisions logged)

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-09-30 | Initial release (Phase 3 implementation) |

---

**Status**: ✅ **Production-Ready** (Phase 3 Complete - Master Router)
**Next**: Build Tier 2 workflows (story-refactoring, story-bugfix) for complete routing coverage