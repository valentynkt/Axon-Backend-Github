---
id: AXON-20250731-architecturetests-repositorypattern-ARCHITECTURE
title: Repository Pattern: Architecture
module: ArchitectureTests
feature: RepositoryPattern
gate: G1
owner: system-designer&planner
status: draft
relates_to: []
source_of_truth: doc
created: 2025-07-31
updated: 2025-07-31
version: 1
---

# Context & Scope

## Problem Statement
Add 5 Repository Pattern architecture tests to the existing CleanArchitectureRules.cs test suite. The current 76 tests lack critical Repository Pattern enforcement that could prevent DDD violations in data access layers.

## Feature Boundaries
- **In Scope**: Adding 5 new test methods to existing test class
- **Test Scope**: Repository interface/implementation placement, EF Core type exposure, aggregate root usage
- **Integration Point**: Existing ArchitectureTestHelpers utility class and NetArchTest.Rules framework

## Current Architecture Context
- Existing test suite: 76 passing tests in ~366ms
- Test framework: NUnit with NetArchTest.Rules
- Error formatting: ArchitectureTestHelpers.FormatViolations() pattern
- Naming conventions: Layer_ShouldConstraint_Target format

# Boundaries & Dependencies

## Module Dependencies
```
CleanArchitectureRules.cs
├── NetArchTest.Rules (existing)
├── ArchitectureTestHelpers (existing utility)
├── NUnit.Framework (existing)
└── System.Reflection (existing)
```

## Test Execution Context
- **Assembly Scanning**: Types.InCurrentDomain() covers all project assemblies
- **Namespace Patterns**: Uses wildcards (*.Domain.*, *.Infrastructure.*)
- **Performance Budget**: Must stay within existing 366ms execution time
- **Error Reporting**: Consistent violation formatting with actionable guidance

# Ports & Contracts

## Repository Detection Interfaces
```csharp
// Repository Type Detection
private static bool IsRepositoryInterface(Type type)
private static bool IsRepositoryImplementation(Type type)
private static bool IsInDomainLayer(Type type)
private static bool IsInInfrastructureLayer(Type type)

// EF Core Type Detection
private static readonly string[] EntityFrameworkTypes = {
    "Microsoft.EntityFrameworkCore.DbContext",
    "Microsoft.EntityFrameworkCore.DbSet",
    "System.Linq.IQueryable",
    "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker"
};

// Aggregate Root Detection
private static bool IsAggregateRootType(Type type)
private static bool InheritsFromAggregateRoot(Type type)
```

## Test Method Signatures
```csharp
[Test]
public void Repositories_ShouldBeIn_InfrastructureLayer()

[Test] 
public void Repositories_ShouldImplement_IRepository()

[Test]
public void Repositories_ShouldNotExpose_EntityFrameworkTypes()

[Test]
public void RepositoryInterfaces_ShouldBeIn_DomainLayer()

[Test]
public void Repositories_ShouldWork_WithAggregateRootsOnly()
```

# CQRS Mapping

## Test Operations (Read-Only Analysis)
- **Query**: Scan assemblies for repository types
- **Analysis**: Apply architectural rules to discovered types
- **Validation**: Generate violation reports with guidance
- **Reporting**: Format errors using existing helper utilities

## Violation Detection Flow
```
Assembly Types → Repository Filter → Rule Application → Violation Collection → Error Formatting
```

# Data Flow / Sequence

## Happy Path Sequence
1. **Test Discovery**: NUnit discovers 5 new test methods
2. **Type Scanning**: NetArchTest.Rules scans loaded assemblies
3. **Pattern Matching**: Filter types by repository naming patterns
4. **Rule Validation**: Apply specific architectural constraints
5. **Success Report**: Tests pass with no violations

## Failure Path Sequence
1. **Violation Detection**: Repository violates architectural rule
2. **Context Capture**: Record type name, violation details, location
3. **Guidance Generation**: Create actionable remediation suggestions
4. **Error Formatting**: Use ArchitectureTestHelpers.FormatViolations()
5. **Test Failure**: Assert failure with formatted violation report

## Performance Considerations
- **Lazy Evaluation**: Use LINQ deferred execution where possible
- **Type Caching**: Leverage NetArchTest's built-in type caching
- **Reflection Optimization**: Minimize GetMethods()/GetProperties() calls
- **Early Exit**: Fail fast on first violation for better developer feedback

# Transactions, Idempotency, Consistency

## Test Isolation
- **Stateless Design**: All test methods are independent and stateless
- **No Side Effects**: Tests only read assembly metadata, never modify state
- **Deterministic Results**: Same types always produce same validation results
- **Parallel Safe**: Tests can run concurrently without interference

## Consistency Guarantees
- **Assembly Snapshot**: Tests operate on consistent view of loaded assemblies
- **Rule Consistency**: All repository types evaluated against same rule set
- **Error Format Consistency**: All violations use same formatting pattern

# Observability (ILogger, Activity, W3C), Security, Performance

## Performance Characteristics
- **Target**: Stay within existing 366ms total test suite execution time
- **Estimated Impact**: +50-75ms for 5 new tests (repository type scanning overhead)
- **Optimization Strategy**: Reuse type collections between tests where possible

## Error Observability
- **Violation Details**: Include type names, namespaces, and violation context
- **Actionable Guidance**: Suggest specific remediation actions
- **Structured Output**: NUnit test result integration for CI/CD visibility

## Security Considerations
- **Read-Only Operations**: Tests only inspect metadata, no execution of user code
- **Assembly Trust**: Operates on already-loaded, trusted assemblies
- **No External Dependencies**: No network calls or file system access beyond loaded assemblies

# Compatibility & Migration

## Integration Strategy
- **Non-Breaking**: Add new tests to existing CleanArchitectureRules class
- **Backward Compatible**: Existing 76 tests remain unchanged
- **Naming Consistency**: Follow established Layer_ShouldConstraint_Target pattern
- **Helper Reuse**: Leverage existing ArchitectureTestHelpers methods

## Migration Considerations
- **No Database Changes**: Pure code analysis, no persistence layer impact
- **No API Changes**: Internal test infrastructure only
- **CI/CD Integration**: Tests automatically included in existing test pipelines
- **Developer Experience**: Familiar NUnit test output and error formatting

## Feature Flags
- **Not Required**: Tests are always active once merged
- **Conditional Logic**: Could add test category attributes for selective execution if needed

# Alternatives Considered

## Alternative 1: Separate Test Class
**Pros**: Clean separation, easier to maintain
**Cons**: Would need to duplicate layer constants and helper methods
**Decision**: Rejected - adds complexity without benefit

## Alternative 2: Custom Architecture Rule Framework
**Pros**: More flexible, could match existing Core.Rules pattern
**Cons**: Significant overhead, inconsistent with existing CleanArchitectureRules pattern
**Decision**: Rejected - overengineering for 5 simple tests

## Alternative 3: Integration with Existing Core Rules
**Pros**: Leverages sophisticated rule framework in tests/Axon.ArchitectureTests.Core
**Cons**: Adds dependency complexity, may not integrate well with existing CleanArchitectureRules
**Decision**: Rejected - maintain consistency with existing simple test pattern

# Risks & Mitigations

## Risk 1: Performance Impact
**Risk**: New tests exceed 366ms performance budget
**Mitigation**: Share type collections between tests, use efficient LINQ operations
**Monitoring**: Measure execution time in CI/CD pipeline

## Risk 2: False Positives
**Risk**: Generic repository base classes trigger inappropriate violations  
**Mitigation**: Add specific exclusion patterns for known framework types
**Detection**: Include unit tests with mock repository scenarios

## Risk 3: Namespace Pattern Brittleness
**Risk**: Non-standard project structures break namespace detection
**Mitigation**: Use multiple namespace patterns (*.Domain.*, *.Domain)
**Validation**: Test against actual project namespace structure

## Risk 4: Entity Framework Version Changes
**Risk**: EF Core type names change in future versions
**Mitigation**: Use flexible string matching, version-specific type lists
**Evolution**: Monitor EF Core releases for breaking changes

## Risk 5: Aggregate Root Detection Accuracy
**Risk**: False negatives for legitimate aggregate root implementations
**Mitigation**: Multiple detection strategies (inheritance, namespace, naming)
**Feedback**: Provide clear guidance on proper aggregate root patterns

## Risk 6: Integration with Existing Tests
**Risk**: New tests interfere with existing 76 passing tests
**Mitigation**: Thorough testing with current test suite, isolated helper methods
**Verification**: Run full test suite after implementation