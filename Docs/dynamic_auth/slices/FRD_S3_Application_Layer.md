# FRD S3 - Application Layer

**Stage**: S3 - API > Infrastructure > Application  
**Layer**: Application Layer  
**Dependencies**: S2 (Infrastructure Integration)  

## Responsibility

Implement CQRS application layer with commands, queries, and handlers. Abstract infrastructure dependencies behind ports and implement business logic orchestration following Clean Architecture principles.

## Components to Implement

- **Commands**: `ValidateJwtCommand`, `SyncUserCommand`, `ProcessWebhookCommand`
- **Queries**: `GetCurrentUserQuery`, `GetUserWalletsQuery`
- **Handlers**: MediatR command/query handlers with business logic
- **Application Ports**: `IJwtValidationService`, `IUserSyncService`, `IWebhookProcessor`
- **Validators**: FluentValidation for commands and queries
- **Behaviors**: Authentication, validation, and logging pipeline behaviors

## Stage Progression

- **S3**: Application orchestration (current stage)
- **S4**: Add rich domain models and business rules
- **S5**: Full data persistence and transactions

## Exit Criteria

- CQRS pattern implemented with MediatR
- Infrastructure abstracted behind application ports
- Authentication pipeline integrated with `ICurrentUserService`
- Validation behaviors enforce business rules
- All handlers return `Result<T>` for error handling

## Key Deliverables

- Complete CQRS implementation
- Application service abstractions
- Business logic orchestration
- Authentication behavior integration