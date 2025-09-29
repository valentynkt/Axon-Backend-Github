# Story Implementation Workflow - Validation Checklist

## Phase 0: Story Understanding

### Story Input Validation
- [ ] Story file exists and is readable
- [ ] Story ID is present and follows format (e.g., STORY-001)
- [ ] Story title is clear and descriptive
- [ ] Module context identified (Identity/Chat/API/Cross-cutting)
- [ ] User story statement complete (As a... I want... So that...)
- [ ] At least 3 acceptance criteria defined
- [ ] All acceptance criteria are testable (Given-When-Then format possible)

### Documentation Loading
- [ ] Core hub documentation loaded (00-START-HERE, QUICK-REFERENCE, system-overview, Libraries/00-INDEX)
- [ ] Module-specific docs loaded if applicable (5 docs per module)
- [ ] Story template loaded for reference
- [ ] Tech spec loaded if BMM handoff exists

### Story Understanding Summary
- [ ] Summary is 1-2 paragraphs (not too long)
- [ ] Key requirements listed (3-5 items maximum)
- [ ] Technical approach identified (patterns, dependencies)
- [ ] Implementation layers outlined (Domain/Application/Infrastructure/API)
- [ ] Complexity estimated (Simple/Medium/Complex)
- [ ] Summary saved to implementation log

## Phase 1: Pre-Flight Validation

### Codebase Discovery (Archaeologist)
- [ ] Searched for existing implementations in target module
- [ ] Mapped available APIs across all layers
- [ ] Found similar patterns in codebase
- [ ] Generated reuse recommendations (Reuse Directly/Extend/Adapt/Create New)
- [ ] All findings include file:line citations
- [ ] Reuse report generated

### Library Capability Check (Library Sage)
- [ ] Checked if requirements can be solved by existing libraries
- [ ] Evaluated 11+ core libraries for applicability
- [ ] Generated 4-factor scoring (Availability 40%, Effort 30%, Maintainability 20%, Performance 10%)
- [ ] Recommended approach: Library-First vs Manual vs Hybrid
- [ ] Showed usage patterns from codebase examples
- [ ] Checked version compatibility
- [ ] Library recommendations report generated

### Pattern Compliance (Doc Oracle)
- [ ] Validated against all 6 ADRs (Modular Monolith, CQRS, Result Pattern, Strong IDs, PostgreSQL, FastEndpoints)
- [ ] Verified pattern requirements (Result<T>, StrongId<T>, CQRS separation)
- [ ] Detected potential drift points
- [ ] Calculated weighted compliance score (6 dimensions)
- [ ] No pattern violations OR violations documented with mitigation
- [ ] Pattern compliance report generated

### Pre-Flight Package
- [ ] All 3 validation tracks completed
- [ ] Discovery, library, and pattern results synthesized
- [ ] Implementation plan summary clear and actionable
- [ ] Risks identified and documented
- [ ] Pre-flight package saved to output folder

## Phase 2: Implementation

### Implementation Plan & Diff Preview
- [ ] Implementation plan follows bottom-up layering (Domain → Application → Infrastructure → API)
- [ ] All files to create listed with full content preview
- [ ] All files to modify listed with unified diff preview
- [ ] Pattern compliance verified in preview (Result<T>, StrongId<T>, CQRS)
- [ ] Library integrations shown in preview
- [ ] Module-specific patterns applied (Identity: owned entities, Chat: business rules, API: FastEndpoints)
- [ ] Estimated line count reasonable
- [ ] Implementation diff saved

### Code Generation (Implementation Surgeon)
- [ ] All new directories created
- [ ] All new files created with correct content
- [ ] All modified files updated using surgical edits
- [ ] XML documentation added to all public members
- [ ] Consistent formatting applied
- [ ] Syntax validated (C# compilation check)
- [ ] Pattern compliance verified in actual code
- [ ] Library integration correctness verified

### Documentation Drift Detection
- [ ] New APIs checked against API documentation
- [ ] New patterns checked against pattern docs
- [ ] Architectural changes checked against ADRs
- [ ] New domain concepts checked against domain-model docs
- [ ] Drift report generated with affected docs list
- [ ] Recommended doc updates documented

## Phase 3: Validation

### Test Generation (Quality Guardian)
- [ ] Domain unit tests generated (aggregates, value objects, domain events, error paths)
- [ ] Application integration tests generated (command handlers, query handlers)
- [ ] API E2E tests generated (endpoints, request/response, validation, auth)
- [ ] AC coverage tests generated (one test per AC, Given-When-Then naming)
- [ ] All tests use AAA pattern (Arrange-Act-Assert)
- [ ] All tests use Shouldly assertions
- [ ] Test files saved to appropriate directories (tests/Modules/{{module}}/)
- [ ] Minimum 90% code coverage targeted

### Build & Test Execution
- [ ] dotnet build executed with --configuration Release
- [ ] Build succeeded with warnings-as-errors enabled
- [ ] All projects compiled successfully
- [ ] dotnet test executed with code coverage
- [ ] All tests passed (0 failures, 0 skipped OR justified skips)
- [ ] Code coverage calculated and meets 90%+ target
- [ ] Build/test report generated

### Acceptance Criteria Validation
- [ ] Test exists for each AC from story
- [ ] All AC tests passed
- [ ] All AC tests fully cover their respective AC
- [ ] 100% AC coverage achieved (MANDATORY)
- [ ] AC validation report generated

### Final Compliance Check
- [ ] Pattern compliance ≥ 95% (Result<T>, StrongId<T>, CQRS)
- [ ] Test coverage ≥ 90%
- [ ] AC coverage = 100%
- [ ] Build success = 100%
- [ ] Doc drift addressed
- [ ] All 5 quality gates passed
- [ ] Final compliance report generated

### Documentation Synchronization
- [ ] Module docs updated with new APIs
- [ ] Pattern docs updated if new patterns introduced
- [ ] ADRs updated if architectural decisions made
- [ ] Domain-model docs updated with new concepts
- [ ] All doc updates show before/after diffs
- [ ] Accuracy verified, consistent terminology maintained
- [ ] Doc sync summary generated
- [ ] Zero documentation drift after sync

### Decision & Learning Capture
- [ ] Decision log created in YAML format
- [ ] All major decisions documented with rationale
- [ ] Alternatives considered listed
- [ ] Tradeoffs explained
- [ ] Learnings captured (what went well, what to improve)
- [ ] Patterns applied listed
- [ ] Libraries used listed
- [ ] Technical debt items documented if any
- [ ] Metrics captured (time, coverage, compliance)
- [ ] Decision log saved to {decision_log_path}

## Checkpoint Validation

### Checkpoint 1: Story Understanding (1 min)
- [ ] Story understanding summary displayed to user
- [ ] User explicitly approved understanding OR requested clarification
- [ ] If clarification requested, updated summary re-approved
- [ ] Workflow did not proceed without approval

### Checkpoint 2: Pre-Flight Approval (3-5 min)
- [ ] Pre-flight validation package displayed to user
- [ ] Key findings summarized (discovery, library, patterns)
- [ ] User explicitly approved plan OR requested adjustment
- [ ] If adjustment requested, updated plan re-approved
- [ ] Workflow did not proceed to code generation without approval

### Checkpoint 3: Implementation Preview (5-10 min)
- [ ] Implementation diff preview displayed to user
- [ ] Files to create/modify counts shown
- [ ] Pattern compliance confirmed
- [ ] User explicitly approved changes OR requested edit
- [ ] If edit requested, updated diff re-approved
- [ ] Workflow did not apply changes without approval

### Checkpoint 4: Final Commit Approval (2 min)
- [ ] Complete implementation summary displayed
- [ ] All quality gates shown as passed
- [ ] User explicitly approved commit OR chose defer/review
- [ ] Commit created only if user chose 'c' (commit)
- [ ] Commit message follows format (feat(module): title)

### Checkpoint Timing
- [ ] Total human review time was 11-18 minutes (not counting AI work time)
- [ ] Checkpoints were batched, not scattered
- [ ] User was not micromanaged between checkpoints

## Final Deliverables

### Code Artifacts
- [ ] All source files created/modified in correct locations
- [ ] All test files created in correct locations
- [ ] Code compiles successfully
- [ ] All tests pass

### Documentation Artifacts
- [ ] Implementation log created at {implementation_log}
- [ ] Pre-flight package saved at {default_output_folder}/preflight-package.md
- [ ] Implementation diff saved at {default_output_folder}/implementation-diff.md
- [ ] Decision log saved at {decision_log_path}
- [ ] Module/pattern/ADR docs updated as needed

### Git Artifacts (if committed)
- [ ] Git commit created with proper message format
- [ ] Commit includes all code + tests + docs
- [ ] Commit SHA recorded
- [ ] Commit verified with git log -1

### Quality Metrics
- [ ] Pattern compliance score ≥ 95%
- [ ] Test coverage ≥ 90%
- [ ] AC coverage = 100%
- [ ] Build success rate = 100%
- [ ] Documentation drift = 0

## Workflow Completion

- [ ] Workflow completion report generated
- [ ] All deliverables listed with paths
- [ ] Quality metrics summarized
- [ ] Git commit details shown (if committed)
- [ ] Next steps provided (push, PR, deploy)
- [ ] All artifacts saved to {default_output_folder}

## Issues Found

List any issues discovered during validation:

### Critical Issues (Must fix before commit)
-

### Minor Issues (Can address later)
-

### Technical Debt Created
-