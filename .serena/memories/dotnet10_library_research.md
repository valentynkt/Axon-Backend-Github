# .NET 10 Compatible Libraries Research - Axon Backend

## Research Question
Find specific versions of key libraries that are compatible with .NET 10 (net10.0) and should be used in the Axon Backend project for CQRS, validation, testing, API endpoints, observability, data access, logging, and resilience patterns.

## Primary Source Research Findings

### 1. MediatR - CQRS Command/Query Handling
- **Package**: `MediatR`
- **Latest Version**: `13.0.0`
- **.NET 10 Compatibility**: ✅ **Confirmed** - Supports .NET 10 preview 4+ features
- **Confidence**: **High** - Official NuGet documentation confirms compatibility
- **Additional Package**: MediatR.Contracts `2.0.1` for contracts
- **Note**: Main package now includes DI extensions (no separate package needed)

### 2. FluentValidation - Input Validation
- **Package**: `FluentValidation`
- **Latest Version**: `12.0.0`
- **.NET 10 Compatibility**: ✅ **Likely Compatible** - Mentions .NET 10 preview 4 support for C# file-based apps
- **Confidence**: **Medium** - No explicit net10.0 TFM confirmation but framework evolution suggests compatibility
- **Breaking Changes**: Version 12.0 has breaking changes from 11.x
- **Additional Package**: `FluentValidation.AspNetCore` (separate versioning after 11.1.0)

### 3. Testing Framework Recommendation: **xUnit**
- **Package**: `xunit`
- **Latest Version**: `2.9.3` (stable), `3.0.0` (current release)
- **Package**: `xunit.runner.visualstudio` `3.1.3`
- **.NET 10 Compatibility**: ⚠️ **Not Yet** - xUnit v3 supports .NET 8+, v2 similar range
- **Confidence**: **Medium** - Will likely get .NET 10 support in future updates
- **Recommendation**: Use xUnit v3 for modern design and performance
- **Alternative**: NUnit `4.3.2` (similar compatibility status)

### 4. Moq - Mocking Framework
- **Package**: `Moq`
- **Latest Version**: `4.20.72`
- **.NET 10 Compatibility**: ✅ **Likely Compatible** - Related packages mention .NET 10 preview 4 support
- **Confidence**: **Medium** - No explicit confirmation but ecosystem preparing for .NET 10
- **Note**: Most popular and friendly mocking framework for .NET

### 5. FastEndpoints - Minimal API Alternative  
- **Package**: `FastEndpoints`
- **Latest Version**: `7.0.1`
- **.NET 10 Compatibility**: ✅ **Confirmed** - "You can start targeting net10.0 SDK in your FE projects now"
- **Confidence**: **High** - Explicit .NET 10 support announced
- **Additional Package**: `FastEndpoints.Swagger` `7.0.0`
- **Architecture Fit**: Perfect for REPR pattern, alternative to Minimal APIs

### 6. OpenTelemetry - Observability & Tracing
- **Package**: `OpenTelemetry`
- **Latest Version**: `1.12.0`
- **.NET 10 Compatibility**: ✅ **Likely Compatible** - Targets .NET Standard 2.0 + .NET 8+
- **Confidence**: **High** - Framework-agnostic design supports latest .NET versions
- **Note**: Uses platform APIs like Microsoft.Extensions.Logging.ILogger
- **Key Packages**: Core, instrumentation packages, exporters for APM systems

### 7. System.Diagnostics.Activity - W3C Tracing
- **Package**: **Built into .NET Framework** - No NuGet package needed
- **Version**: Included in .NET 10
- **.NET 10 Compatibility**: ✅ **Confirmed** - Built-in framework component
- **Confidence**: **High** - Core framework API
- **Features**: W3C TraceContext support (default in .NET 5+), ActivitySource API, automatic HTTP header flow

### 8. Entity Framework Core - Data Access
- **Package**: `Microsoft.EntityFrameworkCore`
- **Latest Stable**: `9.0.7`
- **Preview Version**: `10.0.0-preview.2.25163.8` available
- **.NET 10 Compatibility**: ✅ **Confirmed** - EF Core 10 preview available
- **Confidence**: **High** - Official Microsoft product with .NET 10 preview releases
- **Additional Packages**: Provider-specific packages (SqlServer, etc.)

### 9. Serilog - Structured Logging
- **Package**: `Serilog`
- **Latest Version**: `4.3.0`
- **Package**: `Serilog.AspNetCore` `9.0.0`
- **.NET 10 Compatibility**: ✅ **High Compatibility** - Targets .NET Standard 2.0 + recent .NET versions
- **Confidence**: **High** - Excellent cross-platform compatibility record
- **Additional Packages**: `Serilog.Extensions.Logging` `9.0.2`, various sinks

### 10. Polly - Resilience Patterns
- **Package**: `Polly`
- **Latest Version**: `8.4.0`
- **Package**: `Polly.Core` `8.6.2` (newer core package)
- **.NET 10 Compatibility**: ✅ **Likely Compatible** - Targets .NET 6.0+ and .NET Standard 2.0
- **Confidence**: **High** - Framework design supports modern .NET versions
- **Features**: Retry, Circuit Breaker, Timeout, Rate-limiting, Hedging, Fallback
- **Integration**: `Microsoft.Extensions.Http.Polly` `9.0.7`

## Apply vs Not-Apply for Axon Backend

### ✅ **APPLY IMMEDIATELY**
1. **FastEndpoints 7.0.1** - Explicit .NET 10 support, perfect for REPR pattern
2. **MediatR 13.0.0** - Confirmed .NET 10 compatibility, core to CQRS architecture
3. **System.Diagnostics.Activity** - Built-in, no package needed
4. **EF Core 10.0.0-preview.2** - Official .NET 10 preview available
5. **Serilog 4.3.0** - Excellent compatibility record, structured logging

### ⚠️ **APPLY WITH MONITORING**
1. **FluentValidation 12.0.0** - Likely compatible but monitor for explicit .NET 10 TFM
2. **OpenTelemetry 1.12.0** - Strong compatibility expectation, monitor releases
3. **Polly 8.4.0/8.6.2** - Framework design supports modern .NET, very likely compatible
4. **Moq 4.20.72** - Ecosystem preparing for .NET 10, should work

### 🔄 **WAIT FOR UPDATES**
1. **xUnit 2.9.3/3.0.0** - Currently supports .NET 8+, wait for .NET 10 announcement
2. **NUnit 4.3.2** - Similar status to xUnit

## Confidence Assessment: **Medium-High**

**High Confidence**: FastEndpoints, EF Core, Serilog, System.Diagnostics.Activity, MediatR
**Medium Confidence**: FluentValidation, OpenTelemetry, Polly, Moq  
**Lower Confidence**: xUnit, NUnit (will need updates)

## Recommendation
Start with the high-confidence packages and monitor the medium-confidence ones. For testing, consider using xUnit 3.0.0 and expect .NET 10 support in upcoming releases, or temporarily target a compatible framework for tests.