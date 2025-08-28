# .NET 10 Library Compatibility Research

## Research Question
Identify specific versions of key libraries compatible with .NET 10 (net10.0) for the Axon Backend project, including CQRS, validation, testing, API endpoints, observability, data access, logging, and resilience patterns.

## Primary Source Documentation

### 1. MediatR - CQRS Command/Query Handling
- **Package ID**: `MediatR`
- **Latest Version**: `13.0.0`
- **Source**: [NuGet Gallery](https://www.nuget.org/packages/MediatR)
- **.NET 10 Support**: ✅ **CONFIRMED** - Documentation mentions .NET 10 preview 4+ features support
- **Installation**: `<PackageReference Include="MediatR" Version="13.0.0" />`
- **Additional**: `MediatR.Contracts` v2.0.1 available; DI extensions now included in main package
- **Breaking Changes**: Version 13.0+ requires license key registration

**Apply to Axon Backend**: ✅ **YES** - Core CQRS requirement with confirmed .NET 10 compatibility

### 2. FluentValidation - Input Validation  
- **Package ID**: `FluentValidation`
- **Latest Version**: `12.0.0`
- **Source**: [FluentValidation Documentation](https://docs.fluentvalidation.net/en/latest/)
- **.NET 10 Support**: ⚠️ **LIKELY** - Mentions .NET 10 preview 4 support for C# file-based apps
- **Installation**: `<PackageReference Include="FluentValidation" Version="12.0.0" />`
- **Breaking Changes**: Major version 12.0 has breaking changes from 11.x
- **Additional**: `FluentValidation.AspNetCore` with independent versioning post-11.1.0

**Apply to Axon Backend**: ✅ **YES** - High probability of compatibility, monitor for explicit net10.0 TFM

### 3. Testing Framework - xUnit vs NUnit
- **Recommendation**: **xUnit** for modern .NET development
- **xUnit Package**: `xunit` v2.9.3 (stable) / v3.0.0 (current)
- **Test Runner**: `xunit.runner.visualstudio` v3.1.3
- **Source**: [xUnit.net](https://xunit.net/)
- **.NET 10 Support**: ❌ **NOT YET** - xUnit v3 supports .NET 8+, v2 similar
- **NUnit Alternative**: `NUnit` v4.3.2 (similar compatibility status)

**Apply to Axon Backend**: ⏳ **WAIT** - Use xUnit 3.0.0 and monitor for .NET 10 support updates

### 4. Moq - Mocking Framework
- **Package ID**: `Moq` 
- **Latest Version**: `4.20.72`
- **Source**: [GitHub - devlooped/moq](https://github.com/devlooped/moq)
- **.NET 10 Support**: ⚠️ **LIKELY** - Related ecosystem packages mention .NET 10 preview 4
- **Installation**: `<PackageReference Include="Moq" Version="4.20.72" />`

**Apply to Axon Backend**: ✅ **YES** - High probability based on ecosystem readiness

### 5. FastEndpoints - Minimal API Alternative
- **Package ID**: `FastEndpoints`
- **Latest Version**: `7.0.1`
- **Source**: [FastEndpoints.com](https://fast-endpoints.com/)
- **.NET 10 Support**: ✅ **CONFIRMED** - "You can start targeting net10.0 SDK in your FE projects now"
- **Installation**: `<PackageReference Include="FastEndpoints" Version="7.0.1" />`
- **Additional**: `FastEndpoints.Swagger` v7.0.0
- **Architecture**: Perfect fit for REPR pattern in Clean Architecture

**Apply to Axon Backend**: ✅ **YES** - Explicitly supports .NET 10, ideal for vertical slice endpoints

### 6. OpenTelemetry - Observability & Tracing
- **Package ID**: `OpenTelemetry`
- **Latest Version**: `1.12.0`
- **Source**: [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/dotnet/)
- **.NET 10 Support**: ✅ **HIGH CONFIDENCE** - Targets .NET Standard 2.0 + modern .NET versions
- **Installation**: `<PackageReference Include="OpenTelemetry" Version="1.12.0" />`
- **Framework Integration**: Uses Microsoft.Extensions.Logging.ILogger

**Apply to Axon Backend**: ✅ **YES** - Strong compatibility expectation based on framework-agnostic design

### 7. System.Diagnostics.Activity - W3C Tracing
- **Package**: **BUILT-IN** - No NuGet package required
- **Version**: Included in .NET 10 framework
- **Source**: [Microsoft Learn - Distributed Tracing](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-concepts)
- **.NET 10 Support**: ✅ **CONFIRMED** - Core framework component
- **Features**: W3C TraceContext (default .NET 5+), ActivitySource API, automatic HTTP header flow

**Apply to Axon Backend**: ✅ **YES** - Built-in framework capability, no dependencies needed

### 8. Entity Framework Core - Data Access
- **Package ID**: `Microsoft.EntityFrameworkCore`
- **Stable Version**: `9.0.7`
- **Preview Version**: `10.0.0-preview.2.25163.8`
- **Source**: [EF Core Releases](https://learn.microsoft.com/en-us/ef/core/what-is-new/)
- **.NET 10 Support**: ✅ **CONFIRMED** - EF Core 10 preview available
- **Installation**: `<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0-preview.2.25163.8" />`

**Apply to Axon Backend**: ✅ **YES** - Use EF Core 10 preview for .NET 10 compatibility

### 9. Serilog - Structured Logging
- **Package ID**: `Serilog`
- **Latest Version**: `4.3.0`
- **Source**: [Serilog.net](https://serilog.net/)
- **.NET 10 Support**: ✅ **HIGH CONFIDENCE** - Targets .NET Standard 2.0 + recent .NET versions
- **Installation**: `<PackageReference Include="Serilog" Version="4.3.0" />`
- **ASP.NET Core**: `Serilog.AspNetCore` v9.0.0
- **Extensions**: `Serilog.Extensions.Logging` v9.0.2

**Apply to Axon Backend**: ✅ **YES** - Excellent cross-platform compatibility history

### 10. Polly - Resilience Patterns
- **Package ID**: `Polly` / `Polly.Core`
- **Versions**: `Polly` v8.4.0, `Polly.Core` v8.6.2
- **Source**: [GitHub - App-vNext/Polly](https://github.com/App-vNext/Polly)
- **.NET 10 Support**: ✅ **LIKELY** - Targets .NET 6.0+ and .NET Standard 2.0
- **Installation**: `<PackageReference Include="Polly.Core" Version="8.6.2" />`
- **Integration**: `Microsoft.Extensions.Http.Polly` v9.0.7
- **Patterns**: Retry, Circuit Breaker, Timeout, Rate-limiting, Hedging, Fallback

**Apply to Axon Backend**: ✅ **YES** - Framework design supports modern .NET versions

## Recommended Package Configuration

```xml
<!-- Core CQRS & Architecture -->
<PackageReference Include="MediatR" Version="13.0.0" />
<PackageReference Include="FastEndpoints" Version="7.0.1" />
<PackageReference Include="FluentValidation" Version="12.0.0" />

<!-- Data Access -->
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0-preview.2.25163.8" />
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.0-preview.2.25163.8" />

<!-- Observability -->
<PackageReference Include="OpenTelemetry" Version="1.12.0" />
<PackageReference Include="Serilog" Version="4.3.0" />
<PackageReference Include="Serilog.AspNetCore" Version="9.0.0" />

<!-- Resilience -->
<PackageReference Include="Polly.Core" Version="8.6.2" />

<!-- Testing -->
<PackageReference Include="xunit" Version="3.0.0" />
<PackageReference Include="xunit.runner.visualstudio" Version="3.1.3" />
<PackageReference Include="Moq" Version="4.20.72" />
```

## Confidence Assessment

**HIGH CONFIDENCE** (Apply Immediately):
- FastEndpoints 7.0.1 - Explicit .NET 10 support
- EF Core 10.0.0-preview.2 - Official .NET 10 preview
- System.Diagnostics.Activity - Built-in framework
- Serilog 4.3.0 - Strong compatibility record

**MEDIUM-HIGH CONFIDENCE** (Apply with Monitoring):
- MediatR 13.0.0 - Confirmed compatibility mentions
- OpenTelemetry 1.12.0 - Framework-agnostic design  
- Polly.Core 8.6.2 - Modern .NET targeting
- FluentValidation 12.0.0 - Ecosystem readiness indicators
- Moq 4.20.72 - Related package compatibility

**MONITOR FOR UPDATES**:
- xUnit 3.0.0 - Currently .NET 8+, expect .NET 10 support soon

## Links to Primary Sources

- [MediatR 13.0.0 - NuGet](https://www.nuget.org/packages/MediatR)
- [FluentValidation Documentation](https://docs.fluentvalidation.net/en/latest/)
- [FastEndpoints Official Site](https://fast-endpoints.com/)
- [OpenTelemetry .NET Documentation](https://opentelemetry.io/docs/languages/dotnet/)
- [EF Core What's New](https://learn.microsoft.com/en-us/ef/core/what-is-new/)
- [Serilog Official Site](https://serilog.net/)
- [Polly GitHub Repository](https://github.com/App-vNext/Polly)
- [xUnit.net Documentation](https://xunit.net/)
- [Microsoft Distributed Tracing](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-concepts)

## Axon Backend Architecture Alignment

All recommended packages align with the project's Clean Architecture + DDD + CQRS patterns:

- **FastEndpoints**: Perfect for vertical slice endpoints per module
- **MediatR**: Core to CQRS command/query handling
- **FluentValidation**: Input validation at Application layer boundary
- **EF Core**: Infrastructure layer data access implementation
- **Serilog**: Cross-cutting structured logging concern
- **OpenTelemetry**: Observability without breaking architectural boundaries
- **Polly**: Resilience patterns in Infrastructure adapters

This research supports the project's goal of shipping fast via vertical slices while maintaining clean architectural seams for potential microservice extraction.