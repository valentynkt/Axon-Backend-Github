# Axon Backend Essentials

## Project Identity
- **Axon Backend**: Modular Monolith using Clean Architecture + DDD + CQRS
- **.NET 10 Preview**: Latest C# features required
- **Story-Driven Development**: Research-first BMad methodology

## Mandatory Architectural Patterns
- **Clean Architecture**: Domain → Application → Infrastructure → API
- **CQRS + MediatR**: Commands/Queries with handlers
- **Result<T> Pattern**: Functional error handling, no exceptions
- **Strong IDs**: Type-safe entity identifiers
- **FastEndpoints**: Minimal API implementation

## Research-First Protocol (MANDATORY)
1. Load story from `Docs/stories/`
2. Research existing .NET solutions
3. Create ADR with evidence
4. Implement using researched approach

## Pre-Approved Libraries
- **Authentication**: Microsoft.AspNetCore.Identity
- **Validation**: FluentValidation (already in project)
- **Caching**: Microsoft.Extensions.Caching.*
- **HTTP**: HttpClientFactory + Refit
- **Testing**: NUnit + Testcontainers

## Quality Gates
- Research-gate completed with ADR
- All acceptance criteria satisfied
- Clean Architecture compliance
- Test coverage >90%