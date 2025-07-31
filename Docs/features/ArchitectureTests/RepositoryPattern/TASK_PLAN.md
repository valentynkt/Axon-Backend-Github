---
id: AXON-20250731-architecturetests-repositorypattern-TASK_PLAN
title: Repository Pattern: Task Plan
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

# Plan Summary

Implement 5 Repository Pattern architecture tests in the existing CleanArchitectureRules.cs file. The approach adds new test methods while preserving all existing 76 passing tests and maintaining the established patterns, naming conventions, and performance characteristics.

## Tasks (Domain → Application → Infrastructure → Api)

### T1: Add Repository Type Detection Helper Methods
**Why**: Foundation methods needed by all 5 tests for consistent type identification and namespace validation

**Steps**:
1. Add private helper methods to CleanArchitectureRules class
2. Implement repository type detection logic  
3. Add namespace validation methods for Domain/Infrastructure layers
4. Add Entity Framework type detection utilities

**Files to touch**:
- `tests/ArchitectureTests/CleanArchitectureRules.cs` (add helper methods section)

**Implementation Details**:
```csharp
private static bool IsRepositoryInterface(Type type) =>
    type.IsInterface && type.Name.Contains("Repository", StringComparison.OrdinalIgnoreCase);

private static bool IsRepositoryImplementation(Type type) =>
    type.IsClass && !type.IsAbstract && 
    type.Name.Contains("Repository", StringComparison.OrdinalIgnoreCase);

private static bool IsInDomainLayer(Type type) =>
    type.Namespace?.Contains(".Domain.", StringComparison.OrdinalIgnoreCase) == true ||
    type.Namespace?.EndsWith(".Domain", StringComparison.OrdinalIgnoreCase) == true;

private static bool IsInInfrastructureLayer(Type type) =>
    type.Namespace?.Contains(".Infrastructure.", StringComparison.OrdinalIgnoreCase) == true ||
    type.Namespace?.EndsWith(".Infrastructure", StringComparison.OrdinalIgnoreCase) == true;

private static readonly string[] EntityFrameworkTypes = {
    "Microsoft.EntityFrameworkCore.DbContext",
    "Microsoft.EntityFrameworkCore.DbSet",
    "System.Linq.IQueryable",
    "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker"
};

private static bool ExposesEntityFrameworkTypes(Type type) =>
    type.GetMethods().Any(m => EntityFrameworkTypes.Any(ef => 
        m.ReturnType.FullName?.Contains(ef) == true ||
        m.GetParameters().Any(p => p.ParameterType.FullName?.Contains(ef) == true))) ||
    type.GetProperties().Any(p => EntityFrameworkTypes.Any(ef => 
        p.PropertyType.FullName?.Contains(ef) == true));

private static bool IsAggregateRootType(Type type) =>
    InheritsFromAggregateRoot(type) ||
    type.Namespace?.Contains(".Domain.Aggregates") == true ||
    type.Namespace?.Contains(".Domain.") == true;

private static bool InheritsFromAggregateRoot(Type type)
{
    var baseType = type.BaseType;
    while (baseType != null)
    {
        if (baseType.Name.Contains("AggregateRoot", StringComparison.OrdinalIgnoreCase))
            return true;
        baseType = baseType.BaseType;
    }
    return false;
}
```

---

### T2: Implement Test AC1 - Repositories_ShouldBeIn_InfrastructureLayer
**Why**: Enforce that repository implementations reside in Infrastructure layer only

**Steps**:
1. Add test method following established naming pattern
2. Use NetArchTest.Rules to find repository implementations  
3. Validate namespace location against Infrastructure pattern
4. Generate violations with actionable guidance using ArchitectureTestHelpers

**Files to touch**:
- `tests/ArchitectureTests/CleanArchitectureRules.cs` (add test method)

**Implementation Details**:
```csharp
[Test]
public void Repositories_ShouldBeIn_InfrastructureLayer()
{
    var repositoryTypes = Types.InCurrentDomain()
        .That()
        .AreClasses()
        .And()
        .AreNotAbstract()
        .GetTypes()
        .Where(IsRepositoryImplementation)
        .ToList();

    var violations = repositoryTypes
        .Where(type => !IsInInfrastructureLayer(type))
        .Select(type => type.FullName)
        .ToList();

    violations.ShouldBeEmpty(
        ArchitectureTestHelpers.FormatViolations(violations,
            "Repository implementations should be placed in Infrastructure layer"));
}
```

---

### T3: Implement Test AC2 - Repositories_ShouldImplement_IRepository
**Why**: Ensure repository implementations follow interface-based dependency inversion

**Steps**:
1. Add test method to validate interface implementation
2. Check for corresponding interface with "I" prefix
3. Validate that interface exists in Domain layer
4. Report missing interfaces with creation guidance

**Files to touch**:
- `tests/ArchitectureTests/CleanArchitectureRules.cs` (add test method)

**Implementation Details**:  
```csharp
[Test]
public void Repositories_ShouldImplement_IRepository()
{
    var repositoryTypes = Types.InCurrentDomain()
        .That()
        .AreClasses()
        .And()
        .AreNotAbstract()
        .GetTypes()
        .Where(IsRepositoryImplementation)
        .Where(IsInInfrastructureLayer)
        .ToList();

    var violations = new List<string>();

    foreach (var repoType in repositoryTypes)
    {
        var expectedInterfaceName = $"I{repoType.Name}";
        var hasMatchingInterface = repoType.GetInterfaces()
            .Any(i => i.Name.Equals(expectedInterfaceName, StringComparison.OrdinalIgnoreCase) &&
                     IsInDomainLayer(i));

        if (!hasMatchingInterface)
        {
            violations.Add($"{repoType.FullName} should implement {expectedInterfaceName} in Domain layer");
        }
    }

    violations.ShouldBeEmpty(
        ArchitectureTestHelpers.FormatViolations(violations,
            "Repository implementations should implement corresponding interfaces in Domain layer"));
}
```

---

### T4: Implement Test AC3 - Repositories_ShouldNotExpose_EntityFrameworkTypes  
**Why**: Prevent EF Core types from leaking into Domain/Application layers through repository contracts

**Steps**:
1. Add test method to scan repository interfaces and implementations
2. Check public methods, properties, and return types for EF Core types
3. Identify specific EF types: DbContext, DbSet<T>, IQueryable<T>, ChangeTracker
4. Report violations with domain-appropriate alternatives

**Files to touch**:
- `tests/ArchitectureTests/CleanArchitectureRules.cs` (add test method)

**Implementation Details**:
```csharp
[Test]
public void Repositories_ShouldNotExpose_EntityFrameworkTypes()
{
    var repositoryTypes = Types.InCurrentDomain()
        .That()
        .AreClasses()
        .Or()
        .AreInterfaces()
        .GetTypes()
        .Where(type => IsRepositoryInterface(type) || IsRepositoryImplementation(type))
        .ToList();

    var violations = repositoryTypes
        .Where(ExposesEntityFrameworkTypes)
        .Select(type => type.FullName)
        .ToList();

    violations.ShouldBeEmpty(
        ArchitectureTestHelpers.FormatViolations(violations,
            "Repositories should not expose Entity Framework types. Use domain types and collections instead"));
}
```

---

### T5: Implement Test AC4 - RepositoryInterfaces_ShouldBeIn_DomainLayer
**Why**: Enforce DDD principle that repository contracts belong in Domain layer

**Steps**:
1. Add test method to find repository interfaces
2. Validate namespace location against Domain layer patterns  
3. Report misplaced interfaces with move guidance
4. Ensure consistent error formatting

**Files to touch**:
- `tests/ArchitectureTests/CleanArchitectureRules.cs` (add test method)

**Implementation Details**:
```csharp
[Test]
public void RepositoryInterfaces_ShouldBeIn_DomainLayer()
{
    var repositoryInterfaces = Types.InCurrentDomain()
        .That()
        .AreInterfaces()
        .GetTypes()
        .Where(IsRepositoryInterface)
        .ToList();

    var violations = repositoryInterfaces
        .Where(type => !IsInDomainLayer(type))
        .Select(type => type.FullName)
        .ToList();

    violations.ShouldBeEmpty(
        ArchitectureTestHelpers.FormatViolations(violations,
            "Repository interfaces should be placed in Domain layer"));
}
```

---

### T6: Implement Test AC5 - Repositories_ShouldWork_WithAggregateRootsOnly
**Why**: Ensure repositories operate on aggregate roots, not individual entities

**Steps**:
1. Add test method to analyze repository interface methods
2. Check method parameters and return types for aggregate root patterns
3. Validate types inherit from AggregateRoot<TId> or follow aggregate conventions
4. Report non-aggregate types with design guidance

**Files to touch**:
- `tests/ArchitectureTests/CleanArchitectureRules.cs` (add test method)

**Implementation Details**:
```csharp
[Test]
public void Repositories_ShouldWork_WithAggregateRootsOnly()
{
    var repositoryInterfaces = Types.InCurrentDomain()
        .That()
        .AreInterfaces()
        .GetTypes()
        .Where(IsRepositoryInterface)
        .Where(IsInDomainLayer)
        .ToList();

    var violations = new List<string>();

    foreach (var repoInterface in repositoryInterfaces)
    {
        var methods = repoInterface.GetMethods()
            .Where(m => !m.IsSpecialName)
            .ToList();

        foreach (var method in methods)
        {
            // Check return types
            var returnType = method.ReturnType;
            if (returnType.IsGenericType)
            {
                var genericArgs = returnType.GetGenericArguments();
                foreach (var arg in genericArgs.Where(a => a.IsClass && !a.IsPrimitive))
                {
                    if (!IsAggregateRootType(arg))
                    {
                        violations.Add($"{repoInterface.Name}.{method.Name} returns non-aggregate type {arg.Name}");
                    }
                }
            }

            // Check parameters
            foreach (var param in method.GetParameters())
            {
                if (param.ParameterType.IsClass && !param.ParameterType.IsPrimitive && 
                    !IsAggregateRootType(param.ParameterType))
                {
                    violations.Add($"{repoInterface.Name}.{method.Name} parameter {param.Name} is non-aggregate type {param.ParameterType.Name}");
                }
            }
        }
    }

    violations.ShouldBeEmpty(
        ArchitectureTestHelpers.FormatViolations(violations,
            "Repository methods should work with aggregate roots only. Consider proper aggregate design"));
}
```

---

### T7: Integration Testing and Performance Validation
**Why**: Ensure new tests integrate properly with existing suite and maintain performance

**Steps**:
1. Run full test suite to verify no regressions in existing 76 tests
2. Measure execution time to ensure within 366ms + reasonable margin
3. Test with mock repository scenarios to verify violation detection
4. Validate error message formatting and actionability

**Files to touch**:
- `tests/ArchitectureTests/CleanArchitectureRules.cs` (validation only)

**Validation Checklist**:
- [ ] All existing tests remain passing
- [ ] New tests execute within performance budget  
- [ ] Error messages provide actionable guidance
- [ ] No false positives on legitimate repository patterns
- [ ] Namespace detection works with project structure

## Milestones & Criteria

### M1: Helper Methods Foundation (T1)
**Criterion**: All helper methods compile and provide consistent type detection
**Verification**: Unit test helper methods with known types

### M2: Core Repository Tests (T2-T6)  
**Criterion**: All 5 test methods implemented and passing on current codebase
**Verification**: `dotnet test` shows 81 tests passing (76 existing + 5 new)

### M3: Integration and Performance (T7)
**Criterion**: Full test suite executes within acceptable performance window
**Verification**: Test execution time ≤ 450ms (366ms + 20% margin)

## Rollback Plan

### Immediate Rollback (if tests break existing suite)
1. **Revert commits**: Git revert of all changes to CleanArchitectureRules.cs
2. **Restore state**: Verify 76 original tests still pass
3. **Performance check**: Confirm original ~366ms execution time restored

### Partial Rollback (if specific tests cause issues)
1. **Comment out problematic test method**: Use `[Ignore]` attribute temporarily
2. **Isolate issue**: Run remaining new tests to identify specific problem
3. **Fix and re-enable**: Address root cause and restore test

### Emergency Rollback (if CI/CD pipeline breaks)
1. **Hotfix branch**: Create immediate fix branch from last known good commit
2. **Remove failing tests**: Comment out or remove problematic test methods
3. **Deploy fix**: Emergency deployment to restore CI/CD pipeline
4. **Schedule rework**: Plan implementation fixes for next sprint

## Effort Estimate

**Size**: **M** (Medium)

**Breakdown**:
- **T1 (Helper Methods)**: 2-3 hours - Foundation implementation with multiple utility methods
- **T2-T6 (Test Methods)**: 4-6 hours - 5 test methods with specific validation logic  
- **T7 (Integration/Performance)**: 2-3 hours - Testing, validation, and performance tuning

**Total Estimate**: 8-12 hours

**Risk Factors**:
- **Namespace pattern complexity**: May need iteration to handle edge cases
- **Aggregate root detection**: Could require refinement based on actual patterns
- **Performance optimization**: May need caching or efficient LINQ patterns

**Dependencies**:
- **No blocking dependencies**: All required infrastructure exists
- **Parallel work possible**: Helper methods can be developed alongside test methods
- **Immediate feedback**: Each test can be validated independently