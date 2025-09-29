# .NET 10 Compatibility - Implementation Guide

**Comprehensive guide to .NET 10 compatibility in Axon Backend, including library versions, breaking changes, migration patterns, and testing considerations.**

---

## Overview

**Target Framework**: `net10.0` (Preview)
**SDK Version**: .NET 10 Preview 2+
**Status**: Production-ready architecture with preview runtime
**Compatibility Strategy**: Use stable library versions with .NET 10 TFM where available

---

## Current Configuration

### Directory.Build.props

```xml
<Project>
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <Nullable>enable</Nullable>
        <ImplicitUsings>enable</ImplicitUsings>
        <EnableNETAnalyzers>true</EnableNETAnalyzers>
        <AnalysisLevel>latest</AnalysisLevel>
        <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
        <TreatWarningsAsErrors Condition="'$(Configuration)' == 'Release'">true</TreatWarningsAsErrors>
    </PropertyGroup>
</Project>
```

**Key Features**:
- Nullable reference types enabled (strict null safety)
- Implicit usings for common namespaces
- Latest analyzer rules
- Warnings as errors in Release builds

---

## Library Compatibility Matrix

### ✅ Fully Compatible (Confirmed)

| Library | Version | Status | Notes |
|---------|---------|--------|-------|
| **FastEndpoints** | 7.0.1 | ✅ Confirmed | Explicitly supports net10.0 |
| **MediatR** | 13.0.0 | ✅ Confirmed | .NET 10 preview 4+ support |
| **EF Core** | 9.0.8 | ✅ Stable | Production-ready for .NET 10 |
| **System.Diagnostics.Activity** | Built-in | ✅ Framework | Core .NET 10 component |
| **ASP.NET Core Identity** | 9.0.8 | ✅ Stable | JWT Bearer auth included |

### ⚠️ Likely Compatible (High Confidence)

| Library | Version | Status | Notes |
|---------|---------|--------|-------|
| **FluentValidation** | 11.3.1 | ⚠️ Likely | .NET 10 preview 4 support mentioned |
| **Polly** | 9.0.8 | ⚠️ Likely | Targets .NET 6.0+ and .NET Standard 2.0 |
| **Moq** | 4.20.72 | ⚠️ Likely | Ecosystem compatibility indicators |
| **Swashbuckle** | 9.0.3 | ⚠️ Likely | ASP.NET Core 9.x compatible |

### 🧪 Testing Framework Compatibility

| Library | Version | Status | Notes |
|---------|---------|--------|-------|
| **NUnit** | 4.3.1 | ✅ Working | Currently supports .NET 8+, works with .NET 10 |
| **NUnit3TestAdapter** | 5.0.0 | ✅ Working | Test runner compatible |
| **Shouldly** | 4.3.0 | ✅ Working | Fluent assertions library |
| **NSubstitute** | 5.3.0 | ✅ Working | Mocking framework |
| **Moq** | 4.20.72 | ✅ Working | Alternative mocking framework |
| **FluentAssertions** | 8.5.0 | ✅ Working | Alternative assertions library |

---

## Package Configuration (Actual Axon Backend)

### API Project (Axon.Api.csproj)

```xml
<ItemGroup>
  <!-- API Framework -->
  <PackageReference Include="FastEndpoints" Version="7.0.1" />
  <PackageReference Include="FastEndpoints.Swagger" Version="7.0.1" />
  <PackageReference Include="Asp.Versioning.Http" Version="8.1.0" />
  <PackageReference Include="Swashbuckle.AspNetCore" Version="9.0.3" />

  <!-- CQRS & Validation -->
  <PackageReference Include="MediatR" Version="13.0.0" />
  <PackageReference Include="FluentValidation.AspNetCore" Version="11.3.1" />

  <!-- Authentication & Identity -->
  <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="9.0.8" />
  <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="9.0.8" />

  <!-- Data Access -->
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.8" />

  <!-- Resilience -->
  <PackageReference Include="Microsoft.Extensions.Http.Polly" Version="9.0.8" />
</ItemGroup>
```

### Test Project (Axon.Modules.Identity.Application.Tests.csproj)

```xml
<ItemGroup>
  <!-- Testing Framework -->
  <PackageReference Include="NUnit" Version="4.3.1" />
  <PackageReference Include="NUnit3TestAdapter" Version="5.0.0" />
  <PackageReference Include="NUnit.Analyzers" Version="4.3.0" />
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />

  <!-- Assertions -->
  <PackageReference Include="Shouldly" Version="4.3.0" />
  <PackageReference Include="FluentAssertions" Version="8.5.0" />

  <!-- Mocking -->
  <PackageReference Include="Moq" Version="4.20.72" />
  <PackageReference Include="NSubstitute" Version="5.3.0" />

  <!-- Code Coverage -->
  <PackageReference Include="coverlet.collector" Version="6.0.2" />

  <!-- DI for Testing -->
  <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.8" />
  <PackageReference Include="Microsoft.Extensions.Http" Version="9.0.8" />
</ItemGroup>
```

---

## Breaking Changes and Migration Notes

### MediatR 13.0.0

**Breaking Changes**:
- License key registration now required
- DI extensions now included in main package (no separate `MediatR.Extensions.Microsoft.DependencyInjection`)

**Migration**:
```csharp
// Old (MediatR 12.x)
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

// New (MediatR 13.0)
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
// + Add license key configuration
```

### FluentValidation 12.0.0 → 11.3.1

**Note**: Axon currently uses FluentValidation 11.3.1 (stable).

**If upgrading to 12.0**:
- Breaking changes in ASP.NET Core integration
- `FluentValidation.AspNetCore` versioning independent from main package post-11.1.0
- Review migration guide: https://docs.fluentvalidation.net/en/latest/upgrading-to-12.html

### EF Core 9.0.8

**No breaking changes** for typical usage patterns in Axon:
- PostgreSQL provider: `Npgsql.EntityFrameworkCore.PostgreSQL` v9.0+
- Owned entities, concurrency tokens (xmin), migrations all compatible

---

## .NET 10 Specific Features

### Enhanced Pattern Matching

```csharp
// List patterns (C# 11+, improved in .NET 10)
public bool IsValidWalletSequence(List<string> wallets) => wallets switch
{
    [] => false,                                    // Empty list
    [var single] => IsValidWallet(single),         // Single element
    [var first, .. var rest] => IsValidWallet(first) && IsValidWalletSequence(rest)
};
```

### Required Members (C# 11+)

```csharp
// Already used in Axon
public sealed class ExchangeCredentialCommand : IRequest<Result<ExchangeOutcome, Error>>
{
    public required string BearerToken { get; init; }
    public required string ProviderType { get; init; }
}
```

### File-Scoped Namespaces (C# 10+)

```csharp
// Standard in Axon codebase
namespace Axon.Modules.Identity.Application.Commands;

public sealed class ExchangeCredentialHandler { }
```

### Nullable Reference Types (C# 8+)

```csharp
// Enabled globally in Directory.Build.props
<Nullable>enable</Nullable>

// Usage
public Result<User, Error>? GetUser(string? email)
{
    if (email is null) return null;
    return Result.Success<User, Error>(new User(email));
}
```

---

## Testing Considerations

### NUnit with .NET 10

**Current Status**: ✅ Working (NUnit 4.3.1 + NUnit3TestAdapter 5.0.0)

**Test Example**:
```csharp
[TestFixture]
public sealed class ExchangeCredentialHandlerTests
{
    [Test]
    public async Task Handle_ValidToken_ShouldReturnSuccess()
    {
        // Arrange
        var handler = CreateHandler();
        var command = new ExchangeCredentialCommand
        {
            BearerToken = "valid-token",
            ProviderType = "dynamic"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }
}
```

### Shouldly Assertions

```csharp
// Fluent assertions style
result.IsSuccess.ShouldBeTrue();
result.Value.UserId.ShouldNotBeNull();
result.Error.ShouldBe(Error.Validation("Email required"));
```

### Moq / NSubstitute Mocking

```csharp
// Moq
var mockRepo = new Mock<IAxonPrincipalRepository>();
mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<AxonUserId>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(principal);

// NSubstitute
var mockRepo = Substitute.For<IAxonPrincipalRepository>();
mockRepo.GetByIdAsync(Arg.Any<AxonUserId>(), Arg.Any<CancellationToken>())
    .Returns(principal);
```

---

## Compiler and Analyzer Configuration

### Enabled Analyzers

```xml
<EnableNETAnalyzers>true</EnableNETAnalyzers>
<AnalysisLevel>latest</AnalysisLevel>
<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
```

**Benefits**:
- Latest code quality rules from Microsoft
- Performance analyzer recommendations
- Security vulnerability detection
- Nullable reference type flow analysis

### Suppressed Warnings

```xml
<NoWarn>$(NoWarn);CA2016;CA1031</NoWarn>
```

**Suppressions**:
- **CA2016**: Forward CancellationToken parameter (suppressed where not applicable)
- **CA1031**: Do not catch general exception types (suppressed for top-level handlers)

### Release Build Strictness

```xml
<TreatWarningsAsErrors Condition="'$(Configuration)' == 'Release'">true</TreatWarningsAsErrors>
```

**Behavior**:
- Development: Warnings allowed (fast iteration)
- Release: All warnings must be resolved (production quality gate)

---

## AOT and Trimming Readiness

### Signal-Only Analyzers

```xml
<EnableTrimAnalyzer Condition="'$(Configuration)' == 'Release'">true</EnableTrimAnalyzer>
<EnableSingleFileAnalyzer Condition="'$(Configuration)' == 'Release'">true</EnableSingleFileAnalyzer>
<EnableAotAnalyzer Condition="'$(Configuration)' == 'Release'">true</EnableAotAnalyzer>
```

**Purpose**:
- Early warnings for AOT-incompatible patterns
- Trimming-safe code validation
- No actual AOT compilation (signals only)

**Current Status**: Not targeting AOT deployment, but analyzer-ready

---

## Known Issues and Workarounds

### Issue 1: EF Core 10 Preview vs Production

**Problem**: EF Core 10 preview available but not used in production
**Current Approach**: Use EF Core 9.0.8 (stable) with net10.0 TFM
**Workaround**: EF Core 9.x fully supports .NET 10 runtime

```xml
<!-- Safe production configuration -->
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.0.8" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.8" />
```

### Issue 2: MediatR License Key

**Problem**: MediatR 13.0+ requires license key registration
**Solution**: Register license key in startup

```csharp
// In Program.cs or startup
MediatRLicense.RegisterLicense("YOUR_LICENSE_KEY");
```

### Issue 3: FluentValidation ASP.NET Core Integration

**Problem**: FluentValidation.AspNetCore has independent versioning post-11.1.0
**Current Version**: 11.3.1 (stable, pre-split)
**Recommendation**: Monitor for breaking changes if upgrading to 12.x

---

## Performance Considerations

### .NET 10 Runtime Improvements

**Expected Benefits**:
- Improved JIT compilation performance
- Enhanced GC (Garbage Collection) performance
- Native AOT readiness (future-proofing)
- Better SIMD (vectorization) support

### Benchmarking

```csharp
// Use BenchmarkDotNet for performance testing
[MemoryDiagnoser]
public class CommandHandlerBenchmarks
{
    [Benchmark]
    public async Task ExchangeCredential()
    {
        var handler = CreateHandler();
        var command = new ExchangeCredentialCommand
        {
            BearerToken = "test-token",
            ProviderType = "dynamic"
        };

        await handler.Handle(command, CancellationToken.None);
    }
}
```

---

## CI/CD Considerations

### GitHub Actions Configuration

```yaml
- name: Setup .NET 10
  uses: actions/setup-dotnet@v4
  with:
    dotnet-version: '10.0.x'
    dotnet-quality: 'preview'  # Required for .NET 10 preview

- name: Build
  run: dotnet build --configuration Release

- name: Test
  run: dotnet test --no-build --configuration Release
```

### Docker Support

```dockerfile
# Use .NET 10 preview SDK
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build

# Use .NET 10 preview runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
```

---

## Migration Checklist

### Migrating from .NET 8/9 to .NET 10

- [ ] Update `TargetFramework` to `net10.0` in all .csproj files
- [ ] Update SDK version in `global.json` (if present)
- [ ] Review library compatibility matrix above
- [ ] Update Docker base images to .NET 10 preview
- [ ] Update CI/CD pipeline to use .NET 10 SDK
- [ ] Run all tests and verify compatibility
- [ ] Check for compiler warnings with `AnalysisLevel>latest</AnalysisLevel>`
- [ ] Review nullable reference type warnings
- [ ] Verify EF Core migrations work correctly
- [ ] Test authentication flows (JWT, Identity)
- [ ] Validate OpenTelemetry telemetry collection
- [ ] Benchmark performance (before/after comparison)

---

## Best Practices

### ✅ DO

- Use stable library versions (9.x) with net10.0 TFM
- Enable nullable reference types globally
- Use latest analyzer rules (`AnalysisLevel>latest`)
- Treat warnings as errors in Release builds
- Monitor library changelogs for net10.0 announcements
- Test thoroughly with preview runtime
- Use `CancellationToken` in all async operations
- Follow modern C# patterns (file-scoped namespaces, records)

### ❌ DON'T

- Use experimental libraries in production
- Disable nullable warnings globally (fix them instead)
- Ignore analyzer warnings (address root causes)
- Mix .NET versions in solution (use consistent net10.0)
- Skip integration testing with .NET 10 runtime
- Deploy to production without thorough testing

---

## Related Documentation

- [Architecture Overview](../../ENGINEERING/guides/architecture/system-overview.md) - System design
- [Tech Stack](../../ENGINEERING/guides/architecture/tech-stack.md) - Technology decisions
- [Testing Guide](../../ENGINEERING/testing/TESTING-GUIDE.md) - Testing patterns
- [MediatR Guide](../MediatR/IMPLEMENTATION_GUIDE.md) - CQRS implementation
- [FastEndpoints Guide](../FastEndpoints/IMPLEMENTATION_GUIDE.md) - API endpoints

---

## External Resources

- [.NET 10 Release Notes](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview)
- [EF Core What's New](https://learn.microsoft.com/en-us/ef/core/what-is-new/)
- [C# 13 Language Features](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-13)
- [MediatR 13.0 Release](https://www.nuget.org/packages/MediatR/13.0.0)
- [FastEndpoints .NET 10 Support](https://fast-endpoints.com/)

---

**Last Updated**: 2025-09-30
**Maintained By**: Axon Engineering Team