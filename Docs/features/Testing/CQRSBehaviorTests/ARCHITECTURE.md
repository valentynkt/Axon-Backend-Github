---
id: AXON-20250131-testing-cqrs-behavior-tests-ARCHITECTURE
title: CQRS Behavior Tests: Architecture
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

# Context & Scope

## Problem Statement
Current architecture tests validate CQRS structural patterns (naming, interfaces, methods) but lack behavioral validation. We need static analysis to ensure Commands/Queries follow behavioral contracts:
- Commands modify state, Queries are read-only
- Commands have FluentValidation validators in Application layer
- Commands return Result patterns, Queries return DTOs not domain entities

## Architecture Goals
- **Conservative Detection**: Avoid false positives through static analysis
- **Performance**: Maintain ~520ms total test execution (6 new tests ~60ms)
- **Integration**: Extend existing CqrsPatternRules.cs with consistent patterns
- **Maintainability**: Use existing ArchitectureTestHelpers and NetArchTest.Rules

# Boundaries & Dependencies

## Current Architecture Tests Structure
```
tests/ArchitectureTests/
├── CqrsPatternRules.cs (300 lines, 11 tests)
├── ArchitectureTestHelpers.cs (74 lines, utilities)
├── CleanArchitectureRules.cs (dependencies validation)
└── ModuleBoundaryRules.cs (module isolation)
```

## Dependencies (no changes)
- NetArchTest.Rules 3.2.0
- ArchitectureTestHelpers static utilities
- System.Reflection for behavioral analysis
- Existing 81/81 test success rate maintained

# Ports & Contracts

## Static Analysis Interfaces

### IMutationDetector
```csharp
namespace Axon.ArchitectureTests.Behaviors;

/// <summary>
/// Detects potential state mutations through static analysis
/// </summary>
internal interface IMutationDetector
{
    /// <summary>
    /// Analyzes method for potential state mutations
    /// </summary>
    /// <param name="method">Method to analyze</param>
    /// <returns>True if mutations detected</returns>
    bool HasPotentialMutations(MethodInfo method);
}
```

### IReturnTypeAnalyzer
```csharp
namespace Axon.ArchitectureTests.Behaviors;

/// <summary>
/// Analyzes method return types for CQRS compliance
/// </summary>
internal interface IReturnTypeAnalyzer
{
    /// <summary>
    /// Checks if return type is Result or Result&lt;T&gt;
    /// </summary>
    bool IsResultType(Type returnType);
    
    /// <summary>
    /// Checks if return type is a DTO (not domain entity)
    /// </summary>
    bool IsDtoType(Type returnType);
    
    /// <summary>
    /// Checks if return type is a domain entity
    /// </summary>
    bool IsDomainEntity(Type returnType);
}
```

# CQRS Behavioral Mapping

## Test Specifications

### 1. Commands_ShouldModify_State
**Intent**: Commands represent state-changing operations
**Detection Strategy**: Analyze handler dependencies for persistence interfaces
```csharp
// Conservative approach: Check for repository/DbContext dependencies
// Assumes commands with persistence dependencies modify state
var hasPersistenceDependency = handlerType.GetConstructors()
    .SelectMany(c => c.GetParameters())
    .Any(p => p.ParameterType.Name.Contains("Repository") ||
              p.ParameterType.Name.Contains("DbContext") ||
              p.ParameterType.Name.Contains("UnitOfWork"));
```

### 2. Queries_ShouldNever_ModifyState  
**Intent**: Queries are read-only operations
**Detection Strategy**: Check handlers lack persistence dependencies
```csharp
// Inverse of command logic - queries should not have write dependencies
var hasWriteDependency = /* Same check as commands but inverted */
```

### 3. Commands_ShouldHave_FluentValidationValidators
**Intent**: All commands must have input validation
**Detection Strategy**: Find corresponding AbstractValidator&lt;TCommand&gt; classes
```csharp
// For each command, find matching validator
var validatorType = Types.InCurrentDomain()
    .Where(t => t.BaseType?.IsGenericType == true &&
                t.BaseType.GetGenericTypeDefinition() == typeof(AbstractValidator<>) &&
                t.BaseType.GetGenericArguments()[0] == commandType);
```

### 4. Validators_ShouldBeIn_ApplicationLayer
**Intent**: Business validation belongs in Application layer
**Detection Strategy**: Namespace validation for AbstractValidator implementations
```csharp
// Check namespace contains ".Application."
validator.Namespace?.Contains(".Application.") == true
```

### 5. Commands_ShouldReturn_ResultOrUnit  
**Intent**: Commands return success/failure outcomes
**Detection Strategy**: Validate return types are Result or Result&lt;T&gt;
```csharp
// Use existing ArchitectureTestHelpers.ReturnsResultType
// Extended to check Unit type for void operations
```

### 6. Queries_ShouldReturn_DTOsNotEntities
**Intent**: Prevent domain entity leakage through queries
**Detection Strategy**: Analyze return types for domain vs DTO patterns
```csharp
// Conservative heuristics:
// - DTOs: Records in *.Application.* or *.Api.Contracts.*
// - Entities: Classes in *.Domain.* with behavior methods
```

# Data Flow / Sequence

## Test Execution Flow
```
1. NetArchTest.Rules discovers types
2. Filter by CQRS patterns (Commands/Queries namespace)
3. For each type:
   a. Extract metadata (dependencies, return types)
   b. Apply behavioral validation rules
   c. Collect violations
4. Assert violations.ShouldBeEmpty() with detailed messages
```

## Conservative Detection Strategy
- **False Positives Prevention**: Multiple validation criteria
- **Graceful Degradation**: Skip analysis if reflection fails
- **Clear Error Messages**: Include specific violation details

# Transactions, Idempotency, Consistency

## Test Isolation
- Each test method is atomic
- No shared state between tests
- Uses NetArchTest caching for performance

## Performance Constraints
- Target: 6 new tests complete in ~60ms
- Use lazy evaluation where possible
- Cache reflection results within test scope

# Observability, Security, Performance

## Observability
- **Test Results**: Clear violation messages with type names
- **Performance**: Individual test timing via NUnit diagnostics
- **Coverage**: Test report shows behavioral rule coverage

## Security Considerations
- Static analysis only - no runtime code execution
- Reflection limited to public API surface
- No access to sensitive application data

## Performance Optimization
- **Lazy Type Discovery**: Cache Types.InCurrentDomain() results
- **Early Exit**: Skip detailed analysis if basic filters fail
- **Batch Operations**: Group reflection operations

# Compatibility & Migration

## Feature Flags
None required - tests are build-time validation

## Rollout Strategy
1. Add tests to existing CqrsPatternRules.cs
2. Validate against current codebase (no violations expected)
3. Enable in CI pipeline
4. Document behavioral expectations

## Breaking Changes
None - extends existing test suite without modification

# Alternatives Considered

## Runtime Behavior Analysis
**Rejected**: Too complex, performance impact, requires instrumentation

## Full Static Analysis Tools
**Rejected**: Roslyn analyzers overkill for architecture tests, different toolchain

## Attribute-Based Metadata
**Rejected**: Requires code modification, violates clean architecture

# Risks & Mitigations

## Risk: False Positives
**Likelihood**: Medium
**Impact**: High (breaks CI builds)
**Mitigation**: Conservative detection heuristics, comprehensive testing against current codebase

## Risk: Performance Degradation  
**Likelihood**: Low
**Impact**: Medium (slows build)
**Mitigation**: Performance benchmarking, lazy evaluation, caching

## Risk: Maintenance Overhead
**Likelihood**: Low  
**Impact**: Medium (test brittleness)
**Mitigation**: Leverage existing helpers, clear documentation, simple heuristics

## Risk: Inconsistent Results
**Likelihood**: Low
**Impact**: High (CI flakiness)
**Mitigation**: Deterministic analysis, no external dependencies