# C# Coding Standards & Best Practices

## Modern C# Features (MANDATORY)
- **Records** for DTOs/Value Objects: `public record UserDto(string Name, string Email);`
- **Primary constructors** for classes when appropriate
- **File-scoped namespaces**: `namespace Axon.Modules.Chat;`
- **Target-typed new**: `List<string> items = new();`
- **Pattern matching** over traditional if/switch when cleaner
- **Nullable reference types** enabled - handle nulls explicitly

## Architecture Patterns
- **SOLID, KISS, YAGNI, DRY** principles consistently applied
- **Command/Handler pattern** with MediatR
- **Result pattern** for error handling (no exceptions for business logic)
- **CQRS** - Commands modify state, Queries read data
- **Value Objects** with validation in domain

## Error Handling
- Domain: Return `Result<T>` for business rule violations
- Application: Handle domain results, don't throw for business logic  
- Infrastructure: Wrap external exceptions in Result pattern
- API: Map Results to appropriate HTTP responses

## Naming Conventions
- **Namespaces:** `Axon.Modules.<Module>.<Layer>.<Area>`
- **Files:** One type per file, handler folders named after feature
- **Project names:** `Axon.Modules.Chat.Application` when split into projects