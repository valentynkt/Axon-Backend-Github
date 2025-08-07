# Epic 04: CQRS Foundation Implementation

## Epic Overview

**Epic ID**: Epic_04  
**Epic Name**: CQRS Foundation Implementation  
**Priority**: Critical  
**Estimated Duration**: 1-2 days  
**Dependencies**: Epic_01 (Functional Foundation)

## Business Value

Establishes the core CQRS abstractions and message contracts that enable clean separation of read/write operations, improved testability, and consistent Result<T> patterns across the application layer. This epic focuses exclusively on defining the interfaces and immutable message contracts that form the foundation of our CQRS architecture.

## Acceptance Criteria

- [ ] All CQRS interfaces and message contracts implemented
- [ ] Result<T> pattern fully integrated with command/query interfaces
- [ ] Correlation ID tracking implemented in message contracts
- [ ] Metadata support for commands/queries
- [ ] Caching declarative properties for queries
- [ ] No breaking changes to existing handlers
- [ ] 100% test coverage for abstractions

## Technical Scope

### 1. Command Contracts
- `ICommand<TResponse>` and `ICommand` interfaces
- `CommandBase<TResponse>` and `CommandBase` record implementations
- `ICommandHandler<TCommand, TResponse>` interface

### 2. Query Contracts  
- `IQuery<TResponse>` with declarative caching support
- `IPagedQuery<TResponse>` for pagination scenarios
- `QueryBase<TResponse>` and `PagedQueryBase<TResponse>` record implementations
- `IQueryHandler<TQuery, TResponse>` interface

### 3. Message Infrastructure
- Correlation ID generation and tracking
- Metadata dictionary for extensible context
- Result<T> integration throughout contracts
- Immutable record-based message design

## Key Design Decisions

### Rejection of Handler Base Classes

This epic explicitly **rejects** the use of handler base classes (e.g., `CommandHandlerBase`, `QueryHandlerBase`) as an architectural anti-pattern. Such inheritance-based approaches lead to:

- **Tight coupling** between handlers and infrastructure concerns
- **Reduced flexibility** in handler implementations
- **Violation of composition over inheritance** principles
- **Difficulty in unit testing** due to inherited dependencies

Instead, all cross-cutting concerns (logging, telemetry, validation, transactions, error wrapping) are handled exclusively through **MediatR pipeline behaviors** using the **Decorator pattern**. This approach provides:

- **Loose coupling** through composition
- **Flexible configuration** of behaviors per request type
- **Easy testing** with isolated units of work
- **Clear separation** of business logic from infrastructure

The sole responsibility of a command or query handler should be to execute its specific business logic and return a Result<T>.

## User Stories

### Story 1: Command Message Contracts
**As a developer**, I want command interfaces and immutable message contracts so that all commands follow consistent patterns and support metadata/correlation tracking.

**Tasks:**
- [ ] Create `ICommand<TResponse>` marker interface with CorrelationId and Metadata properties
- [ ] Create `ICommand` interface for non-returning commands
- [ ] Implement `CommandBase<TResponse>` record with auto-generated correlation ID
- [ ] Implement `CommandBase` record for void commands
- [ ] Create `ICommandHandler<TCommand, TResponse>` interface returning Result<T>
- [ ] Add unit tests for all command contracts

**Acceptance Criteria:**
- Commands automatically generate unique correlation IDs
- All command handlers must return Result<T> or Result
- Metadata can be attached to commands for context
- Command contracts are immutable records

### Story 2: Query Message Contracts
**As a developer**, I want query interfaces and immutable message contracts so that all queries support declarative caching configuration and consistent Result<T> patterns.

**Tasks:**
- [ ] Create `IQuery<TResponse>` interface with UseCache and CacheDuration properties
- [ ] Create `IPagedQuery<TResponse>` extending IQuery with pagination properties
- [ ] Implement `QueryBase<TResponse>` record with cache configuration
- [ ] Implement `PagedQueryBase<TResponse>` record with pagination support
- [ ] Create `IQueryHandler<TQuery, TResponse>` interface returning Result<T>
- [ ] Add unit tests for all query contracts

**Acceptance Criteria:**
- Queries can declaratively specify caching behavior
- Paginated queries include sorting and paging metadata
- All query handlers must return Result<T>
- Query contracts are immutable records

## Definition of Done

- [ ] All interfaces and message contracts implemented as immutable records
- [ ] Unit tests achieve 100% coverage for contracts
- [ ] Integration tests validate contract usage with MediatR
- [ ] Documentation updated with usage examples
- [ ] Code review approved by architecture team
- [ ] No compiler warnings or analyzer violations
- [ ] All contracts follow functional programming principles

## Technical Implementation Notes

### File Structure
```
src/BuildingBlocks/Core/Abstractions/CQRS/
├── ICommand.cs                 # Command marker interfaces
├── ICommandHandler.cs          # Command handler interface
├── IQuery.cs                   # Query marker interfaces  
├── IQueryHandler.cs            # Query handler interface
├── CommandBase.cs              # Immutable command records
├── QueryBase.cs                # Immutable query records
└── PagedQueryBase.cs           # Paginated query record
```

### Key Design Principles

1. **Immutable Records**: All commands/queries implemented as C# records
2. **Correlation Tracking**: Auto-generated correlation IDs for traceability
3. **Metadata Support**: Extensible context through metadata dictionary
4. **Result<T> Integration**: Consistent functional error handling
5. **Declarative Caching**: Query-level cache configuration
6. **Composition Over Inheritance**: Pipeline behaviors instead of base classes

### Interface Design

```csharp
// Command contracts
public interface ICommand<out TResponse> 
{
    Guid CorrelationId { get; }
    Dictionary<string, object> Metadata { get; }
}

// Query contracts  
public interface IQuery<out TResponse>
{
    Guid CorrelationId { get; }
    Dictionary<string, object> Metadata { get; }
    bool UseCache { get; }
    TimeSpan? CacheDuration { get; }
}

// Handler contracts
public interface ICommandHandler<in TCommand, TResponse> 
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken);
}
```

### Dependencies
- MediatR for request/response pattern
- System.Diagnostics.Activity for correlation tracking
- Result<T> types from Epic_01 (Functional Foundation)
- No additional dependencies for base classes (eliminated)

### Cross-Cutting Concerns Strategy
All infrastructure concerns are handled through **MediatR Pipeline Behaviors**:
- **Logging Behavior**: Structured logging with correlation IDs
- **Telemetry Behavior**: Activity tracking and performance metrics  
- **Validation Behavior**: FluentValidation integration
- **Transaction Behavior**: Database transaction management
- **Caching Behavior**: Query result caching based on declarative properties
- **Error Handling Behavior**: Exception wrapping into Result<T>

## Testing Strategy

### Unit Tests
- Interface contract validation
- Record immutability verification
- Correlation ID generation
- Metadata handling
- Default value behavior

### Integration Tests
- MediatR integration with command/query contracts
- Pipeline behavior interaction
- Result<T> propagation through handlers
- Correlation ID flow validation

## Success Metrics
- 100% test coverage on all abstractions
- Zero breaking changes to existing handlers
- All new handlers follow contract-based approach
- Pipeline behaviors handle all cross-cutting concerns
- Elimination of inheritance-based handler patterns

## Risk Mitigation

### Architectural Risks
- **Pattern Confusion**: Clear documentation distinguishing contracts from behaviors
- **Migration Complexity**: Gradual migration strategy from any existing base classes
- **Performance Impact**: Benchmark pipeline behavior composition

### Implementation Risks  
- **Contract Evolution**: Maintain backward compatibility in interface changes
- **Metadata Overuse**: Guidelines for appropriate metadata usage
- **Cache Configuration**: Sensible defaults for query caching behavior

This epic establishes the foundational contracts for a clean, composable CQRS architecture that leverages functional programming principles and avoids common inheritance-based anti-patterns.