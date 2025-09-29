# Identity Application Tests - Refactored Structure

## 📁 Organization

This test project follows a **feature-based folder structure** aligned with Domain and Infrastructure test patterns:

```
tests/Modules/Identity/Application/
├── _TestInfrastructure/          # Shared test infrastructure
│   ├── ApplicationTestBase.cs    # Base class for all tests
│   └── Fixtures/                 # Test data builders
│       ├── TestDataFixtures.cs
│       └── ResolutionTestFixtures.cs
│
├── Commands/                      # Command handler tests
│   ├── ExchangeCredential/
│   ├── RevokeCredential/         # 🚧 Placeholder
│   ├── UpdateProfile/            # 🚧 Placeholder
│   ├── LinkWallet/               # 🚧 Placeholder
│   └── UnlinkWallet/             # 🚧 Placeholder
│
├── Queries/                       # Query handler tests
│   ├── GetMyPrincipal/
│   │   ├── GetMyPrincipalHandlerTests.cs
│   │   └── GetMyPrincipalCachingTests.cs  # 🚧 Placeholder
│   └── GetPrincipalProfile/      # 🚧 Placeholder
│
├── Services/                      # Service layer tests
│   ├── PrincipalResolution/      # Resolution algorithm tests
│   │   ├── PrincipalResolutionTestBase.cs
│   │   ├── IdentityResolutionAlgorithmTests.cs
│   │   └── ResolutionFlowIntegrationTests.cs
│   ├── UserProfile/              # 🚧 Placeholder
│   ├── Authentication/           # 🚧 Placeholder
│   └── Authorization/            # 🚧 Placeholder
│
├── Validation/                    # Validator tests
│   ├── Commands/
│   │   ├── ExchangeCredentialValidatorTests.cs
│   │   ├── RevokeCredentialValidatorTests.cs    # 🚧 Placeholder
│   │   └── LinkWalletValidatorTests.cs          # 🚧 Placeholder
│   └── Queries/
│       ├── GetMyPrincipalValidatorTests.cs
│       └── GetMyPrincipalValidatorTests.cs  # 🚧 Placeholder
│
└── Integration/                   # End-to-end integration tests
    ├── CredentialExchangeFlowTests.cs        # 🚧 Placeholder
    ├── PrincipalLifecycleTests.cs            # 🚧 Placeholder
    └── ConcurrentOperationTests.cs           # 🚧 Placeholder
```

## 🎯 Testing Strategy

### Test Categories

1. **Command/Query Handler Tests** - Test CQRS handlers in isolation with mocked dependencies
2. **Service Tests** - Test business logic services (resolution, profile, auth)
3. **Validation Tests** - Test FluentValidation validators
4. **Integration Tests** - Test complete flows across multiple layers

### Naming Conventions

- **Test Classes**: `{Feature}{Type}Tests.cs` (e.g., `ExchangeCredentialHandlerTests.cs`)
- **Test Methods**: `{MethodName}_{Scenario}_{ExpectedOutcome}`
  - Example: `Handle_WithValidToken_ShouldReturnSuccess`
  - Example: `Validate_WithInvalidEmail_ShouldHaveValidationError`

### Namespace Structure

All test namespaces follow: `Axon.Modules.Identity.Application.Tests.{Category}.{Feature}`

Examples:
- `Axon.Modules.Identity.Application.Tests.Commands.ExchangeCredential`
- `Axon.Modules.Identity.Application.Tests.Services.PrincipalResolution`
- `Axon.Modules.Identity.Application.Tests.Validation.Commands`

## 🚧 Missing Test Coverage (Placeholders Created)

The following test files have been created with `[Ignore]` attribute and TODO comments:

### High Priority
1. **Service Tests**: UserProfileService, AuthenticationOrchestrator, TokenRevocation
2. **Integration Tests**: Full flow tests for credential exchange, lifecycle management
3. **Command Tests**: Wallet linking/unlinking, profile updates, credential revocation

### Future Improvements
1. **Split Large Test Files**: `IdentityResolutionAlgorithmTests.cs` should be split into:
   - `CredentialFirstResolutionTests.cs`
   - `WalletVerificationResolutionTests.cs`
   - `AmbiguityResolutionTests.cs`

2. **Test Infrastructure Enhancements**:
   - Command/Query builders in `_TestInfrastructure/Builders/`
   - Custom assertions in `_TestInfrastructure/Assertions/`
   - Service mock factories in `_TestInfrastructure/Fixtures/`

## 📝 How to Add New Tests

### 1. Command Handler Test
```bash
# Create folder and file
mkdir -p Commands/YourCommand
touch Commands/YourCommand/YourCommandHandlerTests.cs
```

### 2. Service Test
```bash
# Create folder and file
mkdir -p Services/YourService
touch Services/YourService/YourServiceTests.cs
```

### 3. Integration Test
```bash
# Add to Integration folder
touch Integration/YourFeatureFlowTests.cs
```

## 🔍 Test Discovery

All tests use NUnit's `[TestFixture]` and `[Test]` attributes. Run with:

```bash
# Run all Application tests
dotnet test tests/Modules/Identity/Application/Axon.Modules.Identity.Application.Tests.csproj

# Run specific category
dotnet test --filter "FullyQualifiedName~Commands"
dotnet test --filter "FullyQualifiedName~Services.PrincipalResolution"
```

## 📚 Resources

- [Testing Philosophy](../../../../Docs/ENGINEERING/testing/testing-philosophy.md)
- [Unit Testing Guide](../../../../Docs/ENGINEERING/testing/unit-testing-guide.md)
- [Integration Testing Guide](../../../../Docs/ENGINEERING/testing/integration-testing-guide.md)

---

**Last Updated**: 2025-09-29
**Refactoring Status**: ✅ Phase 1 Complete (Structure + Placeholders)
**Next Phase**: Implement placeholder tests + Split large files