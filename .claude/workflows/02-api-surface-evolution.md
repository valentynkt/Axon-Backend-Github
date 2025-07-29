# API Surface Evolution (versioning & compatibility)

P: Safely change endpoints/DTOs with clear versioning and comms.
Pre: Impacted consumers known; versioning strategy chosen (additive vs breaking).
In: module, feature; current API_CONTRACT; compatibility goals.

## G1 – Plan
- spec-analyst → REQUIREMENTS.md (compat targets, deprecations, migration intent).
- system-designer&planner → ARCHITECTURE.md + TASK_PLAN.md (DTO boundary rules, mapping, versioning scheme, feature flag/dual-path if needed).
- (Parallel) docs-grounder → RESEARCH_NOTES.md (official guidance on API versioning for stack).
  Exit G1 when: mapping & versioning rules explicit; file list + rollback defined.

## G2 – Implement & Test (Parallel)
- slice-implementer → additive first where possible; guard with feature flag.
- test-guardian → contract tests + migration tests; TEST_REPORT.md with before/after failure modes.
  Exit G2 when: all contract tests pass; ACs met.

## G3 – Review & Policy (Parallel)
- review-coach → readability of DTOs/mappers; intent‑revealing names.
- policy-enforcer → PASS/FAIL with focus on:
    - No Domain entities crossing API boundary
    - Result mapping at boundary
    - Analyzer warnings=0; secrets/logging hygiene
      Exit G3 when: PASS; warnings handled/justified.

## Ship
- release-steward → update docs/contracts/<Module>/API_CONTRACT.md (mark **breaking** vs **non‑breaking**), PR_BODY.md with migration notes, DECISION_LOG entry; front‑matter normalized; INDEX updated.
- Evidence: Build, tests, **health check** on changed endpoints (status + env + timestamp).
  Exit Ship when: contract synced; breaking changes called out; evidence attached.

## Arts
REQUIREMENTS, ARCHITECTURE, TASK_PLAN, TEST_REPORT, REVIEW_REPORT, POLICY_REPORT, RESEARCH_NOTES, PR_BODY, updated API_CONTRACT, DECISION_LOG.

## Model/Cost
Default Sonnet‑4; keep research concise (primary sources > secondary).

## RB (Rollback)
Feature flag off; maintain old path until consumers migrate; revert commit set from TASK_PLAN.

## Orchestrator Hints
- Treat `api_surface_changed=true` from slice-implementer as a hard signal to enforce contract sync at Ship.
- Require explicit “compat window” note in PR when breaking changes exist.
