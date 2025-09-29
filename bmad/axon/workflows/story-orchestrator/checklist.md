# Story Orchestrator Workflow - Validation Checklist

## Story Analysis

- [ ] Story file loaded successfully
- [ ] Story ID extracted (format: AXON-NNN or similar)
- [ ] Story title extracted
- [ ] Module field parsed (Identity/Chat/API/Cross-cutting)
- [ ] Story type field parsed (Feature/Refactor/Bugfix)
- [ ] Priority extracted
- [ ] Complexity extracted
- [ ] Acceptance criteria counted (minimum 1)

## Story Type Detection

- [ ] Story type field checked
- [ ] Story title analyzed for keywords
- [ ] Story type determined: Feature OR Refactor OR Bugfix
- [ ] If ambiguous, user was asked to clarify
- [ ] Target workflow set based on story type:
  - Feature → story-implementation
  - Refactor → story-refactoring
  - Bugfix → story-bugfix

## Module Context Detection

- [ ] Module field checked
- [ ] Story content analyzed for module-specific keywords
- [ ] Module context determined: Identity OR Chat OR API OR Cross-cutting
- [ ] Module enhancement workflow identified (if applicable):
  - Identity → identity-workflow
  - Chat → chat-workflow
  - API → api-workflow
  - Cross-cutting → none

## Routing Decision

- [ ] Primary workflow determined (story-implementation/story-refactoring/story-bugfix)
- [ ] Module enhancement determined (identity/chat/api/none)
- [ ] Workflow path resolved ({project-root}/bmad/axon/workflows/{{target}}/workflow.yaml)
- [ ] Routing rationale documented
- [ ] Routing decision displayed to user
- [ ] User approved routing decision OR chose re-route/abort

## Routing Decision Log

- [ ] Routing decision log created in YAML format
- [ ] Story metadata included (ID, title, type, module, priority, complexity)
- [ ] Routing analysis documented
- [ ] Routing decision documented
- [ ] Execution metadata included (routed_by, routed_at, user_approved)
- [ ] Log saved to {default_output_file}

## Workflow Invocation

- [ ] Target workflow invoked with correct path
- [ ] Story file passed as input
- [ ] Module context passed as input
- [ ] Enhancement workflow passed as input (if applicable)
- [ ] Routing decision path passed as input
- [ ] Workflow execution started successfully
- [ ] Workflow ran to completion (Success/Failed/Partial)

## Post-Execution Summary

- [ ] Workflow status captured (Success/Failed/Partial)
- [ ] Files created count captured
- [ ] Files modified count captured
- [ ] Tests generated count captured
- [ ] Test coverage percentage captured
- [ ] Quality gates passed count captured (X/5)
- [ ] Git commit SHA captured (if committed)
- [ ] AI work time captured
- [ ] Human review time captured (4 checkpoints)
- [ ] Total time calculated
- [ ] Post-execution summary generated and displayed

## Routing Decision Log Update

- [ ] Execution results appended to routing decision log
- [ ] All metrics included (status, files, tests, coverage, gates, commit, times)
- [ ] Completion timestamp added
- [ ] Updated log saved to {default_output_file}

## Final Deliverables

- [ ] Routing decision log complete at {default_output_file}
- [ ] Target workflow executed successfully
- [ ] Post-execution summary provided to user
- [ ] Next steps communicated (review commit, push, PR)

## Issues Found

List any issues discovered during validation:

### Critical Issues (Must fix)
-

### Minor Issues (Can address later)
-