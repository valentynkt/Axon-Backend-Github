# Hotpath Performance Hardening (measure → change → verify)

P: Improve latency/throughput on a hot path without changing behavior.
Pre: Baseline metrics known (p95/p99 latency, throughput, error rate), target SLOs defined.
In: module, feature (if applicable), hot path entry points, baseline.

## G1 – Plan (Measure-first)
- spec-analyst → REQUIREMENTS.md (perf ACs with SLOs, load profile).
- system-designer&planner → ARCHITECTURE.md (bottleneck hypothesis, safe optimizations: alloc cuts, async I/O, batching, caching **queries only**, data shape tweaks) + TASK_PLAN.md (measurement plan, one-change-at-a-time, rollback).
- (Opt) docs-grounder → RESEARCH_NOTES.md on vetted perf patterns.
  Exit G1 when: measurement plan ready; ordered optimizations listed; rollback for each change defined.

## G2 – Implement & Test (One change at a time)
- slice-implementer → apply a single optimization per diff; keep code obvious.
- test-guardian → bench/measurement harness; before/after table; regression tests for behavior equivalence.
  Exit G2 when: SLO deltas measured; behavior unchanged; tests pass.

## G3 – Review & Policy (Parallel)
- review-coach → simplify hotspot code, intent‑revealing names.
- policy-enforcer → block on: sync-over-async, CT propagation missing, cache on commands, analyzer warnings>0, logging/trace gaps.
  Exit G3 when: PASS; warnings handled/justified.

## Ship
- release-steward → PR_BODY.md with perf table (baseline vs result), risks/rollback, DECISION_LOG; normalize docs; update INDEX.
- Evidence: Build green; tests; **health check** for the optimized surface where applicable.
  Exit Ship when: SLOs met or justified; evidence present.

## Arts
REQUIREMENTS, ARCHITECTURE, TASK_PLAN, TEST_REPORT (perf table), REVIEW_REPORT, POLICY_REPORT, PR_BODY, DECISION_LOG, (opt) RESEARCH_NOTES.

## Model/Cost
Sonnet‑4; avoid speculative exploration; measure, decide, proceed.

## RB
Revert the last optimization; restore prior config; confirm metrics return to baseline.

## Hints
- Avoid premature micro‑tuning; remove allocations/branches only with evidence.
- Keep perf changes isolated to enable clean reverts.
