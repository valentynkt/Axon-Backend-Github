# Technology Stack

**Libraries organized by Clean Architecture layer. Token-efficient reference for AI.**

---

## Runtime

| Library | Version | Purpose |
|---------|---------|---------|
| .NET | 10.0 (preview) | Runtime |
| C# | 13 | Language features |

---

## API Layer

| Library | Purpose | Docs |
|---------|---------|------|
| **FastEndpoints** | Minimal API endpoints | [docs](https://fast-endpoints.com) |
| **Microsoft.AspNetCore.Authentication.JwtBearer** | JWT auth | [docs](https://learn.microsoft.com/en-us/aspnet/core/security/authentication) |
| **Microsoft.AspNetCore.RateLimiting** | Rate limiting | [docs](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit) |
| **System.Text.Json** | JSON serialization | [docs](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json) |

---

## Application Layer (CQRS)

| Library | Purpose | Docs |
|---------|---------|------|
| **MediatR** | CQRS command/query dispatch | [docs](https://github.com/jbogard/MediatR) |
| **FluentValidation** | Request validation | [docs](https://docs.fluentvalidation.net) |
| **FluentValidation.DependencyInjectionExtensions** | DI integration | [docs](https://docs.fluentvalidation.net/en/latest/di.html) |

---

## Domain Layer

| Library | Purpose | Docs |
|---------|---------|------|
| **CSharpFunctionalExtensions** | Result<T, Error> pattern | [docs](https://github.com/vkhorikov/CSharpFunctionalExtensions) |
| **CSharpFunctionalExtensions.HttpResults** | ASP.NET Result integration | [docs](https://github.com/vkhorikov/CSharpFunctionalExtensions) |
| **StronglyTypedId** | Type-safe IDs (source gen) | [docs](https://github.com/andrewlock/StronglyTypedId) |
| **Vogen** | Value objects (source gen) | [docs](https://github.com/SteveDunn/Vogen) |

---

## Infrastructure Layer

### Persistence

| Library | Purpose | Docs |
|---------|---------|------|
| **Microsoft.EntityFrameworkCore** | ORM | [docs](https://learn.microsoft.com/en-us/ef/core/) |
| **Microsoft.EntityFrameworkCore.Design** | Migrations tooling | [docs](https://learn.microsoft.com/en-us/ef/core/cli/dotnet) |
| **Npgsql.EntityFrameworkCore.PostgreSQL** | PostgreSQL provider | [docs](https://www.npgsql.org/efcore/) |

### Caching

| Library | Purpose | Docs |
|---------|---------|------|
| **Microsoft.Extensions.Caching.Memory** | In-memory cache | [docs](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory) |
| **Microsoft.Extensions.Caching.StackExchangeRedis** | Redis cache | [docs](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed) |

### HTTP Clients

| Library | Purpose | Docs |
|---------|---------|------|
| **Microsoft.Extensions.Http** | HttpClientFactory | [docs](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory) |
| **Refit** | Typed REST clients | [docs](https://github.com/reactiveui/refit) |

### Observability

| Library | Purpose | Docs |
|---------|---------|------|
| **OpenTelemetry** | Distributed tracing | [docs](https://opentelemetry.io/docs/languages/net/) |
| **OpenTelemetry.Exporter.Console** | Console exporter | [docs](https://github.com/open-telemetry/opentelemetry-dotnet) |
| **OpenTelemetry.Extensions.Hosting** | DI integration | [docs](https://github.com/open-telemetry/opentelemetry-dotnet) |
| **Serilog** | Structured logging | [docs](https://serilog.net) |

---

## Testing

| Library | Purpose | Docs |
|---------|---------|------|
| **NUnit** | Test framework | [docs](https://nunit.org) |
| **NUnit.Analyzers** | Static analysis | [docs](https://github.com/nunit/nunit.analyzers) |
| **Shouldly** | Fluent assertions | [docs](https://shouldly.io) |
| **NSubstitute** | Mocking | [docs](https://nsubstitute.github.io) |
| **Testcontainers** | Integration tests | [docs](https://dotnet.testcontainers.org) |
| **Testcontainers.PostgreSql** | PostgreSQL containers | [docs](https://dotnet.testcontainers.org/modules/postgresql/) |
| **Microsoft.NET.Test.Sdk** | Test runner | [docs](https://learn.microsoft.com/en-us/dotnet/core/testing/) |

---

## Build & Tooling

| Tool | Purpose | Docs |
|------|---------|------|
| **dotnet CLI** | Build, test, run | [docs](https://learn.microsoft.com/en-us/dotnet/core/tools/) |
| **dotnet-ef** | EF Core migrations | [docs](https://learn.microsoft.com/en-us/ef/core/cli/dotnet) |
| **Directory.Build.props** | Centralized build config | [docs](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory) |

---

## BuildingBlocks (Internal)

**Location**: `src/BuildingBlocks/`

| Project | Purpose |
|---------|---------|
| **Core** | Error types, Result<T>, StrongId, CQRS interfaces |
| **Application** | MediatR behaviors, validation pipeline, domain events |
| **Infrastructure** | EF Core base, repositories, caching, external services |
| **Validation** | FluentValidation integration |
| **Web** | FastEndpoints base classes, problem details, Result mapping |

---

## Key Version Constraints

```xml
<!-- From Directory.Build.props -->
<TargetFramework>net10.0</TargetFramework>
<LangVersion>13</LangVersion>
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
```

---

## Related Docs

- [System Overview](./system-overview.md) - Architecture layers
- [ADRs](./adrs/00-INDEX.md) - Technology decisions
- [Libraries](../../../Libraries/00-INDEX.md) - Implementation guides

---

**Lines**: ~150
**Focus**: Quick library reference with docs links