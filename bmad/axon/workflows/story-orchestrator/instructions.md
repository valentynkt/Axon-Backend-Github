# Story Orchestrator - Workflow Instructions

<workflow>

<critical>The workflow execution engine is governed by: {project-root}/bmad/core/tasks/workflow.md</critical>
<critical>You MUST have already loaded and processed: {project-root}/bmad/axon/workflows/story-orchestrator/workflow.yaml</critical>
<critical>This is the MASTER ROUTER - it analyzes stories and delegates to appropriate implementation workflows</critical>

## Overview

The Story Orchestrator is the entry point for all Axon story implementation. It:
- Analyzes story type (Feature/Refactor/Bugfix)
- Detects module context (Identity/Chat/API/Cross-cutting)
- Routes to the appropriate Tier 2 or Tier 3 workflow
- Manages post-execution summary

## Routing Logic

```
Story Type Detection:
  - Feature → story-implementation
  - Refactor → story-refactoring
  - Bugfix → story-bugfix

Module Context Detection:
  - Identity → enhance with identity-workflow
  - Chat → enhance with chat-workflow
  - API → enhance with api-workflow
  - Cross-cutting → use base workflow only

Example Routes:
  - "Add wallet verification" → Feature + Identity → identity-workflow
  - "Optimize message query" → Refactor + Chat → story-refactoring + chat-context
  - "Fix auth token expiry" → Bugfix + Identity → story-bugfix + identity-context
```

---

<step n="1" goal="Load and analyze story">
<action>Read story file from {story_file} input</action>
<action>Parse story metadata:
  - Story ID (e.g., AXON-001)
  - Story title
  - Module context (from "Module:" field)
  - Story type (from "Type:" field)
  - Priority
  - Complexity
</action>

<action>Analyze story content:
  - Read user story statement
  - Count acceptance criteria
  - Review technical context
  - Note dependencies
</action>

<action>Store parsed values:
  - {{story_id}}
  - {{story_title}}
  - {{module}}
  - {{story_type}}
  - {{priority}}
  - {{complexity}}
</action>
</step>

<step n="2" goal="Detect story type (Feature/Refactor/Bugfix)">
<action>Analyze story type field and title:

**Feature Detection:**
- Story Type field = "Feature"
- OR title contains: "Add", "Implement", "Create", "Build", "Enable"
- OR describes new capability

**Refactor Detection:**
- Story Type field = "Refactor"
- OR title contains: "Refactor", "Optimize", "Improve", "Restructure", "Extract"
- OR describes code improvement without new functionality

**Bugfix Detection:**
- Story Type field = "Bugfix" OR "Bug"
- OR title contains: "Fix", "Resolve", "Correct", "Debug"
- OR describes issue resolution
</action>

<action>Set routing target based on story type:
  - Feature → {target_workflow} = "story-implementation"
  - Refactor → {target_workflow} = "story-refactoring"
  - Bugfix → {target_workflow} = "story-bugfix"
</action>

<check>If story type ambiguous → Ask user to clarify</check>
</step>

<step n="3" goal="Detect module context (Identity/Chat/API/Cross-cutting)">
<action>Analyze module field and story content:

**Identity Module:**
- Module field = "Identity"
- OR mentions: authentication, wallet, credential, principal, JWT, Dynamic.xyz
- OR affects: src/Modules/Identity/

**Chat Module:**
- Module field = "Chat"
- OR mentions: conversation, message, AI, Claude, MCP, streaming
- OR affects: src/Modules/Chat/

**API Module:**
- Module field = "API"
- OR mentions: endpoint, FastEndpoints, REST, HTTP, OpenAPI
- OR affects: src/Api/Endpoints/

**Cross-Cutting:**
- Module field = "Cross-cutting" OR "BuildingBlocks"
- OR affects multiple modules
- OR mentions: shared, common, infrastructure
</action>

<action>Set module context:
  - {{module_context}} = "identity" | "chat" | "api" | "cross-cutting"
  - {{module_enhancement}} = "identity-workflow" | "chat-workflow" | "api-workflow" | null
</action>
</step>

<step n="4" goal="Determine final routing decision">
<action>Combine story type and module context to determine workflow:

**Routing Matrix:**

| Story Type | Module | Target Workflow | Enhancement |
|------------|--------|----------------|-------------|
| Feature | Identity | story-implementation | identity-workflow |
| Feature | Chat | story-implementation | chat-workflow |
| Feature | API | story-implementation | api-workflow |
| Feature | Cross-cutting | story-implementation | none |
| Refactor | Identity | story-refactoring | identity-context |
| Refactor | Chat | story-refactoring | chat-context |
| Refactor | Any | story-refactoring | module-context |
| Bugfix | Identity | story-bugfix | identity-context |
| Bugfix | Chat | story-bugfix | chat-context |
| Bugfix | Any | story-bugfix | module-context |

</action>

<action>Set final routing:
  - {{target_workflow}} = primary workflow path
  - {{enhancement_workflow}} = module-specific enhancement (if applicable)
  - {{workflow_path}} = "{project-root}/bmad/axon/workflows/{{target_workflow}}/workflow.yaml"
</action>
</step>

<step n="5" goal="Display routing decision">
<action>Generate routing summary:

**Story Routing Decision**

Story: {{story_title}} ({{story_id}})
Type: {{story_type}}
Module: {{module_context}}
Priority: {{priority}}
Complexity: {{complexity}}

**Routing:**
- Primary Workflow: {{target_workflow}}
- Module Enhancement: {{enhancement_workflow}} (if applicable)
- Workflow Path: {{workflow_path}}

**Rationale:**
[Explain why this route was chosen based on story type and module]

**Expected Outcome:**
[What will be delivered: new code, refactored code, bug fix]

</action>

<ask>
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🎯 STORY ROUTING DECISION
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Review the routing decision above.

Proceed with this workflow?
- [c] Continue - Execute {{target_workflow}}
- [r] Re-route - Choose different workflow
- [a] Abort - Cancel story implementation

Your choice:
</ask>

<action if="user_response == 'r'">
  <ask>Which workflow should be used instead?
    1. story-implementation (Feature)
    2. story-refactoring (Refactor)
    3. story-bugfix (Bugfix)
  </ask>
  <action>Update {{target_workflow}} based on user choice</action>
  <goto step="5">Redisplay routing decision</goto>
</action>

<action if="user_response == 'a'">
  <action>Log routing decision to {default_output_file}</action>
  <action>Exit workflow with summary</action>
</action>
</step>

<step n="6" goal="Save routing decision log">
<action>Generate routing decision log in YAML format:

```yaml
story_id: {{story_id}}
story_title: {{story_title}}
date: {{date}}

routing_analysis:
  story_type_detected: {{story_type}}
  module_context_detected: {{module_context}}
  priority: {{priority}}
  complexity: {{complexity}}

routing_decision:
  target_workflow: {{target_workflow}}
  enhancement_workflow: {{enhancement_workflow}}
  workflow_path: {{workflow_path}}
  rationale: {{routing_rationale}}

execution:
  routed_by: story-orchestrator
  routed_at: {{date}}
  user_approved: true
```
</action>

<action>Save to {default_output_file}</action>
</step>

<step n="7" goal="Invoke target workflow">
<invoke-workflow critical="true">
Execute the target workflow with story context:

Workflow: {{workflow_path}}
Inputs:
  - story_file: {story_file}
  - module_context: {{module_context}}
  - enhancement: {{enhancement_workflow}}
  - routing_decision: {default_output_file}

Wait for workflow completion...
</invoke-workflow>

<critical>The invoked workflow (story-implementation, story-refactoring, or story-bugfix) will handle:
  - All 4 phases (Understanding, Pre-Flight, Implementation, Validation)
  - All 4 checkpoints
  - All agent coordination
  - All quality gates
  - Git commit (if approved)
</critical>
</step>

<step n="8" goal="Post-execution summary">
<action>After target workflow completes, gather results:
  - Workflow status: Success/Failed/Partial
  - Files created/modified
  - Tests generated
  - Test coverage achieved
  - Quality gates passed
  - Git commit SHA (if committed)
  - Time spent (AI + human)
</action>

<action>Generate post-execution summary:

**Story Implementation Complete! 🎉**

Story: {{story_title}} ({{story_id}})
Workflow: {{target_workflow}}
Status: {{workflow_status}}

**Results:**
- Files: {{create_count}} new + {{modify_count}} modified
- Tests: {{test_count}} tests, {{coverage_percent}}% coverage
- Quality Gates: {{gates_passed}}/5 passed
- Commit: {{commit_sha}} (if committed)

**Time Breakdown:**
- AI Work: {{ai_time}} minutes
- Human Review: {{human_time}} minutes (4 checkpoints)
- Total: {{total_time}} minutes

**Artifacts:**
- Implementation log: {{implementation_log_path}}
- Decision log: {{decision_log_path}}
- Routing decision: {default_output_file}

**Next Steps:**
1. Review commit: git log -1
2. Push to remote: git push origin {{branch}}
3. Create PR (if needed)

Story orchestration complete! 🚀
</action>
</step>

<step n="9" goal="Update routing decision log with results">
<action>Append execution results to routing decision log:

```yaml
execution_results:
  status: {{workflow_status}}
  files_created: {{create_count}}
  files_modified: {{modify_count}}
  tests_generated: {{test_count}}
  test_coverage: {{coverage_percent}}
  quality_gates_passed: {{gates_passed}}/5
  commit_sha: {{commit_sha}}
  ai_time_minutes: {{ai_time}}
  human_time_minutes: {{human_time}}
  total_time_minutes: {{total_time}}
  completed_at: {{date}}
```
</action>

<action>Save updated log to {default_output_file}</action>
</step>

</workflow>