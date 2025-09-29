# CQRS Patterns

**Command/Query separation with MediatR - interfaces, handlers, and pipeline behaviors.**

---

**STATUS**: 🚧 Scaffold - Needs Content
**PRIORITY**: High
**LAST_UPDATED**: 2025-01-29

---

## Content to be filled:

### Core Concepts
- Command vs Query distinction
- When to use commands vs queries
- MediatR integration overview

### Interfaces (from BuildingBlocks)
- ICommand<TResult> interface
- IQuery<TResult> interface
- ICommandHandler<TCommand, TResult>
- IQueryHandler<TQuery, TResult>

### Implementation Patterns
- Handler implementation patterns
- Command/Query naming conventions
- Result<T> integration
- Async/await best practices

### Pipeline Behaviors
- Validation behavior
- Logging behavior
- Transaction behavior
- Performance measurement
- Exception handling

### Examples
- Complete command example
- Complete query example
- Handler with dependencies
- Complex command with validation

---

**Related Documentation:**
- [Quick Reference](./00-QUICK-REFERENCE.md) - CQRS code examples
- [Domain Modeling](./domain-modeling.md) - Using Result<T> with commands
- [Validation](./validation.md) - FluentValidation in pipeline