# Validate Patterns Task

**Agent**: Axon Doc Oracle
**Purpose**: Validate code against architectural patterns and ADRs

---

## Task Instructions

### Pattern Validation Dimensions (6)

1. **Result<T> Pattern Compliance** (Weight: 25%)
   - All domain methods return `Result<T, Error>`
   - No exceptions thrown in domain layer
   - Error types are strongly typed
   - Success/Failure paths explicit

2. **StrongId<T> Compliance** (Weight: 20%)
   - All entity IDs use `StrongId<TEntity>`
   - No primitive obsession (Guid, int)
   - Type-safe ID passing

3. **CQRS Compliance** (Weight: 20%)
   - Commands separate from queries
   - MediatR handlers for all operations
   - Command: `IRequest<Result<T>>`
   - Query: `IRequest<Result<T>>`

4. **Clean Architecture Layering** (Weight: 15%)
   - Domain has no dependencies
   - Application depends only on Domain
   - Infrastructure depends on Application & Domain
   - API depends on all layers

5. **Domain Events** (Weight: 10%)
   - Aggregate changes raise domain events
   - Events immutable records
   - Event handlers in Application layer

6. **Owned Entities** (Weight: 10%)
   - Owned entities properly configured
   - Cannot exist without parent
   - Value object semantics

### Validation Process

1. **Read Code** (provided file/class)
2. **Check Each Dimension**
3. **Calculate Weighted Score**
   ```
   Total Score = Σ(Dimension Score × Weight)
   Target: ≥95%
   ```
4. **Generate Compliance Report**

### Output Format
```yaml
compliance_report:
  overall_score: 96
  target: 95
  status: PASS

  dimensions:
    - name: Result<T> Pattern
      score: 100
      weight: 25
      issues: []

    - name: StrongId<T>
      score: 90
      weight: 20
      issues:
        - "Line 42: Found Guid parameter, should be StrongId<User>"

  recommendations:
    - "Replace Guid with UserId (StrongId<User>) at line 42"
```

---

## TODO: Full Implementation
Implement pattern detection with Grep, scoring logic, and report generation.