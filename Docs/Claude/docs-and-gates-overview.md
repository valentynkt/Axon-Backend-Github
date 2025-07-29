# AXON Docs & Gates Overview

**Purpose.** This document is the **single source of truth** for Axon’s documentation structure, artifact conventions, and gate workflow. It is referenced by `CLAUDE.md` and used by agents to create, validate, and ship docs consistently.

---

## 1) Folder Structure (canonical)

```
docs/
  INDEX.md                       # auto-maintained link map
  DECISION_LOG.md                # append-only; 1 line per merged change
  adr/                           # architecture decision records
    <YYYYMMDD>-<slug>.md
  contracts/
    <Module>/
      API_CONTRACT.md            # one per module (or per service if split later)
  features/
    <Module>/<Feature>/
      REQUIREMENTS.md
      ARCHITECTURE.md
      TASK_PLAN.md
      TEST_REPORT.md
      REVIEW_REPORT.md
      POLICY_REPORT.md
      RESEARCH_NOTES.md
      PR_BODY.md
```

**Rules**

* Do not invent new artifact names or locations.
* Per-feature docs live only under `docs/features/<Module>/<Feature>/`.
* One `API_CONTRACT.md` per module under `docs/contracts/<Module>/`.

---

## 2) Front‑Matter (required on every artifact)

```yaml
---
id: AXON-<YYYYMMDD>-<module>-<feature>-<ARTIFACT>
title: <Feature>: <Artifact Name>
module: <Module>
feature: <Feature>
gate: G1|G2|G3|Ship
owner: <owner>
status: draft|approved|superseded
relates_to: []           # list of AXON-... ids
source_of_truth: doc|code
created: <YYYY-MM-DD>
updated: <YYYY-MM-DD>
version: 1
---
```

**Conventions**

* Dates are ISO (`YYYY-MM-DD`).
* `id` is immutable; bump `version` and `updated` on change.
* `relates_to` links sibling artifacts (e.g., REQUIREMENTS ↔ TASK\_PLAN ↔ ADR).
* `source_of_truth` declares which side wins if code and doc diverge.

---

## 3) Gate Workflow (what “done” means)

| Gate                   | Purpose                         | Required Artifacts                                                                                                | Blocking Conditions                                                           |
| ---------------------- | ------------------------------- | ----------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------- |
| **G1 – Plan**          | Align on *what/why/how*         | `REQUIREMENTS.md`, `TASK_PLAN.md`, (`ARCHITECTURE.md` when design is non-trivial), (optional `RESEARCH_NOTES.md`) | Missing testable ACs; undefined boundaries/ports; no rollback plan            |
| **G2 – Dev/Test**      | Implement slice, prove it       | `TEST_REPORT.md` (with delta coverage on touched files)                                                           | Failing tests; ACs unmet; unexplained coverage drop                           |
| **G3 – Review/Policy** | Human readability + hard policy | `REVIEW_REPORT.md` (advisory), `POLICY_REPORT.md` (blocking)                                                      | Any BLOCKER in policy; unresolved critical review issues                      |
| **Ship**               | Merge-ready with evidence       | `PR_BODY.md`, updated `DECISION_LOG.md`, updated `API_CONTRACT.md` (if surface changed)                           | Missing build/test/health evidence; contract drift; broken links/front‑matter |

**Build/Test/Health evidence** is required at **Ship** (summaries in `PR_BODY.md`).

---

## 4) Artifact Ownership (who writes what)

| Artifact                                                   | Owner (Agent)                | Notes                                        |
| ---------------------------------------------------------- | ---------------------------- | -------------------------------------------- |
| `REQUIREMENTS.md`                                          | **spec‑analyst**             | Testable ACs; minimal scope (YAGNI)          |
| `ARCHITECTURE.md`                                          | **system‑designer\&planner** | Ports, contracts, CQRS mapping, flows        |
| `TASK_PLAN.md`                                             | **system‑designer\&planner** | Exact `files_to_touch`, rollback, milestones |
| `TEST_REPORT.md`                                           | **test‑guardian**            | Delta coverage on touched files; pass/fail   |
| `REVIEW_REPORT.md`                                         | **review‑coach**             | Top‑3 behavior‑safe micro‑refactors          |
| `POLICY_REPORT.md`                                         | **policy‑enforcer**          | Pass/Fail with severities, exact fixes       |
| `RESEARCH_NOTES.md`                                        | **docs‑grounder**            | Primary sources + confidence                 |
| `PR_BODY.md`, `DECISION_LOG.md`, `INDEX.md`, contract sync | **release‑steward**          | Normalize front‑matter, keep links valid     |

---

## 5) Control JSON (lightweight contract for orchestration)

Every agent pairs its Markdown artifact with a control JSON:

```json
{
  "artifact": "REQUIREMENTS|ARCHITECTURE|TASK_PLAN|TEST_REPORT|REVIEW_REPORT|POLICY_REPORT|RESEARCH_NOTES|PR_BODY",
  "module": "<Module>",
  "feature": "<Feature>",
  "gate": "G1|G2|G3|Ship",
  "status": "draft|approved",
  "links": ["AXON-..."],      // optional cross-refs by id
  "summary": "1–2 sentences"
}
```

**Why:** enables routing (e.g., block Ship if POLICY failed; trigger contract sync if implementation flagged `api_surface_changed`).

---

## 6) ADRs & Contracts

* **ADR**: Create under `docs/adr/<YYYYMMDD>-<slug>.md` when boundaries/ports/contracts change or a contentious choice is made. Link it via `relates_to`.
* **API Contracts**: One `API_CONTRACT.md` per module. Surface changes must be reflected here and summarized in `PR_BODY.md` (mark breaking vs non‑breaking). The **release‑steward** updates/validates this at Ship.

---

## 7) Normalization & Index

* **Normalization:** At Ship, release‑steward verifies every feature doc has valid front‑matter, fixes missing fields, and updates `version`/`updated`.
* **Index:** `docs/INDEX.md` lists/links all feature artifacts and contracts; updated by release‑steward.
* **Decision Log:** `docs/DECISION_LOG.md` is append‑only; one line per merge:

```
YYYY-MM-DD | <Module>/<Feature> | <PR# or link> | ADR: <id or n/a> | Contracts: changed/none | <summary>
```

---

## 8) Quality Bars (per gate)

* **G1**: ACs observable, rollback defined, file‑touch plan precise, ports/contracts sketched.
* **G2**: Tests pass; coverage non‑regressive on touched files; ACs satisfied.
* **G3**: Review done (advisory), Policy PASS (no blockers, warnings resolved/justified), analyzers 0 warnings.
* **Ship**: Build green; tests summary in PR; health check success for changed surfaces; contracts + links validated; decision logged.

---

## 9) Naming & IDs

* `id` format: `AXON-YYYYMMDD-<module>-<feature>-<ARTIFACT>`
* ADR file name: `<YYYYMMDD>-<kebab-slug>.md`
* Titles: `<Feature>: <Artifact Name>`
* **Do not** reuse IDs across artifacts; use `relates_to` to connect them.

---

## 10) Example (Chat / Direct\_MCP)

* Feature path: `docs/features/Chat/Direct_MCP/`
* IDs start with `AXON-<date>-chat-direct_mcp-...` (module/feature case normalized as you prefer—be consistent).
* Contract path: `docs/contracts/Chat/API_CONTRACT.md` (update when public surface changes).

---

## 11) Do / Don’t

**Do**

* Keep artifacts short, scannable, and testable.
* List exact `files_to_touch` and a real rollback.
* Use `relates_to` to wire artifacts and ADRs.
* Fail early at gates when the bar isn’t met.

**Don’t**

* Add new artifact types or folders.
* Ship without build/test/health evidence.
* Bury breaking changes; always call them out in PR and contract.

---
