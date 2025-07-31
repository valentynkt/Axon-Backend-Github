---
id: AXON-20250131-testing-cqrs-behavior-tests-TASK_PLAN
title: CQRS Behavior Tests: Task Plan
module: Testing
feature: CqrsBehaviorTests
gate: G1
owner: system-designer&planner
status: draft
relates_to: []
source_of_truth: doc
created: 2025-01-31
updated: 2025-01-31
version: 1
---

# Plan Summary

## Objective
Add 6 behavioral validation tests to existing CqrsPatternRules.cs to ensure CQRS Commands and Queries follow architectural behavioral contracts through static analysis.

## Success Criteria
- All 6 new tests integrate with existing CqrsPatternRules.cs
- Total test execution time remains ≤580ms (current ~520ms + ~60ms)
- 87/87 tests pass (current 81 + 6 new)
- Zero false positives against current codebase
- Conservative detection prevents future CI breaks

## Tasks (Domain → Application → Infrastructure → Api)

### T1: Extend ArchitectureTestHelpers with Behavioral Analysis
**Why**: Shared utilities for behavioral validation across all 6 tests
**Steps**:
1. Add `IsDomainEntity(Type type)` method
   - Check type namespace contains ".Domain."
   - Verify class (not record) with behavior methods
2. Add `IsDtoType(Type type)` method  
   - Check namespace contains ".Application." or ".Api.Contracts."
   - Verify record type or simple data classes
3. Add `HasPersistenceDependencies(Type handlerType)` method
   - Analyze constructor parameters for repository/DbContext types
   - Include interfaces ending with "Repository", "Store", "Context"
4. Add `FindValidatorForCommand(Type commandType)` method
   - Locate AbstractValidator&lt;TCommand&gt; in same assembly
   - Return validator type or null if not found

**Files to touch**:
- `tests/ArchitectureTests/ArchitectureTestHelpers.cs` (modify)

### T2: Implement Commands State Modification Validation  
**Why**: Ensure commands represent state-changing operations
**Steps**:
1. Add `Commands_ShouldModify_State()` test method
   - Find all command handler types in *.Commands.* namespaces
   - Use `HasPersistenceDependencies()` to detect state modification
   - Collect violations for handlers without persistence dependencies
   - Assert violations list is empty with detailed error message
2. Add conservative detection with fallback
   - If no persistence detected, check for async operations (might indicate external calls)
   - Skip validation for handlers with generic interfaces (avoid false positives)

**Files to touch**:
- `tests/ArchitectureTests/CqrsPatternRules.cs` (modify - add test method)

### T3: Implement Queries Read-Only Validation
**Why**: Ensure queries never modify state (read-only operations)
**Steps**:
1. Add `Queries_ShouldNever_ModifyState()` test method
   - Find all query handler types in *.Queries.* namespaces  
   - Use inverse of persistence dependency check
   - Collect violations for handlers with write dependencies
   - Assert violations list is empty
2. Add specific write operation detection
   - Check for IUnitOfWork, SaveChangesAsync patterns
   - Look for command-sending dependencies (IMediator with commands)

**Files to touch**:
- `tests/ArchitectureTests/CqrsPatternRules.cs` (modify - add test method)

### T4: Implement FluentValidation Validator Detection
**Why**: Ensure all commands have input validation
**Steps**:
1. Add `Commands_ShouldHave_FluentValidationValidators()` test method
   - Find all command types ending with "Command"
   - For each command, use `FindValidatorForCommand()` 
   - Collect violations for commands without validators
   - Assert violations list is empty
2. Handle edge cases
   - Skip abstract commands or base classes
   - Skip internal framework commands (if any)

**Files to touch**:
- `tests/ArchitectureTests/CqrsPatternRules.cs` (modify - add test method)

### T5: Implement Validator Layer Placement Validation
**Why**: Ensure validators are placed in Application layer (not Api or Domain)
**Steps**:
1. Add `Validators_ShouldBeIn_ApplicationLayer()` test method
   - Find all AbstractValidator&lt;T&gt; implementations
   - Check namespace contains ".Application."
   - Collect violations for validators in wrong layers
   - Assert violations list is empty
2. Add precision checks
   - Exclude validators for non-command types (DTOs, etc.)
   - Focus on command/query validators only

**Files to touch**:
- `tests/ArchitectureTests/CqrsPatternRules.cs` (modify - add test method)

### T6: Implement Command Return Type Validation
**Why**: Ensure commands return Result or Result&lt;T&gt; patterns
**Steps**:
1. Add `Commands_ShouldReturn_ResultOrUnit()` test method
   - Find all command handler Handle methods
   - Check return type is Task&lt;Result&gt; or Task&lt;Result&lt;T&gt;&gt;
   - Use existing `ArchitectureTestHelpers.ReturnsResultType()` with enhancement
   - Collect violations for handlers returning wrong types
   - Assert violations list is empty
2. Handle Unit type for void operations
   - Extend helper to recognize Unit type as valid
   - Check for Task&lt;Unit&gt; return types

**Files to touch**:
- `tests/ArchitectureTests/ArchitectureTestHelpers.cs` (modify - extend ReturnsResultType)
- `tests/ArchitectureTests/CqrsPatternRules.cs` (modify - add test method)

### T7: Implement Query DTO Return Type Validation  
**Why**: Prevent domain entity leakage through query responses
**Steps**:
1. Add `Queries_ShouldReturn_DTOsNotEntities()` test method
   - Find all query handler Handle methods
   - Extract return type from Task&lt;Result&lt;T&gt;&gt; generic argument
   - Use `IsDomainEntity()` to detect entity leakage
   - Use `IsDtoType()` to verify DTO usage
   - Collect violations for queries returning entities
   - Assert violations list is empty
2. Add conservative heuristics
   - Allow primitive types (string, int, bool)
   - Allow collections of DTOs
   - Flag complex domain objects with behavior

**Files to touch**:
- `tests/ArchitectureTests/CqrsPatternRules.cs` (modify - add test method)

### T8: Performance Optimization and Error Message Enhancement
**Why**: Maintain performance targets and provide clear violation messages
**Steps**:
1. Add caching for type discovery
   - Cache Types.InCurrentDomain() results per test run
   - Reuse reflection metadata across tests
2. Enhance error messages
   - Include full type names in violations
   - Add guidance for fixing violations
   - Use `ArchitectureTestHelpers.FormatViolations()` consistently
3. Add performance benchmarking
   - Measure individual test execution time
   - Ensure 6 tests complete in &lt;60ms total

**Files to touch**:
- `tests/ArchitectureTests/CqrsPatternRules.cs` (modify - optimize existing and new tests)
- `tests/ArchitectureTests/ArchitectureTestHelpers.cs` (modify - add caching utilities)

## Milestones & Criteria

### M1: Helper Methods Implemented (T1)
**Criterion**: ArchitectureTestHelpers compiles with 4 new behavioral analysis methods
**Validation**: Unit tests for helper methods pass with current codebase

### M2: State Validation Tests (T2-T3)  
**Criterion**: Commands/Queries state validation tests implemented and passing
**Validation**: Tests correctly identify current ProcessMessageHandler as command (has dependencies)

### M3: Validation Tests (T4-T5)
**Criterion**: FluentValidation detection and placement tests implemented  
**Validation**: Tests correctly identify ProcessMessageValidator in Application layer

### M4: Return Type Tests (T6-T7)
**Criterion**: Return type validation tests implemented
**Validation**: Tests correctly validate Result&lt;T&gt; returns and DTO usage

### M5: Performance & Polish (T8)
**Criterion**: All 6 tests running in &lt;60ms with clear error messages
**Validation**: Total test suite remains ≤580ms execution time

## Rollback Plan

### Immediate Rollback (if tests fail)
1. Comment out new test methods in CqrsPatternRules.cs
2. Restore original ArchitectureTestHelpers.cs from git
3. Verify 81/81 original tests still pass
4. Re-enable CI builds

### Partial Rollback (if specific tests problematic)
1. Identify failing test method
2. Comment out specific [Test] method
3. Remove related helper methods if unused
4. Document issue in TASK_PLAN.md for future resolution

### Configuration Rollback
- No configuration changes required - pure code addition
- No feature flags to toggle
- No database migrations to revert

## Effort Estimate

**Size: M (Medium)**

**Justification**:
- **Lines of Code**: ~200-250 lines total
  - ArchitectureTestHelpers: +50 lines (4 new methods)
  - CqrsPatternRules: +150-200 lines (6 test methods)
- **Complexity**: Medium static analysis with reflection
- **Risk**: Low-Medium (false positive potential)
- **Testing**: Comprehensive validation against current codebase
- **Dependencies**: No external dependencies, extends existing patterns

**Time Breakdown**:
- T1 (Helpers): 2-3 hours
- T2-T7 (6 Tests): 6-8 hours (1-1.3 hours each)
- T8 (Polish): 1-2 hours
- **Total**: 9-13 hours

**Skills Required**:
- C# Reflection API expertise
- NetArchTest.Rules framework knowledge
- CQRS pattern understanding
- Architecture testing best practices