# Technical Assumptions

## Repository Structure: Monorepo
The existing modular monolith structure under `src/Modules/Identity` will be maintained, allowing focused work within the Identity module boundaries while leveraging shared BuildingBlocks.

## Service Architecture
**Modular Monolith with Clean Architecture + CQRS + DDD** - Maintaining the established .NET 10 FastEndpoints architecture with MediatR command/query dispatch, ensuring Epic 2 changes align with existing patterns while eliminating dual command violations.

## Testing Requirements
**Full Testing Pyramid** - Comprehensive testing approach with unit tests (>90% coverage for business logic), integration tests using Testcontainers, and end-to-end API tests. NUnit + Shouldly + NSubstitute + Testcontainers stack will be used consistently.

## Additional Technical Assumptions and Requests

- **.NET 10 Preview Maintenance**: Continue using .NET 10 preview 5.25277.114 with FastEndpoints for API layer consistency
- **Entity Framework Core 10**: Leverage compiled queries for hot path optimizations and maintain PostgreSQL as primary database
- **Result Pattern Consistency**: All new code must use `Result<T, Error>` pattern from CSharpFunctionalExtensions for error handling
- **Strong ID Pattern Enforcement**: Maintain type-safe `StrongId<T>` patterns throughout, ensuring no primitive obsession in new implementations
- **OpenTelemetry Integration**: All new handlers must include distributed tracing with correlation IDs for production observability
- **FluentValidation Standards**: All command/query validation must use FluentValidation with consistent error message formatting
- **Polly Resilience**: Replace manual retry logic in DynamicAuthService with Polly policies for standardized resilience patterns
- **System.Text.Json Consistency**: Maintain camelCase naming policy and custom converters for StrongIds and Result types
- **Memory Caching Strategy**: Use Microsoft.Extensions.Caching.Memory for JWKS caching with appropriate expiration policies
- **Rate Limiting Implementation**: Use ASP.NET Core 7+ built-in rate limiting middleware with per-IP tracking
- **Database Migration Strategy**: All schema changes must be reversible with proper foreign key constraints and cascade behaviors
- **Code Style Enforcement**: File-scoped namespaces, target-typed new expressions, and nullable reference types enabled throughout
