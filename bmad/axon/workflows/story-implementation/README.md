# Story Implementation Workflow

**Version**: 1.0
**Author**: Axon Module
**Type**: Document + Action Workflow
**Complexity**: High
**Estimated Duration**: 30-60 minutes (AI work) + 11-18 minutes (human checkpoints)

---

## Purpose

Implement new features for the Axon Backend (.NET + Clean Architecture + DDD + CQRS) using a **doc-grounded, discovery-first, library-aware** approach with 4 strategic checkpoints for human oversight.

This workflow ensures:
- ✅ **No AI hallucination** (doc-grounded, discovery-first)
- ✅ **Library-first implementation** (use tools, not manual code)
- ✅ **Pattern compliance** (Result<T>, StrongId<T>, CQRS mandatory)
- ✅ **Zero documentation drift** (continuous sync)
- ✅ **High test coverage** (90%+ code, 100% AC)
- ✅ **Efficient collaboration** (4 batched checkpoints, not 21+)

---

## When to Use

Use this workflow when:
- ✅ Implementing a **new feature** (greenfield in brownfield)
- ✅ Story type is **Feature** (not Refactor or Bugfix)
- ✅ You have a clear story with acceptance criteria
- ✅ Story may reference a BMM tech-spec (optional)
- ✅ You want AI to handle implementation with strategic oversight

**Do NOT use for:**
- ❌ Refactoring existing code (use `story-refactoring` workflow)
- ❌ Fixing bugs (use `story-bugfix` workflow)
- ❌ Exploratory work without clear requirements

---

## Workflow Structure

### 4 Phases

| Phase | Goal | Duration | Agents |
|-------|------|----------|--------|
| **Phase 0: Story Understanding** | Doc-grounded comprehension | 5-10 min | Story Orchestrator, Doc Oracle |
| **Phase 1: Pre-Flight Validation** | Discovery + Library + Pattern | 10-15 min | Archaeologist, Library Sage, Doc Oracle (parallel) |
| **Phase 2: Implementation** | Code generation | 15-30 min | Implementation Surgeon |
| **Phase 3: Validation** | Tests + Doc Sync + Learning | 10-15 min | Quality Guardian |

**Total AI Work**: 40-70 minutes
**Total Human Review**: 11-18 minutes (at 4 checkpoints)

---

### 4 Checkpoints

| Checkpoint | Phase | Purpose | Time | Approval Required |
|------------|-------|---------|------|-------------------|
| **Checkpoint 1** | After Phase 0 | Story Understanding | 1 min | Yes |
| **Checkpoint 2** | After Phase 1 | Pre-Flight Plan | 3-5 min | Yes |
| **Checkpoint 3** | After Phase 2 | Implementation Diff | 5-10 min | Yes |
| **Checkpoint 4** | After Phase 3 | Final Commit | 2 min | Yes |

**Batched Approvals**: Human reviews at strategic points, AI works autonomously between checkpoints.

---

## Prerequisites

### Required Inputs

1. **Story File** (`story-NNN.md`)
   - Story ID, title, module context
   - User story statement (As a... I want... So that...)
   - Acceptance criteria (testable, Given-When-Then format)
   - Technical context (patterns, dependencies)
   - Test strategy

2. **Optional: BMM Tech Spec** (if from BMM planning phase)
   - Path to tech-spec.md
   - Referenced in story file

### Required Documentation

The workflow loads documentation progressively (hub-and-spoke):

**Core Hub** (Always loaded):
- `Docs/ENGINEERING/00-START-HERE.md`
- `Docs/ENGINEERING/guides/patterns/00-QUICK-REFERENCE.md`
- `Docs/ENGINEERING/guides/architecture/system-overview.md`
- `Docs/Libraries/00-INDEX.md`

**Module Spokes** (Loaded on context):
- Identity: 5 module docs (00-INDEX, 01-domain-model, 03-authentication, 05-api-contracts, 06-database-schema)
- Chat: 5 module docs (same pattern)
- API: API docs + library guides (FastEndpoints, FluentValidation)

---

## Usage

### Invocation

**From Axon Story Orchestrator:**
```
@axon-story-orchestrator
*implement-story
[Provide story file path]
```

**Direct Workflow Execution:**
```
[Invoke workflow]
workflow: bmad/axon/workflows/story-implementation/workflow.yaml
story_file: path/to/story-001.md
tech_spec: path/to/tech-spec.md (optional)
```

### Example Story File

```markdown
# Story: Add Wallet Auto-Revocation

**Story ID**: AXON-042
**Module**: Identity
**Type**: Feature
**Priority**: High
**Complexity**: Medium

## User Story

**As a** security admin
**I want** old wallet credentials to be auto-revoked after 90 days
**So that** we reduce the attack surface from stale credentials

## Acceptance Criteria

1. **AC1**: When a wallet credential is 90+ days old, it is automatically revoked
   - Test: Given a credential from 91 days ago, When the revocation job runs, Then the credential is revoked

2. **AC2**: Revoked credentials cannot be used for authentication
   - Test: Given a revoked credential, When auth is attempted, Then authentication fails with error

3. **AC3**: Users are notified 7 days before revocation
   - Test: Given a credential at 83 days, When notification job runs, Then user receives email warning

...
```

---

## Agents Used

This workflow coordinates 6 specialized agents:

| Agent | Role | Phase |
|-------|------|-------|
| **Story Orchestrator** | Master coordinator | Phase 0 |
| **Doc Oracle** | Documentation intelligence | Phase 0-1, 3 |
| **Archaeologist** | Codebase discovery | Phase 1 (parallel) |
| **Library Sage** | Library-first enforcement | Phase 1 (parallel) |
| **Implementation Surgeon** | Surgical code generation | Phase 2 |
| **Quality Guardian** | Testing & validation | Phase 3 |

---

## Outputs

### Code Artifacts
- Source files (src/Modules/{{module}}/)
  - Domain: Aggregates, ValueObjects, Errors, DomainEvents
  - Application: Commands, Queries, Handlers
  - Infrastructure: Repositories, Services, Configurations
  - API: Endpoints (FastEndpoints pattern)
- Test files (tests/Modules/{{module}}/)
  - Domain unit tests
  - Application integration tests
  - API E2E tests
  - AC coverage tests

### Documentation Artifacts
- Implementation log: `{output_folder}/active-stories/{{story_id}}/implementation-log.md`
- Pre-flight package: `{output_folder}/active-stories/{{story_id}}/preflight-package.md`
- Implementation diff: `{output_folder}/active-stories/{{story_id}}/implementation-diff.md`
- Decision log: `{output_folder}/active-stories/{{story_id}}/decisions.yaml`
- Updated docs: Module docs, pattern docs, ADRs (as needed)

### Git Artifacts (if committed)
- Git commit with message format:
  ```
  feat({{module}}): {{story_title}}

  Implements story {{story_id}}: {{user_story_summary}}

  Changes:
  - {{change_1}}
  - {{change_2}}

  Tests: {{test_count}} tests, {{coverage_percent}}% coverage
  Docs: {{doc_count}} files updated
  Quality Gates: ✅ 5/5 passed

  🤖 Generated with Axon Module (BMAD)
  Co-Authored-By: {{user_name}}
  ```

---

## Quality Gates

All 5 quality gates MUST pass before commit:

| Gate | Target | Mandatory |
|------|--------|-----------|
| **Pattern Compliance** | ≥ 95% | Yes |
| **Test Coverage** | ≥ 90% | Yes |
| **AC Coverage** | = 100% | Yes |
| **Build Success** | = 100% | Yes |
| **Doc Sync** | Zero drift | Yes |

---

## Success Metrics

Expected outcomes after using this workflow:

- **Development Speed**: 2x faster feature delivery
- **Code Quality**: 95%+ pattern compliance
- **Test Coverage**: 90%+ code coverage, 100% AC coverage
- **Documentation**: Zero drift (docs always match code)
- **AI Accuracy**: 80% reduction in hallucination errors
- **Developer Experience**: 11-18 min human review (vs 60+ min micromanagement)

---

## Configuration

### Workflow Variables

Configured in `bmad/axon/config.yaml`:

```yaml
# Checkpoint settings
checkpoint_count: 4
yolo_mode_available: true

# Quality targets
pattern_validation_strict: true
library_first_enforcement: true

# Doc loading
progressive_doc_loading: true

# Paths
output_folder: "{project-root}/Docs/PROCESS/active-stories"
decision_log_folder: "{output_folder}/decisions"
```

### Optional: #yolo Mode

Skip all checkpoints for trusted, low-risk stories:

```
[Invoke workflow with #yolo flag]
mode: #yolo
```

**Warning**: Use #yolo mode only for:
- Simple stories (low complexity)
- Non-critical changes
- High trust in AI accuracy
- Time-sensitive situations

---

## Troubleshooting

### Issue: Checkpoint not showing

**Cause**: User response not recognized
**Solution**: Use exact response codes: [c] Continue, [e] Edit, [b] Back, [a] Abort

### Issue: Build fails after implementation

**Cause**: Pattern violation or syntax error
**Solution**: Workflow auto-detects and fixes. If persists, check Implementation Surgeon logs.

### Issue: Test coverage below 90%

**Cause**: Complex code paths not tested
**Solution**: Quality Guardian generates additional tests. Review AC coverage first.

### Issue: Documentation drift detected

**Cause**: New APIs or patterns introduced
**Solution**: Phase 3 auto-syncs docs. Review drift report for accuracy.

### Issue: Pre-flight validation takes too long

**Cause**: Large codebase or many libraries
**Solution**: Normal for first run. Subsequent runs use cached results.

---

## Related Workflows

- **story-orchestrator**: Master routing workflow (calls this workflow)
- **story-refactoring**: For refactoring existing code (not greenfield)
- **story-bugfix**: For issue resolution with root cause analysis
- **identity-workflow**: Identity module specialization (extends this workflow)
- **chat-workflow**: Chat module specialization (extends this workflow)
- **api-workflow**: API/FastEndpoints specialization (extends this workflow)

---

## Support

For issues or questions:
- Review design document: `Docs/PROCESS/bmad/axon-module-design-complete.md`
- Check agent documentation: `bmad/axon/agents/README.md`
- Review workflow catalog: `bmad/axon/workflows/README.md`

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-09-30 | Initial release (Phase 3 implementation) |

---

**Status**: ✅ **Production-Ready** (Phase 3 Complete)
**Next**: Test with real story from Identity or Chat module