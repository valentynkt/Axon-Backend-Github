# Boundary Surgery Refactor (structural, no behavior change)

P: Reshape module boundaries/ports safely to reduce coupling and enable future extraction.
Pre: To‑be module graph known; risky releases paused.
In: module(s), feature (if any), current graph, invariants.

## G1 – Plan (Deep, explicit moves)
- system-designer&planner → ARCHITECTURE.md (new boundaries/ports, mapping table: from→to, invariants) + TASK_PLAN.md
    - Exact ordered moves; one reversible step per task.
    - Migration strategy (adapters/shims), feature flags if visible behavior might flicker.
- (Opt, parallel) docs-grounder → RESEARCH_NOTES.md on migration patterns.
  Exit G1 when: ordered file_move list precise; rollback per move defined; risks known.

## G2 – Implement & Test (Iterative)
- slice-implementer → perform **small, reversible moves** (no behavior change); keep diffs localized.
- test-guardian (parallel) → safety harness + regression tests; delta coverage on touched files.
  Exit G2 when: tests pass; behavior unchanged; coverage stable/non‑regressive.

## G3 – Review & Policy (Parallel)
- review-coach → reduce complexity after moves; improve naming/intent.
- policy-enforcer → block on: cross‑module leaks, DI outside Api, CQRS shape, Result usage, analyzers=0.
  Exit G3 when: PASS (blockers=0; warnings resolved/justified).

## Ship
- release-steward → PR_BODY.md (before/after graph, risks, rollback), DECISION_LOG, ADR link, front‑matter normalize, INDEX update.
- Evidence: Build green; test summary; health check for any surfaced endpoints.
  Exit Ship when: evidence attached; ADR linked; PR coherent.

## Arts
ARCHITECTURE, TASK_PLAN, TEST_REPORT, REVIEW_REPORT, POLICY_REPORT, ADR, PR_BODY, DECISION_LOG.

## Model/Cost
Sonnet‑4; brief Opus escalation only if boundary reasoning stalls.

## RB
Revert last move set; re‑enable adapters/shims; re‑run G2 harness.

## Hints
- Keep moves atomic; cap each diff to one responsibility.
- Do not “optimize while moving”; refactor only after policy PASS.
