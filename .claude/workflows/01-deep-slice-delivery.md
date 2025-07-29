# Deep Slice Delivery (E2E vertical slice)

P: Ship a new feature slice with minimal diffs and full docs.
Pre: Repo builds clean; feature owner + problem agreed.
In: module, feature.

## G1 – Plan
- spec-analyst → REQUIREMENTS.md (testable ACs; Non‑Goals; blocking Qs if any).
- system-designer&planner → ARCHITECTURE.md + TASK_PLAN.md (exact files_to_touch, rollback, milestones).
- (Optional) docs-grounder → RESEARCH_NOTES.md only if uncertainty.
  Exit G1 when: ACs clear; files_to_touch precise; rollback defined; no blocking Qs.

## G2 – Implement & Test (Parallel)
- slice-implementer → minimal diffs per task; flag api_surface_changed if any.
- test-guardian (run **as soon as first diff exists**) → TEST_REPORT.md (delta coverage on touched files; pass/fail).
  Exit G2 when: tests green; ACs satisfied; no unexplained coverage drop.

## G3 – Review & Policy (Parallel)
- review-coach → REVIEW_REPORT.md (Top‑3 behavior‑safe improvements).
- policy-enforcer → POLICY_REPORT.md (PASS/FAIL; blockers=0; warnings resolved/justified; analyzers=0).
  Exit G3 when: Policy PASS; critical review issues addressed.

## Ship
- release-steward → PR_BODY.md; update DECISION_LOG.md; sync contracts if flagged; normalize front‑matter; update INDEX.md.
- Evidence: Build green, test summary, health check status for changed surfaces.
  Exit Ship when: PR body complete; decision logged; contracts synced; evidence present.

## Arts
REQUIREMENTS, ARCHITECTURE, TASK_PLAN, TEST_REPORT, REVIEW_REPORT, POLICY_REPORT, (opt) RESEARCH_NOTES, PR_BODY, DECISION_LOG.

## Model/Cost
Default Sonnet‑4; aim ≤25k tokens per gate; escalate only if design ambiguity persists.

## RB (Rollback)
If blocked: produce minimal fix list; revert to last milestone from TASK_PLAN; re‑run current gate.

## Orchestrator Hints
- Start test-guardian immediately after first diff.
- Batch agent calls per gate; prefer one synthesized reply per subagent.
- Terminate research early once a primary source supports the choice.
