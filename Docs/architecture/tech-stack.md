# Technology Stack - Axon Backend

## Core Framework
- **.NET 10.0** (preview 5.25277.114) - Latest C# features required
- **FastEndpoints** - Minimal API alternative to controllers
- **MediatR** - CQRS command/query dispatch
- **FluentValidation** - Request validation framework

## Data & Persistence
- **Entity Framework Core 9.0** - ORM with PostgreSQL
- **PostgreSQL** - Primary database
- **Strong ID Generators** - Type-safe entity identifiers

## JSON & Serialization
- **System.Text.Json** - API serialization
- Custom converters for StrongId/Result types
- camelCase naming policy for APIs

## Quality & Testing
- **NUnit** - Testing framework
- **Shouldly** - Fluent assertions
- **Testcontainers** - Integration testing with real databases
- **NSubstitute** - Mocking framework

## Monitoring & Observability
- **OpenTelemetry** - Distributed tracing
- **Structured logging** with correlation IDs
- **Problem Details** for HTTP error responses

## Build & Development
- **MSBuild** with Directory.Build.props
- **Analyzers** enabled (warnings as errors in Release)
- **AOT Analysis** for library compatibility
- **Deterministic builds** for reproducibility

## Authentication & Security
- **Microsoft.AspNetCore.Identity** - User management (when needed)
- **JWT Bearer Authentication** - API authentication
- **Authorization policies** - Role-based access control

## Caching Strategy
- **Microsoft.Extensions.Caching.Memory** - In-memory caching
- **Microsoft.Extensions.Caching.StackExchangeRedis** - Distributed caching
- **OutputCaching** - Built-in HTTP response caching

## HTTP & Integration
- **HttpClientFactory** - Typed HTTP clients
- **Refit** - REST client generation
- **Problem Details** - Standardized error responses

## Architecture Libraries
- **BuildingBlocks.Core** - Functional primitives, Result<T>
- **BuildingBlocks.Application** - MediatR behaviors
- **BuildingBlocks.Infrastructure** - Persistence, caching
- **BuildingBlocks.Validation** - FluentValidation integration
- **BuildingBlocks.Web** - HTTP concerns, problem details