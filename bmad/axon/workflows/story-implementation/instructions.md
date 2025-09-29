# Story Implementation - Workflow Instructions

<workflow>

<critical>The workflow execution engine is governed by: {project-root}/bmad/core/tasks/workflow.md</critical>
<critical>You MUST have already loaded and processed: {project-root}/bmad/axon/workflows/story-implementation/workflow.yaml</critical>
<critical>This workflow implements the Axon 4-phase approach: Understanding → Pre-Flight → Implementation → Validation</critical>

## Overview

This workflow implements new features using:
- **Doc-grounded**: Every decision validated against `/Docs`
- **Discovery-first**: Search before creating (Archaeologist)
- **Library-aware**: Use tools, not manual code (Library Sage)
- **Pattern-compliant**: Follow established patterns exactly (Doc Oracle)
- **4 strategic checkpoints**: Batched approvals (11-18 min total)

## Workflow Phases

### Phase 0: Story Understanding (Doc-Grounding)
### Phase 1: Pre-Flight Validation (Discovery + Library + Pattern)
### Phase 2: Implementation (Code Generation)
### Phase 3: Validation (Tests + Doc Sync + Learning)

---

<step n="0" goal="Initialize workflow and load story">
<action>Read story file from {story_file} input</action>
<action>Parse story metadata:
  - Story ID
  - Story title
  - Module context (Identity/Chat/API/Cross-cutting)
  - Story type (Feature confirmed)
  - Acceptance criteria (list)
  - Tech spec reference (if BMM handoff)
</action>
<action>Set workflow context variables:
  - {{story_id}}
  - {{story_title}}
  - {{module}}
  - {{acceptance_criteria}}
  - {{tech_spec_path}} (optional)
</action>
<action>Create output directory: {default_output_folder}</action>
<critical>All subsequent steps use these variables</critical>
</step>

---

## PHASE 0: STORY UNDERSTANDING (DOC-GROUNDING)

<step n="1" goal="Load core documentation hub (progressive loading)">
<action>Load core hub documentation (ALWAYS loaded):
  - {engineering_docs}/00-START-HERE.md
  - {engineering_docs}/guides/patterns/00-QUICK-REFERENCE.md  # Contains 80% of patterns
  - {engineering_docs}/guides/architecture/system-overview.md # Module boundaries
  - {libraries_docs}/00-INDEX.md
</action>

<action>Load story template for reference:
  - {story_template}
</action>

<critical>Do NOT load all 120+ docs upfront - use hub-and-spoke expansion in later steps</critical>
</step>

<step n="2" goal="Understand story requirements and context">
<action>Analyze story content:
  1. Parse user story (As a... I want... So that...)
  2. Extract acceptance criteria (must be testable)
  3. Identify technical context (patterns required, module boundaries)
  4. Note dependencies and integrations
  5. Review test strategy
  6. Check for risks and assumptions
</action>

<action>If tech_spec_path exists:
  - Load BMM tech-spec document
  - Cross-reference AC with tech-spec
  - Note detailed design sections
  - Verify architecture alignment
</action>

<action>Identify module-specific context:
  - Identity → Load 5 Identity module docs (00-INDEX, 01-domain-model, 03-authentication, 05-api-contracts, 06-database-schema)
  - Chat → Load 5 Chat module docs (same pattern)
  - API → Load API docs (00-INDEX.md) + FastEndpoints/FluentValidation guides
  - Cross-cutting → Load only core hub
</action>

<critical>Module docs are loaded NOW based on story module context</critical>
</step>

<step n="3" goal="Generate story understanding summary">
<template-output section="story_understanding">
Generate a concise summary (1-2 paragraphs max):

**Story Understanding Summary**

Story: {{story_title}} ({{story_id}})
Module: {{module}}
Type: Feature Implementation

**Key Requirements:**
- [List 3-5 key requirements from AC]

**Technical Approach:**
- Patterns: [Result<T>, StrongId<T>, CQRS, etc.]
- Module Context: [Identity/Chat/API specific considerations]
- Dependencies: [Libraries, modules, external services]

**Implementation Layers:**
- Domain: [What needs to be created/modified]
- Application: [Commands/Queries needed]
- Infrastructure: [Repositories, external service integrations]
- API: [Endpoints to create/modify]

**Estimated Complexity:** [Simple/Medium/Complex]
</template-output>

<critical>Save this summary to {implementation_log}</critical>
</step>

---

## ✅ CHECKPOINT 1: STORY UNDERSTANDING APPROVAL (1 min)

<step n="4" goal="Checkpoint 1: User approves understanding">
<action>Display story understanding summary from step 3</action>
<ask critical="true">
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ CHECKPOINT 1: STORY UNDERSTANDING APPROVAL
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Review the story understanding summary above.

Do you approve this understanding?
- [c] Continue to Pre-Flight Validation
- [e] Edit/clarify requirements
- [a] Abort workflow

Your choice:
</ask>

<action if="user_response == 'e'">
  <ask>What needs clarification?</ask>
  <action>Update story understanding based on feedback</action>
  <goto step="3">Regenerate summary with clarifications</goto>
</action>

<action if="user_response == 'a'">
  <action>Log abortion reason</action>
  <action>Exit workflow with summary</action>
</action>

<critical>User MUST approve before continuing to Phase 1</critical>
</step>

---

## PHASE 1: PRE-FLIGHT VALIDATION (DISCOVERY + LIBRARY + PATTERN)

<step n="5" goal="Parallel validation: Discovery, Library Check, Pattern Compliance">
<critical>This step coordinates 3 agents running IN PARALLEL (not sequential)</critical>

<action>Launch parallel validation tracks:

**Track A: Codebase Discovery (Archaeologist)**
- Search for existing implementations in {{module}}
- Map available APIs across all layers (Domain/Application/Infrastructure/API)
- Find similar patterns (Result<T>, StrongId<T>, CQRS examples)
- Identify reusable code (Reuse Directly, Extend, Adapt, Create New)
- Generate reuse-report.md

**Track B: Library Capability Check (Library Sage)**
- Check if requirement can be solved by existing libraries (11 core libraries)
- Recommend approach: Library-First vs Manual vs Hybrid (4-factor scoring)
- Show usage patterns from codebase
- Validate library version compatibility
- Generate library-recommendations.md

**Track C: Pattern Compliance Validation (Doc Oracle)**
- Validate story against ADRs (6 ADRs: Modular Monolith, CQRS, Result Pattern, Strong IDs, PostgreSQL, FastEndpoints)
- Check pattern requirements (Result<T> for errors, StrongId<T> for IDs, CQRS separation)
- Detect potential drift points
- Calculate compliance score (6 dimensions weighted)
- Generate pattern-compliance-report.md
</action>

<critical>All 3 tracks must complete before proceeding to Checkpoint 2</critical>
</step>

<step n="6" goal="Synthesize pre-flight validation results">
<action>Combine results from 3 parallel tracks:
  1. Archaeology findings (reuse opportunities)
  2. Library recommendations (library vs manual)
  3. Pattern compliance assessment (violations, risks)
</action>

<template-output section="preflight_validation">
Generate pre-flight validation package:

**Pre-Flight Validation Package**

**🔍 Discovery Results:**
- Existing implementations found: [Count]
- Reusable APIs: [List with file:line]
- Similar patterns: [List with examples]
- Recommendation: [Reuse Directly / Extend / Adapt / Create New]

**🛠️ Library Analysis:**
- Requirement: [What needs to be built]
- Library Solution Available: [Yes/No]
- Recommended Approach: [Library-First / Manual / Hybrid]
- Scoring: Availability (40%), Effort (30%), Maintainability (20%), Performance (10%)
- Libraries to use: [List with versions]

**✅ Pattern Compliance:**
- ADR alignment: [6/6 ADRs checked]
- Violations detected: [None / List]
- Pattern requirements: [Result<T> ✓, StrongId<T> ✓, CQRS ✓]
- Compliance score: [X/100]
- Drift risk: [Low/Medium/High]

**Implementation Plan:**
- Approach: [Summary of recommended approach]
- Layers to modify: [Domain, Application, Infrastructure, API]
- Risks: [Any identified risks]
</template-output>

<critical>Save to {default_output_folder}/preflight-package.md</critical>
</step>

---

## ✅ CHECKPOINT 2: PRE-FLIGHT APPROVAL (3-5 min)

<step n="7" goal="Checkpoint 2: User approves pre-flight plan">
<action>Display pre-flight validation package from step 6</action>
<ask critical="true">
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ CHECKPOINT 2: PRE-FLIGHT APPROVAL
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Review the pre-flight validation package above.

Key findings:
- Discovery: {{reuse_recommendation}}
- Library: {{library_approach}}
- Patterns: {{compliance_score}}/100

Do you approve this implementation plan?
- [c] Continue to Implementation
- [e] Edit/adjust approach
- [b] Back to story understanding
- [a] Abort workflow

Your choice:
</ask>

<action if="user_response == 'e'">
  <ask>What needs adjustment? (discovery findings, library choice, pattern approach)</ask>
  <action>Update pre-flight plan based on feedback</action>
  <goto step="6">Regenerate pre-flight package</goto>
</action>

<action if="user_response == 'b'">
  <goto step="3">Return to story understanding</goto>
</action>

<action if="user_response == 'a'">
  <action>Log abortion reason</action>
  <action>Exit workflow with pre-flight summary</action>
</action>

<critical>User MUST approve before code generation begins</critical>
</step>

---

## PHASE 2: IMPLEMENTATION (CODE GENERATION)

<step n="8" goal="Generate implementation plan with diff preview">
<action>Based on approved pre-flight package, create detailed implementation plan:
  1. Bottom-up layering: Domain → Application → Infrastructure → API
  2. For each layer, list files to create/modify
  3. Apply patterns: Result<T>, StrongId<T>, CQRS
  4. Integrate library recommendations
  5. Follow module-specific patterns (Identity: owned entities, Chat: business rules, API: FastEndpoints)
</action>

<action>Generate unified diff preview for ALL changes:
  - New files: Show full content
  - Modified files: Show before/after diffs
  - Deleted files: Show what's being removed
</action>

<template-output section="implementation_plan">
**Implementation Plan - Diff Preview**

**Changes Summary:**
- Files to create: [Count]
- Files to modify: [Count]
- Total lines: [~Estimate]

**Layer-by-Layer Changes:**

### Domain Layer (src/Modules/{{module}}/Domain/)
```diff
+ NewFile: Aggregates/{{EntityName}}.cs
+ NewFile: ValueObjects/{{ValueObjectName}}.cs
+ NewFile: Errors/{{ErrorName}}.cs
[Show full content for new files]

~ ModifyFile: Aggregates/ExistingAggregate.cs
[Show unified diff]
```

### Application Layer (src/Modules/{{module}}/Application/)
```diff
+ NewFile: Commands/{{CommandName}}.cs
+ NewFile: Queries/{{QueryName}}.cs
+ NewFile: CommandHandlers/{{HandlerName}}.cs
[Show full content]
```

### Infrastructure Layer (src/Modules/{{module}}/Infrastructure/)
```diff
+ NewFile: Repositories/{{RepositoryName}}.cs
+ NewFile: Services/{{ServiceName}}.cs
[Show full content]

~ ModifyFile: DependencyInjection/{{Module}}Configuration.cs
[Show unified diff]
```

### API Layer (src/Api/Endpoints/{{Module}}/)
```diff
+ NewFile: {{EndpointName}}.cs
[Show full content with FastEndpoints pattern]
```

**Pattern Compliance Check:**
- Result<T> usage: ✅ All error paths
- StrongId<T> usage: ✅ All entity IDs
- CQRS separation: ✅ Commands/Queries distinct
- Library integration: ✅ {{library_names}}
</template-output>

<critical>Save to {default_output_folder}/implementation-diff.md</critical>
</step>

---

## ✅ CHECKPOINT 3: IMPLEMENTATION PREVIEW APPROVAL (5-10 min)

<step n="9" goal="Checkpoint 3: User approves diff preview">
<action>Display implementation plan and diff preview from step 8</action>
<ask critical="true">
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ CHECKPOINT 3: IMPLEMENTATION PREVIEW APPROVAL
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Review the implementation diff preview above.

Changes:
- Files to create: {{create_count}}
- Files to modify: {{modify_count}}
- Estimated lines: {{loc_estimate}}

Pattern compliance: ✅ All patterns applied

Do you approve these changes?
- [c] Continue - Apply changes now
- [e] Edit - Adjust implementation
- [b] Back to pre-flight
- [a] Abort workflow

Your choice:
</ask>

<action if="user_response == 'e'">
  <ask>What needs adjustment? (specific files, patterns, approach)</ask>
  <action>Update implementation plan based on feedback</action>
  <goto step="8">Regenerate diff preview</goto>
</action>

<action if="user_response == 'b'">
  <goto step="6">Return to pre-flight validation</goto>
</action>

<action if="user_response == 'a'">
  <action>Log abortion reason</action>
  <action>Exit workflow with preview</action>
</action>

<critical>User MUST approve before files are modified</critical>
</step>

<step n="10" goal="Apply implementation changes (Implementation Surgeon)">
<action>Execute all changes from approved diff preview:
  1. Create new directories if needed
  2. Create new files with full content
  3. Modify existing files using surgical edits
  4. Add XML documentation to all public members
  5. Ensure consistent formatting
</action>

<action>For each file created/modified:
  - Validate syntax (C# compilation check)
  - Verify pattern compliance (Result<T>, StrongId<T>, CQRS)
  - Check library integration correctness
  - Add inline XML docs
</action>

<action>Generate implementation summary:
  - Files created: [List with paths]
  - Files modified: [List with paths]
  - Total lines added: [Count]
  - Patterns applied: [List]
</action>

<critical>All code must be generated before moving to Phase 3</critical>
</step>

<step n="11" goal="Detect documentation drift">
<action>Compare new code against documentation:
  1. Check if new APIs are documented (API docs, module docs)
  2. Identify new patterns not in pattern docs
  3. Detect architectural changes not in ADRs
  4. Find new domain concepts not in domain-model docs
</action>

<action>Generate drift report:
  - Drift detected: [Yes/No]
  - Affected docs: [List with specific sections]
  - Recommended updates: [List]
</action>

<critical>Drift report feeds into Phase 3 doc sync</critical>
</step>

---

## PHASE 3: VALIDATION (TESTS + DOC SYNC + LEARNING)

<step n="12" goal="Generate comprehensive test suite (Quality Guardian)">
<action>Generate tests for all layers:

**Domain Unit Tests** (tests/Modules/{{module}}/Domain/)
- Test all domain logic (aggregates, value objects, domain events)
- Test error paths (Result<T> error cases)
- Test business rules
- Use AAA pattern (Arrange-Act-Assert)
- Use Shouldly assertions

**Application Integration Tests** (tests/Modules/{{module}}/Application/)
- Test command handlers end-to-end
- Test query handlers with test database
- Use Testcontainers for PostgreSQL
- Test error handling and validation

**API E2E Tests** (tests/Modules/{{module}}/E2E/)
- Test endpoints with real HTTP requests
- Test request/response mapping
- Test validation errors
- Test authentication/authorization

**AC Coverage Tests** (tests/Modules/{{module}}/AC/)
- One test per acceptance criterion
- Given-When-Then naming: Given_Context_When_Action_Then_ExpectedResult
- Use story AC exactly as written
</action>

<action>Save all test files to appropriate directories</action>
<action>Verify test count: Minimum 90% code coverage target</action>
</step>

<step n="13" goal="Run build and tests">
<action>Execute dotnet build:
  - dotnet build --configuration Release
  - Warnings-as-errors must pass
  - All projects must compile
</action>

<action if="build_fails">
  <action>Capture build errors</action>
  <action>Fix compilation issues</action>
  <action>Retry build</action>
</action>

<action if="build_succeeds">
  <action>Execute dotnet test:
    - dotnet test --collect:"XPlat Code Coverage"
    - All tests must pass
    - Calculate coverage percentage
  </action>
</action>

<action>Generate build/test report:
  - Build status: [Success/Failed]
  - Test results: [Passed/Failed/Skipped]
  - Coverage: [X%]
  - Failed tests: [List if any]
</action>

<critical>All tests MUST pass before proceeding</critical>
</step>

<step n="14" goal="Validate acceptance criteria coverage">
<action>For each AC from story:
  1. Verify test exists with AC-specific name
  2. Verify test passes
  3. Verify test covers AC completely
</action>

<action>Generate AC validation report:
  - Total ACs: [Count]
  - ACs with tests: [Count]
  - ACs fully covered: [Count]
  - Coverage: [X%] (target: 100%)
  - Missing tests: [List if any]
</action>

<critical>100% AC coverage is MANDATORY</critical>
</step>

<step n="15" goal="Final compliance check">
<action>Validate all quality gates:
  - ✅ Pattern compliance: 95%+ (Result<T>, StrongId<T>, CQRS)
  - ✅ Test coverage: 90%+
  - ✅ AC coverage: 100%
  - ✅ Build success: 100%
  - ✅ Doc drift: Addressed
</action>

<action>Generate final compliance report:
  - Quality gates: [X/5 passed]
  - Pattern violations: [None/List]
  - Test coverage: [X%]
  - AC coverage: [X%]
  - Ready for commit: [Yes/No]
</action>

<critical>All 5 quality gates must pass</critical>
</step>

<step n="16" goal="Synchronize documentation (Doc Oracle + Quality Guardian)">
<action>Apply doc updates from drift report (step 11):
  1. Update module docs with new APIs
  2. Update pattern docs if new patterns introduced
  3. Update ADRs if architectural decisions made
  4. Update domain-model docs with new concepts
</action>

<action>For each doc update:
  - Show before/after diff
  - Verify accuracy
  - Maintain consistent terminology
</action>

<action>Generate doc sync summary:
  - Docs updated: [Count]
  - Files modified: [List]
  - Drift eliminated: [Yes/No]
</action>

<critical>Zero documentation drift after this step</critical>
</step>

<step n="17" goal="Capture decisions and learning">
<action>Generate decision log in YAML format:

```yaml
story_id: {{story_id}}
story_title: {{story_title}}
date: {{date}}
module: {{module}}

decisions:
  - decision: {{decision_description}}
    rationale: {{why_this_decision}}
    alternatives_considered: [{{alternative_1}}, {{alternative_2}}]
    tradeoffs: {{tradeoffs_explanation}}
    outcome: {{what_was_implemented}}

  - decision: {{another_decision}}
    ...

learnings:
  - what_went_well: {{success_factor}}
  - what_to_improve: {{improvement_area}}
  - patterns_applied: [Result<T>, StrongId<T>, CQRS]
  - libraries_used: [{{library_1}}, {{library_2}}]

technical_debt:
  - item: {{debt_description}}
    severity: [Low/Medium/High]
    mitigation: {{how_to_address}}

metrics:
  implementation_time: {{time_estimate}}
  test_coverage: {{coverage_percent}}
  ac_coverage: 100%
  pattern_compliance: {{compliance_score}}
```
</action>

<action>Save to {decision_log_path}</action>
<critical>Decision log enables continuous learning for future stories</critical>
</step>

---

## ✅ CHECKPOINT 4: FINAL COMMIT APPROVAL (2 min)

<step n="18" goal="Checkpoint 4: User approves final commit">
<action>Display complete implementation summary:
  - Story: {{story_title}} ({{story_id}})
  - Files created: {{create_count}}
  - Files modified: {{modify_count}}
  - Tests generated: {{test_count}}
  - Test coverage: {{coverage_percent}}
  - AC coverage: 100%
  - Build status: ✅ Success
  - Quality gates: ✅ 5/5 passed
  - Docs updated: {{doc_count}} files
  - Decision log: ✅ Captured
</action>

<ask critical="true">
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ CHECKPOINT 4: FINAL COMMIT APPROVAL
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Implementation complete! All quality gates passed.

Summary:
- Code: {{create_count}} new + {{modify_count}} modified files
- Tests: {{test_count}} tests, {{coverage_percent}}% coverage
- Docs: {{doc_count}} files updated
- Build: ✅ Success
- Quality: ✅ 5/5 gates passed

Ready to commit?
- [c] Commit - Create git commit now
- [r] Review - Show detailed summary
- [e] Edit - Make adjustments
- [d] Defer - Save but don't commit yet

Your choice:
</ask>

<action if="user_response == 'r'">
  <action>Show detailed file-by-file summary</action>
  <action>Show test results breakdown</action>
  <action>Show doc changes</action>
  <goto step="18">Return to commit prompt</goto>
</action>

<action if="user_response == 'e'">
  <ask>What needs adjustment?</ask>
  <action>Make requested changes</action>
  <action>Re-run tests</action>
  <goto step="18">Return to commit prompt</goto>
</action>

<action if="user_response == 'd'">
  <action>Save all changes without commit</action>
  <action>Generate "deferred commit" summary</action>
  <action>Exit workflow</action>
</action>

<critical>Only proceed to commit if user explicitly approves with 'c'</critical>
</step>

<step n="19" goal="Create git commit (if approved)" optional="true">
<action if="user_approved_commit">
  Execute git commit sequence:
  1. git add . (stage all changes)
  2. git status (verify staged files)
  3. git commit with message:

  ```
  feat({{module}}): {{story_title}}

  Implements story {{story_id}}: {{user_story_summary}}

  Changes:
  - {{change_1}}
  - {{change_2}}
  - {{change_3}}

  Tests:
  - {{test_count}} tests added
  - {{coverage_percent}}% coverage
  - 100% AC coverage

  Docs:
  - {{doc_count}} files updated

  Quality Gates: ✅ 5/5 passed

  🤖 Generated with Axon Module (BMAD)
  Co-Authored-By: {{user_name}}
  ```
</action>

<action>Verify commit succeeded: git log -1</action>
<action>Display commit SHA and summary</action>
</step>

---

## WORKFLOW COMPLETION

<step n="20" goal="Final summary and next steps">
<action>Generate workflow completion report:

**Story Implementation Complete! 🎉**

Story: {{story_title}} ({{story_id}})
Module: {{module}}
Status: ✅ Implemented & Committed

**Deliverables:**
- Code: {{create_count}} new + {{modify_count}} modified files
- Tests: {{test_count}} tests ({{coverage_percent}}% coverage)
- Docs: {{doc_count}} files updated
- Decision Log: {decision_log_path}

**Quality Metrics:**
- ✅ Pattern Compliance: {{compliance_score}}/100
- ✅ Test Coverage: {{coverage_percent}}%
- ✅ AC Coverage: 100%
- ✅ Build Success: ✅
- ✅ Doc Sync: ✅ Zero drift

**Git Commit:**
- SHA: {{commit_sha}}
- Message: feat({{module}}): {{story_title}}

**Next Steps:**
1. Push to remote: git push origin {{branch_name}}
2. Create pull request (if needed)
3. Review in context of epic/sprint
4. Deploy (if ready)

**Artifacts Created:**
- Implementation log: {implementation_log}
- Pre-flight package: {default_output_folder}/preflight-package.md
- Implementation diff: {default_output_folder}/implementation-diff.md
- Decision log: {decision_log_path}

**Time Spent on Checkpoints:** ~11-18 minutes (human review only)

Thank you for using Axon Story Implementation workflow! 🚀
</action>

<critical>Workflow complete - all outputs saved to {default_output_folder}</critical>
</step>

</workflow>