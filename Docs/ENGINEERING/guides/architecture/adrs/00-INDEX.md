# Architecture Decision Records (ADRs)

**Record of significant architectural decisions made in Axon Backend.**

---

## What are ADRs?

Architecture Decision Records capture important architectural decisions along with their context and consequences. They provide historical context for why certain technical choices were made.

**Format**: Each ADR follows a structured template documenting:
- Context and problem statement
- Decision made
- Rationale and alternatives considered
- Consequences (positive and negative)

---

## Active ADRs

### Core Architecture

**[ADR-001: Modular Monolith](./001-modular-monolith.md)**
📅 2025-01-15 | Status: ✅ Accepted

Chose modular monolith over microservices for initial architecture.

**Key Decision**: Start with modular monolith, maintain clear module boundaries, enable future extraction to microservices.

---

**[ADR-002: CQRS with MediatR](./002-cqrs-mediatr.md)**
📅 2025-01-15 | Status: ✅ Accepted

Implement CQRS pattern using MediatR library for command/query separation.

**Key Decision**: Use MediatR for in-process messaging, separate write and read models.

---

**[ADR-003: Result Pattern for Error Handling](./003-result-pattern.md)**
📅 2025-01-16 | Status: ✅ Accepted

Use Result<T, Error> pattern instead of exceptions for business logic errors.

**Key Decision**: Adopt CSharpFunctionalExtensions for Result<T> implementation.

---

**[ADR-004: Strong Typed IDs](./004-strong-ids.md)**
📅 2025-01-16 | Status: ✅ Accepted

Use StrongId<T> pattern for type-safe entity identifiers.

**Key Decision**: Use StronglyTypedId source generator for compile-time type safety.

---

**[ADR-005: PostgreSQL as Primary Database](./005-postgresql.md)**
📅 2025-01-17 | Status: ✅ Accepted

Selected PostgreSQL over SQL Server, MySQL, and MongoDB.

**Key Decision**: PostgreSQL offers best balance of performance, features, and cost.

---

**[ADR-006: FastEndpoints over Controllers](./006-fastendpoints.md)**
📅 2025-01-20 | Status: ✅ Accepted

Use FastEndpoints instead of traditional ASP.NET Core controllers.

**Key Decision**: Vertical slice architecture, minimal API style, better performance.

---

## Proposed ADRs

### Future Decisions

**ADR-00X: Event Bus Selection**
Status: 🔄 Proposed

Evaluate RabbitMQ, Azure Service Bus, or AWS SQS for inter-module events.

---

**ADR-00X: Caching Strategy**
Status: 🔄 Proposed

Define multi-tier caching strategy (Memory, Redis, CDN).

---

## Superseded ADRs

None yet.

---

## ADR Template

When creating a new ADR, use this template:

```markdown
# ADR-XXX: [Decision Title]

**Status**: Proposed | Accepted | Deprecated | Superseded
**Date**: YYYY-MM-DD
**Deciders**: [Names]
**Technical Story**: [Link to ticket/issue]

---

## Context and Problem Statement

[Describe the context and problem that needs to be addressed.
What is the issue we're trying to solve?]

---

## Decision Drivers

- [Driver 1 - e.g., performance requirements]
- [Driver 2 - e.g., team expertise]
- [Driver 3 - e.g., cost constraints]

---

## Considered Options

### Option 1: [Name]

**Pros:**
- [Advantage 1]
- [Advantage 2]

**Cons:**
- [Disadvantage 1]
- [Disadvantage 2]

### Option 2: [Name]

**Pros:**
- [Advantage 1]

**Cons:**
- [Disadvantage 1]

---

## Decision Outcome

**Chosen option**: "Option 1: [Name]"

**Rationale:**
[Explain why this option was chosen over alternatives]

---

## Consequences

### Positive
- [Positive consequence 1]
- [Positive consequence 2]

### Negative
- [Negative consequence 1]
- [Negative consequence 2]

### Risks
- [Risk 1 and mitigation]

---

## Implementation Notes

[Specific implementation guidance, if applicable]

---

## Related Decisions

- [ADR-XXX: Related decision]

---

## References

- [Link to research]
- [Link to documentation]
```

---

## Related Documentation

- **System Overview** → [../system-overview.md](../system-overview.md)
- **Tech Stack** → [../tech-stack.md](../tech-stack.md)
- **Modular Monolith** → [../modular-monolith.md](../modular-monolith.md)

---

**Last Updated**: 2025-09-29