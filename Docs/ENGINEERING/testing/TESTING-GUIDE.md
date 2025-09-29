# Testing Guide

**Comprehensive guide to testing practices in Axon Backend.**

---

## Overview

Axon follows a **test pyramid** approach with emphasis on:
- **Domain tests** (50%) - Pure business logic, no I/O
- **Integration tests** (40%) - Database, external services
- **E2E tests** (10%) - Critical user flows

**Test Framework**: NUnit + Shouldly assertions + NSubstitute mocking + Testcontainers

---

## Test Organization

```
tests/
├── BuildingBlocks/
│   ├── Core/              # Core pattern tests
│   ├── Application/       # CQRS behavior tests
│   └── Infrastructure/    # Repository, EF Core tests
└── Modules/
    ├── Identity/
    │   ├── Domain/        # Aggregate invariants
    │   ├── Application/   # Command/query handlers
    │   └── Infrastructure/ # Persistence, concurrency
    └── Chat/
        ├── Domain/
        ├── Application/
        └── Infrastructure/
```

---

## Test Naming Convention

```csharp
[Test]
public async Task {Method}_{Scenario}_Should{ExpectedBehavior}()
{
    // Example:
    // UpdateAsync_ConcurrentModifications_ShouldThrowConcurrencyException
    // LinkWallet_WhenAlreadyLinked_ShouldReturnSuccess  (idempotency)
    // CreatePrincipal_WithInvalidRiskTier_ShouldReturnValidationError
}
```

---

## AAA Pattern (Arrange-Act-Assert)

```csharp
[Test]
public async Task ExchangeCredential_NewPrincipal_ShouldCreatePrincipal()
{
    // Arrange
    var command = new ExchangeCredentialCommand("valid-bearer-token");
    var handler = CreateHandler();

    // Act
    var result = await handler.Handle(command, CancellationToken.None);

    // Assert
    result.IsSuccess.ShouldBeTrue();
    result.Value.UserId.ShouldNotBeNull();
    result.Value.IsNewPrincipal.ShouldBeTrue();
}
```

---

## Test Data Patterns

### 1. Test Data Fixtures (Object Mother)

**Purpose**: Pre-configured test scenarios with consistent data.

**Location**: `tests/{Module}/Application/_TestInfrastructure/Fixtures/TestDataFixtures.cs`

**Example** (Identity module):
```csharp
public static class TestDataFixtures
{
    // Constants for deterministic tests
    public const string DynamicIssuer = "https://app.dynamic.xyz/test";
    public const string DynA_Subject = "dyn_user_a_12345";
    public const string SolanaMainnetChain = "solana-mainnet";
    public const string W1MainAddress = "9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM";

    /// <summary>
    /// Creates test principal A with Dynamic credential.
    /// </summary>
    public static AxonPrincipal CreatePrincipalA()
    {
        var providerType = ProviderType.Create("dynamic").Value;
        return AxonPrincipal.CreateWithDynamicCredential(
            providerType,
            DynamicIssuer,
            DynA_Subject).Value;
    }

    /// <summary>
    /// Creates principal with verified signing wallet.
    /// </summary>
    public static AxonPrincipal CreatePrincipalWithVerifiedWallet()
    {
        var principal = CreatePrincipalA();
        var ownership = CreateVerifiedWalletOwnership(principal.Id, W1MainAddress);
        principal.LinkWalletOwnership(ownership, _ => Result.Success<bool, Error>(false));
        return principal;
    }
}
```

**Usage**:
```csharp
[Test]
public async Task GetMyPrincipal_ExistingPrincipal_ShouldReturnWithWallets()
{
    // Arrange
    var principal = TestDataFixtures.CreatePrincipalWithVerifiedWallet();
    await _repository.AddAsync(principal);

    // Act
    var result = await _handler.Handle(new GetMyPrincipalQuery(principal.Id), default);

    // Assert
    result.IsSuccess.ShouldBeTrue();
    result.Value.Wallets.Count.ShouldBe(1);
}
```

### 2. Test Data Builders (Fluent API)

**Purpose**: Flexible test data creation with explicit configuration.

**Location**: `tests/{Module}/Domain/TestData/Builders.cs`

**Example**:
```csharp
public static class Builders
{
    // Enum shortcuts
    public static RiskTier LowRiskTier => RiskTier.Low;
    public static AccessMode SigningAccess => AccessMode.Signing;
    public static OwnershipStatus VerifiedStatus => OwnershipStatus.Verified;

    // Entity builder
    public static IdentityCredential CreateIdentityCredential(
        AxonUserId? principalId = null,
        string provider = "dynamic",
        string issuer = "issuer",
        string subject = "subject",
        DateTime? timestamp = null)
    {
        return IdentityCredential.Create(
            principalId ?? AxonUserId.New(),
            provider,
            issuer,
            subject,
            timestamp ?? DateTime.UtcNow
        );
    }

    public static WalletOwnership CreateWalletOwnership(
        AxonUserId principalId,
        string address,
        AccessMode? accessMode = null,
        OwnershipStatus? status = null)
    {
        var walletId = WalletId.From(address);
        return WalletOwnership.Create(
            principalId,
            walletId,
            accessMode ?? SigningAccess,
            status ?? VerifiedStatus
        ).Value;
    }
}
```

**Usage**:
```csharp
[Test]
public void LinkWallet_DuplicateWallet_ShouldReturnSuccess()
{
    // Arrange
    var principal = TestDataFixtures.CreatePrincipalA();
    var ownership = Builders.CreateWalletOwnership(
        principal.Id,
        TestDataFixtures.W1MainAddress,
        AccessMode.Signing,
        OwnershipStatus.Verified);

    // Act - First link
    var result1 = principal.LinkWalletOwnership(ownership, _ => Result.Success<bool, Error>(false));

    // Act - Second link (idempotent)
    var result2 = principal.LinkWalletOwnership(ownership, _ => Result.Success<bool, Error>(false));

    // Assert
    result1.IsSuccess.ShouldBeTrue();
    result2.IsSuccess.ShouldBeTrue(); // Idempotent
    principal.WalletOwnerships.Count.ShouldBe(1);
}
```

---

## Testing Patterns by Layer

### Domain Layer Tests

**Focus**: Business rules, invariants, state transitions

**No I/O**: Pure in-memory tests, no database/network

**Example**:
```csharp
[TestFixture]
public class AxonPrincipalTests
{
    [Test]
    public void UpdateRiskTier_ServicePrincipalHighRisk_ShouldReturnError()
    {
        // Arrange
        var principal = AxonPrincipal.CreateService();

        // Act
        var result = principal.UpdateRiskTier(RiskTier.High);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("PRINCIPAL.INVALID_RISK_FOR_SERVICE");
    }

    [Test]
    public void SetChainDefault_WalletNotVerified_ShouldReturnError()
    {
        // Arrange
        var principal = TestDataFixtures.CreatePrincipalA();
        var pendingOwnership = Builders.CreateWalletOwnership(
            principal.Id,
            TestDataFixtures.W1MainAddress,
            status: OwnershipStatus.Pending);
        principal.LinkWalletOwnership(pendingOwnership, _ => Result.Success<bool, Error>(false));

        // Act
        var result = principal.SetChainDefault("solana-mainnet", pendingOwnership.WalletId);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("PRINCIPAL.WALLET_NOT_VERIFIED");
    }
}
```

### Application Layer Tests

**Focus**: Command/query handlers, orchestration, validation

**Dependencies**: In-memory or mocked repositories

**Example**:
```csharp
[TestFixture]
public class ExchangeCredentialHandlerTests : ApplicationTestBase
{
    private ExchangeCredentialHandler _handler;
    private IAxonPrincipalWriteRepository _repository;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<IAxonPrincipalWriteRepository>();
        _handler = new ExchangeCredentialHandler(_repository, ...);
    }

    [Test]
    public async Task Handle_NewPrincipal_ShouldCreateAndReturnSuccess()
    {
        // Arrange
        var command = new ExchangeCredentialCommand("bearer-token");
        _repository.GetByCredentialAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns((AxonPrincipal?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await _repository.Received(1).AddAsync(Arg.Any<AxonPrincipal>(), Arg.Any<CancellationToken>());
    }
}
```

### Infrastructure Layer Tests

**Focus**: EF Core configurations, repositories, concurrency, database invariants

**Database**: Real PostgreSQL via Testcontainers

**Example**:
```csharp
[TestFixture]
public class AxonPrincipalRepositoryTests : PostgreSqlTestBase
{
    private IAxonPrincipalWriteRepository _repository;
    private IdentityWriteDbContext _context;

    [SetUp]
    public async Task SetUp()
    {
        _context = await CreateDbContextAsync();
        _repository = new AxonPrincipalWriteRepository(_context);
    }

    [Test]
    public async Task GetByIdAsync_ExistingPrincipal_ShouldLoadOwnedEntities()
    {
        // Arrange
        var principal = TestDataFixtures.CreatePrincipalWithVerifiedWallet();
        await _repository.AddAsync(principal);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var loaded = await _repository.GetByIdAsync(principal.Id);

        // Assert
        loaded.ShouldNotBeNull();
        loaded.WalletOwnerships.Count.ShouldBe(1);
        loaded.Credentials.Count.ShouldBe(1);
    }
}
```

---

## Testing Result<T, Error> Pattern

```csharp
// Success case
result.IsSuccess.ShouldBeTrue();
result.Value.ShouldBe(expectedValue);

// Failure case
result.IsFailure.ShouldBeTrue();
result.Error.Code.ShouldBe("EXPECTED.ERROR_CODE");
result.Error.Message.ShouldContain("expected message");

// Pattern matching
var output = result.Match(
    onSuccess: value => $"Success: {value}",
    onFailure: error => $"Error: {error.Message}");
```

---

## Mocking with NSubstitute

```csharp
// Setup return value
_repository.GetByIdAsync(Arg.Any<AxonUserId>())
    .Returns(TestDataFixtures.CreatePrincipalA());

// Setup async return
_service.ValidateAsync(Arg.Any<string>())
    .Returns(Task.FromResult(Result.Success<bool, Error>(true)));

// Verify call
await _repository.Received(1).AddAsync(
    Arg.Any<AxonPrincipal>(),
    Arg.Any<CancellationToken>());

// Verify NOT called
await _repository.DidNotReceive().DeleteAsync(Arg.Any<AxonUserId>());

// Argument capture
AxonPrincipal capturedPrincipal = null!;
await _repository.AddAsync(
    Arg.Do<AxonPrincipal>(p => capturedPrincipal = p),
    Arg.Any<CancellationToken>());
```

---

## Database Testing with Testcontainers

```csharp
public abstract class PostgreSqlTestBase
{
    private PostgreSqlContainer? _container;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("axon_test")
            .Build();

        await _container.StartAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (_container != null)
            await _container.DisposeAsync();
    }

    protected async Task<TContext> CreateDbContextAsync<TContext>()
        where TContext : DbContext
    {
        var options = new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        var context = (TContext)Activator.CreateInstance(typeof(TContext), options)!;
        await context.Database.MigrateAsync();
        return context;
    }
}
```

---

## Best Practices

### ✅ DO
- Use fixtures for common scenarios (Object Mother)
- Use builders for flexible test data
- Test domain invariants thoroughly
- Use real database for integration tests (Testcontainers)
- Keep tests deterministic (no random data without seed)
- Test idempotency explicitly
- Use AAA pattern consistently
- Mock external dependencies, not domain logic

### ❌ DON'T
- Test implementation details
- Use in-memory database for concurrency tests
- Create builders with side effects
- Couple tests to each other (test isolation)
- Test framework code (EF Core, MediatR internals)
- Use `Thread.Sleep()` for timing (use deterministic clock)

---

## Test Data Management

### Deterministic Data
```csharp
// ✅ Good: Consistent, repeatable
public const string TestAddress = "9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM";
public static readonly DateTime TestTimestamp = new(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
```

### Randomized Data (when needed)
```csharp
// For uniqueness tests only
public static AxonUserId RandomPrincipalId() => AxonUserId.New();
public static string RandomEmail() => $"test-{Guid.NewGuid()}@example.com";
```

---

## Concurrency Testing

See [Concurrency Testing Guide](./concurrency-testing-guide.md) for comprehensive patterns on:
- Testing optimistic concurrency with separate DbContext instances
- Simulating concurrent updates
- Verifying DbUpdateConcurrencyException
- ConcurrencyTestBase infrastructure

---

## Running Tests

```bash
# All tests
dotnet test

# Specific module
dotnet test --filter "FullyQualifiedName~Identity"

# Specific test category
dotnet test --filter "Category=Integration"
dotnet test --filter "Category=Concurrency"

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

## Test Coverage Goals

- **Domain Layer**: 95%+ (business logic critical)
- **Application Layer**: 90%+ (handlers, orchestration)
- **Infrastructure Layer**: 80%+ (focus on custom code, not framework)
- **Overall Project**: 90%+

---

## Related Documentation

- [Concurrency Testing Guide](./concurrency-testing-guide.md) - Specialized concurrency patterns
- [Identity Testing Strategy](../modules/identity/testing-strategy.md) - Identity TDD plan
- [NUnit Implementation Guide](../../Libraries/NUnit/IMPLEMENTATION_GUIDE.md) - Test framework usage
- [Shouldly Usage Guide](../../Libraries/Shouldly/USAGE_GUIDE.md) - Assertion library

---

**Last Updated**: 2025-09-30
**Maintained By**: Axon Engineering Team