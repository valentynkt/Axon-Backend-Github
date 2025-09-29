# Story Refactoring Workflow

**Version**: 1.0
**Author**: Axon Module
**Type**: Refactoring Workflow (extends story-implementation)
**Complexity**: High (Danger Zone)
**Estimated Duration**: 40-80 minutes (AI work) + 16-25 minutes (human checkpoints)

---

## Purpose

Safely refactor existing code in brownfield .NET projects with **extra safety measures**:
- ✅ **Impact Analysis**: Map ALL usages before refactoring
- ✅ **Backward Compatibility**: Old code still works
- ✅ **Rollback Plan**: Clear reversal strategy
- ✅ **Existing Tests MUST Pass**: No broken behavior
- ✅ **5 Checkpoints**: One extra for refactoring plan approval

**Refactoring is DANGEROUS** - this workflow prevents:
- ❌ Breaking existing functionality
- ❌ Unintended side effects
- ❌ Lost backward compatibility
- ❌ Production incidents

---

## When to Use

Use this workflow when:
- ✅ Story type is **Refactor** (not Feature or Bugfix)
- ✅ Improving code quality, structure, or performance
- ✅ Removing technical debt
- ✅ Code behavior stays the same (or backward compatible)
- ✅ You need safety guarantees for brownfield changes

**Do NOT use for:**
- ❌ Adding new features (use `story-implementation`)
- ❌ Fixing bugs (use `story-bugfix`)
- ❌ Breaking changes without migration plan

---

## Key Differences from story-implementation

| Aspect | story-implementation | story-refactoring |
|--------|---------------------|-------------------|
| **Checkpoints** | 4 | 5 (extra: refactoring plan) |
| **Impact Analysis** | Optional | **MANDATORY** |
| **Usage Mapping** | Basic discovery | **ALL usages mapped** |
| **Backward Compatibility** | N/A (new code) | **MANDATORY** |
| **Existing Tests** | N/A | **MUST ALL PASS** |
| **Rollback Plan** | N/A | **MANDATORY** |
| **Breaking Changes** | Allowed | **NOT ALLOWED** (or explicit migration) |
| **Quality Gates** | 5 gates | 6 gates (+ backward compatibility) |

---

## Workflow Phases

### Phase 0: Story Understanding + Refactoring Plan
- Understand what needs refactoring and why
- Map ALL usages of code being refactored (Archaeologist)
- Analyze impact (blast radius, breaking changes)
- Design refactoring plan with backward compatibility
- Create rollback plan
- **Checkpoint 1.5**: Refactoring plan approval

### Phase 1: Pre-Flight Validation
- Run parallel validation (same as story-implementation)
- **Checkpoint 2**: Pre-flight approval

### Phase 2: Implementation
- Generate refactoring with backward compatibility enforced
- Preserve old APIs, add deprecation warnings
- **Checkpoint 3**: Implementation diff approval
- Apply changes

### Phase 3: Validation
- Generate tests for refactored code
- **Run ALL tests** (existing + new) - existing MUST pass
- Validate backward compatibility
- **Checkpoint 4**: Final commit approval
- Create git commit with rollback reference

**Total**: 5 checkpoints (vs 4 for story-implementation)

---

## Prerequisites

Same as story-implementation, plus:

### Refactoring Story Requirements

```markdown
# Story: Refactor Wallet Verification to Service

**Story ID**: AXON-055
**Module**: Identity
**Type**: Refactor  # CRITICAL: Must be "Refactor"
**Priority**: Medium
**Complexity**: High

## User Story

**As a** developer
**I want** wallet verification logic extracted to a dedicated service
**So that** it can be reused and tested independently

## Acceptance Criteria

1. **AC1**: Wallet verification logic moved to WalletVerificationService
2. **AC2**: Old VerifyWallet method still works (backward compatible)
3. **AC3**: All existing tests pass without modification

## Refactoring Scope

**Files to refactor:**
- src/Modules/Identity/Application/Commands/VerifyWalletCommand.cs
- src/Modules/Identity/Application/CommandHandlers/VerifyWalletHandler.cs

**Desired end state:**
- New: WalletVerificationService with extracted logic
- Old: Handler delegates to service
- Backward compatible: Yes (old API preserved)

## Risks

- High blast radius: VerifyWallet used in 15 places
- Breaking change risk if not careful
...
```

---

## Usage

**From Story Orchestrator:**
```
@axon-story-orchestrator
*implement-story
[Provide refactoring story file]
# Orchestrator detects Type: Refactor → routes to story-refactoring
```

**Direct:**
```
workflow: bmad/axon/workflows/story-refactoring/workflow.yaml
story_file: path/to/refactor-story-055.md
```

---

## Outputs

Same as story-implementation, plus:

### Refactoring-Specific Artifacts

1. **Impact Analysis** (`{output_folder}/impact-analysis.md`)
   - Code being refactored
   - ALL usages mapped
   - Blast radius assessment
   - Risk analysis
   - Mitigation strategies

2. **Refactoring Plan** (`{output_folder}/refactoring-plan.md`)
   - Phased approach
   - Backward compatibility strategy
   - Deprecation path
   - Testing strategy

3. **Rollback Plan** (`{output_folder}/rollback-plan.md`)
   - Step-by-step reversal
   - Files to restore
   - Time estimate
   - Data loss risk

4. **Migration Guide** (if breaking changes)
   - How to migrate to new API
   - Deprecation timeline
   - Code examples

### Git Commit Format

```
refactor(identity): Extract wallet verification to service

Refactors wallet verification logic to WalletVerificationService

Impact:
- Files changed: 3 new + 2 modified
- Blast radius: Medium (15 usages)
- Backward compatible: ✅
- All existing tests pass: ✅

Rollback: See Docs/PROCESS/active-stories/AXON-055/rollback-plan.md

Tests: 18 tests (15 existing + 3 new), 93% coverage
Quality Gates: ✅ 6/6 passed (includes backward compatibility)

🤖 Generated with Axon Module (BMAD)
Co-Authored-By: Valik
```

---

## Safety Guarantees

This workflow GUARANTEES:

1. **Impact Known**: ALL usages mapped before refactoring
2. **Backward Compatible**: Old code still works (or explicit migration)
3. **Tests Pass**: ALL existing tests pass (0 failures mandatory)
4. **Rollback Ready**: Clear reversal plan if things go wrong
5. **No Surprises**: 5 checkpoints for human oversight

**Mandatory Safety Gates:**
- ✅ All existing tests pass: 100%
- ✅ Backward compatibility: 100%
- ✅ No breaking changes: true (or acknowledged + migration guide)

---

## Example: Safe Refactoring Flow

### Story: "Extract Authentication Logic to Service"

**Phase 0: Analysis**
1. Map ALL 47 usages of old authentication code
2. Assess blast radius: Large (affects 3 modules)
3. Design: Extract to AuthenticationService, keep old methods as wrappers
4. Rollback: Restore old inline implementation if service fails

**Checkpoint 1.5**: User approves plan

**Phase 1: Pre-Flight**
- Discovery: Found 47 usages, mapped dependencies
- Library: Can use Microsoft.AspNetCore.Authentication.JwtBearer
- Patterns: Result<T>, dependency injection

**Checkpoint 2**: User approves

**Phase 2: Implementation**
- Create AuthenticationService with new logic
- Keep old methods, delegate to service
- Add deprecation warnings
- All old call sites still work

**Checkpoint 3**: User approves diff

**Phase 3: Validation**
- Run ALL tests: 145 tests (142 existing + 3 new)
- **ALL 142 existing tests pass** ✅
- New tests pass ✅
- Backward compatibility validated ✅

**Checkpoint 4**: User approves commit

**Result**: Safe refactoring with zero production risk

---

## Troubleshooting

### Issue: Existing tests failing after refactoring

**Cause**: Refactoring changed behavior unintentionally
**Solution**:
1. Analyze which tests fail and why
2. Fix refactored code to preserve old behavior
3. NEVER change tests to make them pass
4. If behavior change is intentional, explicitly acknowledge as breaking change

### Issue: Blast radius is Large

**Cause**: Code being refactored is heavily used
**Solution**:
1. Consider phased refactoring (split into 2+ stories)
2. Use adapter pattern to preserve compatibility
3. Add comprehensive integration tests
4. Plan careful rollout strategy

### Issue: Breaking changes unavoidable

**Cause**: Refactoring requires API changes
**Solution**:
1. Document all breaking changes
2. Create migration guide with code examples
3. Add deprecation warnings to old API
4. Plan deprecation timeline (e.g., 3 months)
5. Communicate to all affected teams

### Issue: Rollback needed

**Cause**: Refactoring caused production incident
**Solution**:
1. Follow rollback plan from {rollback_plan}
2. Revert git commit
3. Restore files listed in rollback plan
4. Verify rollback with tests
5. Document what went wrong for learning

---

## Configuration

Same as story-implementation, plus:

```yaml
# Refactoring-specific settings
extra_validation: true
backward_compatibility_check: true
impact_analysis_required: true
rollback_plan_required: true
checkpoint_count: 5  # One extra
```

---

## Success Metrics

- **Safety**: 100% backward compatibility
- **Test Coverage**: 90%+ code, 100% existing tests pass
- **Impact Analysis**: 100% usages mapped
- **Rollback Ready**: 100% have rollback plan
- **Production Incidents**: 0 (goal)

---

## Related Workflows

- **story-orchestrator**: Routes to this workflow for Type: Refactor
- **story-implementation**: Similar structure, fewer safety measures
- **story-bugfix**: Different focus (issue resolution)

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-09-30 | Initial release (Phase 3 implementation) |

---

**Status**: ✅ **Production-Ready** (Phase 3 Complete - Safe Refactoring)
**Warning**: ⚠️ **DANGER ZONE** - Always follow safety protocols!