# FRD S4 - Domain Model

**Stage**: S4 - API > Infrastructure > Application > Domain  
**Layer**: Domain Layer  
**Dependencies**: S3 (Application Layer)  

## Responsibility

Implement rich domain models following Domain-Driven Design principles. Create aggregates, entities, value objects, and domain events that encapsulate business rules and invariants for user authentication and wallet management.

## Components to Implement

- **Aggregates**: `User` aggregate root with business logic
- **Entities**: `Wallet` entity with wallet-specific rules  
- **Value Objects**: `DynamicUserId`, `DynamicWalletId`, `WalletProperties`
- **Strong IDs**: `UserId`, `WalletId` using Axon's StrongId pattern
- **Domain Events**: `UserSynchronized`, `WalletConnected`, `AuthenticationCompleted`
- **Domain Services**: Complex business logic that doesn't belong in entities
- **Repository Contracts**: Domain interfaces for data access

## Stage Progression

- **S4**: Rich domain model with business rules (current stage)
- **S5**: Full persistence implementation with Entity Framework

## Exit Criteria

- User and Wallet aggregates implement business invariants
- Domain events published for state changes  
- Strong typing enforced throughout domain model
- Repository contracts defined in domain layer
- Business rules validated at domain boundaries

## Key Deliverables

- Complete domain model with DDD patterns
- Business rule enforcement in aggregates
- Domain event publishing infrastructure  
- Repository abstractions for persistence