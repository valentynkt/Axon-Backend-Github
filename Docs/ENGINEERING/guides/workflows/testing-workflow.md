# Testing Workflow

**Running tests, coverage, test organization, and debugging test failures.**

---

**STATUS**: 🚧 Draft - AI Content Generation Ready
**PRIORITY**: Medium
**LAST_UPDATED**: 2025-09-29

---

## Overview

This guide covers the testing strategy, test organization, running tests, coverage analysis, and debugging test failures in Axon Backend.

---

## Test Organization

### Test Project Structure

```
tests/
├── Modules/
│   ├── Identity/
│   │   ├── Domain/                    # Domain logic tests (pure unit tests)
│   │   ├── Application/               # Application layer tests (with mocks)
│   │   ├── Infrastructure/            # Infrastructure tests (with TestContainers)
│   │   ├── Integration/               # Full module integration tests
│   │   └── E2E/                       # End-to-end API tests
│   │
│   └── Chat/
│       ├── Domain/
│       ├── Application/
│       ├── Infrastructure/
│       ├── Integration/
│       ├── E2E/
│       └── Performance/               # Performance/load tests
│
├── Api/                               # API-level tests
└── BuildingBlocks/
    ├── Infrastructure/                # Shared infrastructure tests
    └── Testing/                       # Test helpers and utilities
```

### Test Categories

**Domain Tests** (`Domain/`)
- Pure unit tests
- No external dependencies
- Test business logic, invariants, domain events
- Fast execution (< 1ms per test)

**Application Tests** (`Application/`)
- Command/query handler tests
- Use mocks for repositories/services
- Test application workflows
- Medium speed (1-10ms per test)

**Infrastructure Tests** (`Infrastructure/`)
- Repository, service, adapter tests
- Use TestContainers for real databases
- Test data access, external integrations
- Slower (10-100ms per test)

**Integration Tests** (`Integration/`)
- Full module integration
- Real database, real services
- Test module boundaries, consistency
- Slow (100-500ms per test)

**E2E Tests** (`E2E/`)
- Full API tests
- Test complete user journeys
- Real HTTP requests, real database
- Slowest (500ms-2s per test)

---

## Running Tests

### Run All Tests

```bash
# Run entire test suite
dotnet test

# Run with verbose output
dotnet test --logger:"console;verbosity=detailed"

# Run in parallel (faster)
dotnet test --parallel
```

### Run by Category

```bash
# Domain tests only (fast)
dotnet test tests/Modules/Identity/Domain/

# Application tests
dotnet test tests/Modules/Identity/Application/

# Infrastructure tests (uses TestContainers)
dotnet test tests/Modules/Identity/Infrastructure/

# Integration tests
dotnet test tests/Modules/Identity/Integration/

# E2E tests
dotnet test tests/Modules/Identity/E2E/
```

### Run Specific Tests

```bash
# Run single test class
dotnet test --filter ClassName~AxonPrincipalTests

# Run single test method
dotnet test --filter FullyQualifiedName~AxonPrincipalTests.LinkWallet_ValidAddress_ReturnsSuccess

# Run tests matching pattern
dotnet test --filter Name~LinkWallet

# Run tests by category (if using [Category] attribute)
dotnet test --filter Category=Domain
dotnet test --filter "Category!=E2E"  # Exclude E2E tests
```

### Watch Mode

```bash
# Watch and re-run tests on file changes
dotnet watch test tests/Modules/Identity/Domain/

# Watch specific test
dotnet watch test --filter ClassName~AxonPrincipalTests
```

---

## Test Coverage

### Generate Coverage Report

```bash
# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Coverage reports generated in:
# tests/**/TestResults/{guid}/coverage.cobertura.xml
```

### View Coverage Report

```bash
# Install reportgenerator (one-time)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate HTML report
reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"coverage" \
  -reporttypes:"Html"

# Open in browser
open coverage/index.html  # macOS
xdg-open coverage/index.html  # Linux
start coverage/index.html  # Windows
```

### Coverage Goals

**Target Coverage:**
- Domain layer: 100% (business logic)
- Application layer: 90%+ (handlers, validators)
- Infrastructure layer: 80%+ (repositories, services)
- API layer: 70%+ (endpoints)

**What to Cover:**
- ✅ Business rules and invariants
- ✅ Domain events
- ✅ Command/query handlers
- ✅ Validation logic
- ✅ Error scenarios
- ✅ Edge cases

**What NOT to Cover:**
- ❌ DTOs (data transfer objects)
- ❌ Auto-generated code
- ❌ Simple property getters/setters
- ❌ Configuration code

---

## Writing Tests

### Domain Test Example

```csharp
namespace Axon.Modules.Identity.Domain.Tests;

[TestFixture]
public class AxonPrincipalTests
{
    [Test]
    public void LinkWallet_ValidAddress_ReturnsSuccess()
    {
        // Arrange
        var principal = AxonPrincipal.Create(
            new EnvironmentId(Guid.NewGuid()),
            PrincipalType.Human);

        var walletAddress = new Address("0x1234...");
        var chainId = ChainId.Solana;

        // Act
        var result = principal.LinkWallet(
            walletAddress,
            chainId,
            ProofType.SignedMessage,
            "proof-data");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        principal.Wallets.ShouldContain(w =>
            w.Address == walletAddress && w.ChainId == chainId);
    }

    [Test]
    public void LinkWallet_DuplicateAddress_ReturnsFailure()
    {
        // Arrange
        var principal = AxonPrincipal.Create(
            new EnvironmentId(Guid.NewGuid()),
            PrincipalType.Human);

        var walletAddress = new Address("0x1234...");
        principal.LinkWallet(walletAddress, ChainId.Solana, ProofType.SignedMessage, "proof");

        // Act
        var result = principal.LinkWallet(
            walletAddress,
            ChainId.Solana,
            ProofType.SignedMessage,
            "proof");

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.BusinessRule);
        result.Error.Message.ShouldContain("already linked");
    }
}
```

### Application Test Example (with Mocks)

```csharp
namespace Axon.Modules.Identity.Application.Tests;

[TestFixture]
public class VerifyWalletCommandHandlerTests
{
    private IAxonPrincipalWriteRepository _repository;
    private IWalletVerificationService _verificationService;
    private VerifyWalletCommandHandler _handler;

    [SetUp]
    public void Setup()
    {
        _repository = Substitute.For<IAxonPrincipalWriteRepository>();
        _verificationService = Substitute.For<IWalletVerificationService>();
        _handler = new VerifyWalletCommandHandler(_repository, _verificationService);
    }

    [Test]
    public async Task Handle_ValidSignature_ReturnsSuccess()
    {
        // Arrange
        var principal = AxonPrincipal.Create(new EnvironmentId(Guid.NewGuid()), PrincipalType.Human);
        var walletAddress = new Address("0x1234...");

        _repository.GetByIdAsync(principal.Id, default)
            .Returns(principal);

        _verificationService.VerifySignature(
            walletAddress,
            Arg.Any<string>(),
            Arg.Any<string>())
            .Returns(Result.Success<bool, Error>(true));

        var command = new VerifyWalletCommand(
            principal.Id,
            walletAddress,
            "challenge",
            "signature");

        // Act
        var result = await _handler.Handle(command, default);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await _repository.Received(1).UpdateAsync(principal, default);
    }
}
```

### Integration Test Example (with TestContainers)

```csharp
namespace Axon.Modules.Identity.Integration.Tests;

[TestFixture]
public class AxonPrincipalRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder().Build();
    private IdentityWriteDbContext _dbContext;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        await _container.StartAsync();

        var options = new DbContextOptionsBuilder<IdentityWriteDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        _dbContext = new IdentityWriteDbContext(options);
        await _dbContext.Database.MigrateAsync();
    }

    [Test]
    public async Task AddAsync_ValidPrincipal_SavesSuccessfully()
    {
        // Arrange
        var repository = new AxonPrincipalWriteRepository(_dbContext);
        var principal = AxonPrincipal.Create(
            new EnvironmentId(Guid.NewGuid()),
            PrincipalType.Human);

        // Act
        await repository.AddAsync(principal);

        // Assert
        var saved = await repository.GetByIdAsync(principal.Id);
        saved.ShouldNotBeNull();
        saved.Id.ShouldBe(principal.Id);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _dbContext.DisposeAsync();
        await _container.StopAsync();
    }
}
```

### E2E Test Example

```csharp
namespace Axon.Modules.Identity.E2E.Tests;

[TestFixture]
public class WalletVerificationFlowTests : E2ETestBase
{
    [Test]
    public async Task CompleteWalletVerificationFlow_Success()
    {
        // Arrange
        var client = CreateClient();

        // Step 1: Exchange credential (create principal)
        var exchangeResponse = await client.PostAsJsonAsync(
            "/api/v1/identity/exchange-credential",
            new { Token = "dynamic-jwt-token" });

        exchangeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var principalId = await exchangeResponse.Content.ReadFromJsonAsync<Guid>();

        // Step 2: Generate challenge
        var challengeResponse = await client.PostAsJsonAsync(
            "/api/v1/identity/generate-challenge",
            new { WalletAddress = "0x1234..." });

        challengeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var challenge = await challengeResponse.Content.ReadAsStringAsync();

        // Step 3: Verify wallet signature
        var verifyResponse = await client.PostAsJsonAsync(
            "/api/v1/identity/verify-wallet",
            new
            {
                PrincipalId = principalId,
                WalletAddress = "0x1234...",
                Challenge = challenge,
                Signature = "signed-challenge"
            });

        // Assert
        verifyResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
```

---

## Debugging Test Failures

### Running Single Test with Verbose Output

```bash
# Run single test with detailed logging
dotnet test \
  --filter FullyQualifiedName~VerifyWalletCommandHandlerTests.Handle_ValidSignature_ReturnsSuccess \
  --logger:"console;verbosity=detailed"
```

### Debug Test in IDE

**Rider:**
1. Open test file
2. Click green arrow next to test method
3. Select "Debug"
4. Set breakpoints
5. Inspect variables

**VS Code:**
1. Open test file
2. Click "Debug Test" above test method (CodeLens)
3. Set breakpoints
4. Use Debug Console

### Common Test Failures

**Problem:** `NullReferenceException` in test

**Solution:**
```csharp
// Ensure all mocks are configured
_repository = Substitute.For<IRepository>();
_repository.GetByIdAsync(Arg.Any<Guid>(), default)
    .Returns((AxonPrincipal)null!);  // ❌ Returns null

// Fix: Return actual object
_repository.GetByIdAsync(Arg.Any<Guid>(), default)
    .Returns(AxonPrincipal.Create(...));  // ✅
```

**Problem:** `TestContainers timeout`

**Solution:**
```bash
# Ensure Docker is running
docker ps

# Increase timeout
[OneTimeSetUp]
public async Task Setup()
{
    await _container.StartAsync(TimeSpan.FromMinutes(5));  // Increase timeout
}
```

**Problem:** `Test passes individually, fails in suite`

**Solution:**
```csharp
// Ensure test isolation
[SetUp]
public async Task Setup()
{
    // Clear state before each test
    await _dbContext.Database.EnsureDeletedAsync();
    await _dbContext.Database.MigrateAsync();
}
```

---

## Test Best Practices

### AAA Pattern (Arrange-Act-Assert)

```csharp
[Test]
public void TestName()
{
    // Arrange - Set up test data and dependencies
    var principal = AxonPrincipal.Create(...);

    // Act - Execute the code under test
    var result = principal.LinkWallet(...);

    // Assert - Verify the outcome
    result.IsSuccess.ShouldBeTrue();
}
```

### Test Naming Convention

```csharp
// Format: MethodName_Scenario_ExpectedBehavior
[Test]
public void LinkWallet_ValidAddress_ReturnsSuccess() { }

[Test]
public void LinkWallet_DuplicateAddress_ReturnsFailure() { }

[Test]
public void LinkWallet_NullAddress_ThrowsArgumentNullException() { }
```

### Use Shouldly for Assertions

```csharp
// ✅ Good: Shouldly (fluent, readable)
result.IsSuccess.ShouldBeTrue();
result.Value.ShouldBe(expected);
collection.ShouldContain(item);
exception.ShouldNotBeNull();

// ❌ Avoid: Assert (less readable)
Assert.IsTrue(result.IsSuccess);
Assert.AreEqual(expected, result.Value);
```

### Test Data Builders

```csharp
// Create test data builder for complex objects
public class AxonPrincipalBuilder
{
    private EnvironmentId _environmentId = new(Guid.NewGuid());
    private PrincipalType _type = PrincipalType.Human;

    public AxonPrincipalBuilder WithEnvironment(EnvironmentId id)
    {
        _environmentId = id;
        return this;
    }

    public AxonPrincipalBuilder AsService()
    {
        _type = PrincipalType.Service;
        return this;
    }

    public AxonPrincipal Build()
    {
        return AxonPrincipal.Create(_environmentId, _type);
    }
}

// Usage
var principal = new AxonPrincipalBuilder()
    .WithEnvironment(envId)
    .AsService()
    .Build();
```

---

## Performance Testing

### Benchmark Tests

```csharp
[TestFixture]
[Category("Performance")]
public class PrincipalResolutionPerformanceTests
{
    [Test]
    public async Task ResolveIdentity_1000Calls_CompletesUnder1Second()
    {
        // Arrange
        var service = CreateService();
        var stopwatch = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < 1000; i++)
        {
            await service.ResolveIdentityAsync(...);
        }
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(1000);
    }
}
```

### Load Tests

```bash
# Use k6 or similar for load testing
# See tests/Modules/Identity/Performance/ for examples
```

---

## Continuous Integration

### GitHub Actions Workflow

```yaml
# .github/workflows/test.yml
name: Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'
      - name: Restore
        run: dotnet restore
      - name: Build
        run: dotnet build --no-restore
      - name: Test
        run: dotnet test --no-build --verbosity normal --collect:"XPlat Code Coverage"
      - name: Upload Coverage
        uses: codecov/codecov-action@v3
```

---

## Related Documentation

- **Development Workflow** → [development-workflow.md](./development-workflow.md)
- **Debugging Guide** → [debugging.md](./debugging.md)
- **Testing Strategy** → [../../testing/00-INDEX.md](../../testing/00-INDEX.md)
- **Coding Standards** → [../codebase/coding-standards.md](../codebase/coding-standards.md)

---

**Last Updated**: 2025-09-29