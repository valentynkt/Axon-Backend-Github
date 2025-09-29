# ADR-001: Modular Monolith Architecture

**Status**: ✅ Accepted
**Date**: 2025-01-15
**Deciders**: Engineering Team
**Technical Story**: System Architecture Foundation

---

## Context and Problem Statement

Axon needs an architectural foundation that balances rapid development velocity with future scalability. The choice between monolithic, modular monolithic, and microservices architectures significantly impacts development speed, deployment complexity, and operational costs.

**Key Question**: What architectural style best serves Axon's current MVP needs while maintaining flexibility for future growth?

---

## Decision Drivers

- **Team Size**: Small team (2-5 developers) requires minimal operational overhead
- **Development Speed**: Need rapid iteration for MVP features
- **Deployment Complexity**: Limited DevOps resources
- **Future Scalability**: Must support future growth without major rewrites
- **Module Independence**: Clear boundaries between Identity and Chat domains
- **Testing**: Simplified testing and debugging compared to distributed systems
- **Cost**: Infrastructure costs should scale with usage

---

## Considered Options

### Option 1: Traditional Monolith

Single codebase with no enforced module boundaries.

**Pros:**
- Simplest to develop initially
- Single deployment unit
- No inter-service communication overhead
- Easiest debugging (single process)

**Cons:**
- Tight coupling between domains inevitable
- No clear ownership boundaries
- Difficult to extract services later
- All-or-nothing deployments
- Limited scalability (vertical only)

### Option 2: Microservices

Each module as independent service with separate databases.

**Pros:**
- Independent scaling per service
- Technology flexibility per service
- Clear ownership boundaries
- Independent deployments

**Cons:**
- High operational complexity for small team
- Distributed debugging challenges
- Network latency overhead
- Data consistency challenges
- Significant DevOps investment required
- Over-engineering for MVP phase

### Option 3: Modular Monolith ✅

Single deployable with enforced module boundaries and independent databases per module.

**Pros:**
- Clear module boundaries without distributed complexity
- Single deployment (simpler CI/CD)
- In-process communication (low latency)
- Easy debugging (single process)
- Future extraction to microservices possible
- Module-level ownership clear
- Simplified testing (no network mocking)

**Cons:**
- Requires discipline to maintain boundaries
- Shared infrastructure means less technology flexibility
- Cannot scale modules independently (initially)
- Single point of failure

---

## Decision Outcome

**Chosen option**: "Option 3: Modular Monolith"

**Rationale:**

1. **Right Size for Team**: Modular monolith provides clear boundaries without microservices overhead. Perfect for 2-5 developer teams.

2. **Development Velocity**: In-process communication and single deployment enable faster iteration than microservices while maintaining better organization than traditional monolith.

3. **Future-Proof**: Well-defined module boundaries (Identity, Chat) with separate databases make future extraction to microservices straightforward if needed.

4. **Operational Simplicity**: Single deployment artifact, one database per module, unified logging/monitoring. DevOps overhead minimal.

5. **Cost Efficiency**: Infrastructure costs stay low during MVP phase, can scale vertically before horizontal.

---

## Consequences

### Positive

✅ **Fast Development**: Team can ship features quickly without coordinating deployments across services.

✅ **Simplified Debugging**: Single process means standard debugging tools work without distributed tracing complexity.

✅ **Lower Operational Costs**: Single server, simple deployment pipeline, less infrastructure to monitor.

✅ **Clear Module Boundaries**: Enforced via namespace and project structure, preventing accidental coupling.

✅ **Future Flexibility**: Module boundaries enable extraction to microservices if scale demands it.

### Negative

❌ **Module Discipline Required**: Team must actively prevent cross-module dependencies (mitigated by code reviews, architectural tests).

❌ **Shared Technology Stack**: All modules use .NET, PostgreSQL (acceptable tradeoff for current needs).

❌ **Vertical Scaling Initially**: Cannot scale Identity independently of Chat (acceptable for MVP scale).

### Risks

**Risk 1: Module Boundary Violations**
- **Mitigation**: ArchUnit tests enforce "no cross-module references"
- **Mitigation**: Code review checklist includes module boundary verification

**Risk 2: Performance Bottlenecks**
- **Mitigation**: Design with async/await, proper caching from day one
- **Mitigation**: Monitor query performance, optimize database indexes proactively

**Risk 3: Deployment Downtime**
- **Mitigation**: Zero-downtime deployment strategies (blue-green or rolling updates)
- **Mitigation**: Database migrations run before code deployment

---

## Implementation Notes

### Module Structure

```
src/Modules/
├── Identity/
│   ├── Domain/           # Business logic
│   ├── Application/      # Use cases
│   └── Infrastructure/   # Data access, external services
│       └── Persistence/
│           └── DbContexts/
│               ├── IdentityWriteDbContext.cs  # Dedicated database
│               └── IdentityReadDbContext.cs
│
└── Chat/
    ├── Domain/
    ├── Application/
    └── Infrastructure/
        └── Persistence/
            └── DbContexts/
                ├── ChatWriteDbContext.cs      # Dedicated database
                └── ChatReadDbContext.cs
```

### Enforcement Mechanisms

1. **Project References**: Modules cannot reference each other's projects
2. **Database Isolation**: Each module has dedicated schema/database
3. **Events Only**: Inter-module communication via domain events only
4. **Architecture Tests**: ArchUnit tests validate boundaries in CI

### Communication Patterns

**Within Module**: Direct method calls
**Between Modules**: Domain events (in-process event bus)

```csharp
// ✅ Allowed: Within module
public class CreateUserCommandHandler
{
    private readonly IUserRepository _repository;  // Same module
}

// ❌ Forbidden: Cross-module direct reference
public class SendMessageCommandHandler
{
    private readonly IUserRepository _identityRepo;  // Different module!
}

// ✅ Allowed: Cross-module via events
public class UserCreatedEventHandler : IDomainEventHandler<UserCreated>
{
    // Chat module listens to Identity module events
}
```

---

## Related Decisions

- [ADR-002: CQRS with MediatR](./002-cqrs-mediatr.md) - Enables clean module boundaries
- [ADR-005: PostgreSQL](./005-postgresql.md) - Database per module implementation

---

## References

- [Modular Monolith: A Primer](https://www.kamilgrzybek.com/design/modular-monolith-primer/)
- [Monolith to Microservices by Sam Newman](https://www.oreilly.com/library/view/monolith-to-microservices/9781492047834/)
- [Pattern: Modular Monolith](https://microservices.io/patterns/monolithic.html)

---

**Last Updated**: 2025-09-29