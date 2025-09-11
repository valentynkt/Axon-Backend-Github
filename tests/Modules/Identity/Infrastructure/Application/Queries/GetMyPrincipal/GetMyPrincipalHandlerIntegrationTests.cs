using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.DTOs.Responses;
using Axon.Modules.Identity.Application.Queries.GetMyPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Identity.Infrastructure.Persistence.Repositories;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Infrastructure.Tests.Application.Queries.GetMyPrincipal;

/// <summary>
/// Integration tests for GetMyPrincipalHandler using real EF Core database context.
/// Tests the complete query flow including ETag fingerprint generation and 304 logic.
/// </summary>
[TestFixture]
public class GetMyPrincipalHandlerIntegrationTests
{
    private ServiceProvider _serviceProvider = null!;
    private IdentityReadDbContext _readContext = null!;
    private IdentityWriteDbContext _writeContext = null!;
    private AxonPrincipalReadRepository _readRepository = null!;
    private WalletReadRepository _walletReadRepository = null!;
    private AxonPrincipalWriteRepository _writeRepository = null!;
    private WalletWriteRepository _walletWriteRepository = null!;
    private ICurrentUserService _currentUserService = null!;
    private GetMyPrincipalHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        
        // Add EF Core with InMemory database for testing
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<IdentityReadDbContext>(options =>
            options.UseInMemoryDatabase(dbName)
                   .UseSnakeCaseNamingConvention());

        services.AddDbContext<IdentityWriteDbContext>(options =>
            options.UseInMemoryDatabase(dbName)
                   .UseSnakeCaseNamingConvention());

        // Add repositories
        services.AddScoped<AxonPrincipalReadRepository>();
        services.AddScoped<AxonPrincipalWriteRepository>();
        services.AddScoped<WalletReadRepository>();
        services.AddScoped<WalletWriteRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _readContext = _serviceProvider.GetRequiredService<IdentityReadDbContext>();
        _writeContext = _serviceProvider.GetRequiredService<IdentityWriteDbContext>();
        _readRepository = _serviceProvider.GetRequiredService<AxonPrincipalReadRepository>();
        _walletReadRepository = _serviceProvider.GetRequiredService<WalletReadRepository>();
        _writeRepository = _serviceProvider.GetRequiredService<AxonPrincipalWriteRepository>();
        _walletWriteRepository = _serviceProvider.GetRequiredService<WalletWriteRepository>();

        // Mock current user service
        _currentUserService = Substitute.For<ICurrentUserService>();

        _handler = new GetMyPrincipalHandler(
            _currentUserService,
            _readRepository,
            _walletReadRepository);

        // Ensure databases are created with schema
        _readContext.Database.EnsureCreated();
        _writeContext.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
        _readRepository?.Dispose();
        _walletReadRepository?.Dispose();
        _writeRepository?.Dispose();
        _walletWriteRepository?.Dispose();
        _readContext?.Dispose();
        _writeContext?.Dispose();
        _serviceProvider?.Dispose();
    }

    [Test]
    public async Task Handle_ShouldReturnNotFound_WhenPrincipalDoesNotExist()
    {
        // Arrange
        var query = new GetMyPrincipalQuery(
            ProviderType.From("dynamic"),
            "https://issuer.example.com",
            "nonexistent-user",
            null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Message.ShouldBe("Principal not found. User may not have completed exchange yet.");
    }

    [Test]
    public async Task Handle_ShouldReturnCurrentUser_WhenPrincipalExists()
    {
        // Arrange
        var axonId = AxonId.New();
        var principal = await CreateAndSaveTestPrincipal(axonId);
        await AddTestCredentialToPrincipal(principal, "dynamic", "https://issuer.example.com", "test-user");

        var query = new GetMyPrincipalQuery(
            ProviderType.From("dynamic"),
            "https://issuer.example.com",
            "test-user",
            null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Profile.AxonId.ShouldBe(axonId.Value.ToString());
        result.Value.Profile.RiskTier.ShouldBe("low");
        result.Value.Wallets.ShouldBeEmpty();
        result.Value.ChainDefaults.ShouldBeEmpty();
    }

    [Test]
    public async Task Handle_ShouldReturnCurrentUserWithWallets_WhenPrincipalHasVerifiedWallets()
    {
        // Arrange
        var axonId = AxonId.New();
        var walletId = WalletId.New();
        
        // Create and save wallet
        var wallet = await CreateAndSaveTestWallet(walletId, "ethereum");
        
        // Create and save principal with wallet ownership
        var principal = await CreateAndSaveTestPrincipal(axonId);
        await AddTestCredentialToPrincipal(principal, "dynamic", "https://issuer.example.com", "test-user");
        await AddWalletOwnershipToPrincipal(principal, walletId, AccessMode.Signing);

        var query = new GetMyPrincipalQuery(
            ProviderType.From("dynamic"),
            "https://issuer.example.com",
            "test-user",
            null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.Profile.AxonId.ShouldBe(axonId.Value.ToString());
        result.Value.Wallets.ShouldHaveCount(1);
        
        var walletInfo = result.Value.Wallets.First();
        walletInfo.WalletId.ShouldBe(walletId.Value.ToString());
        walletInfo.ChainId.ShouldBe("ethereum");
        walletInfo.AccessMode.ShouldBe("signing");
        walletInfo.IsVerified.ShouldBeTrue();
    }

    [Test]
    public async Task Handle_ShouldGenerate_DeterministicETag()
    {
        // Arrange
        var axonId = AxonId.New();
        var principal = await CreateAndSaveTestPrincipal(axonId);
        await AddTestCredentialToPrincipal(principal, "dynamic", "https://issuer.example.com", "test-user");

        var query = new GetMyPrincipalQuery(
            ProviderType.From("dynamic"),
            "https://issuer.example.com",
            "test-user",
            null);

        // Act
        var result1 = await _handler.Handle(query, CancellationToken.None);
        var result2 = await _handler.Handle(query, CancellationToken.None);

        // Assert - ETags should be deterministic
        result1.IsSuccess.ShouldBeTrue();
        result2.IsSuccess.ShouldBeTrue();
        
        // ETag should be consistent for same data
        var etag1 = await _readRepository.GetPrincipalFingerprintAsync(axonId);
        var etag2 = await _readRepository.GetPrincipalFingerprintAsync(axonId);
        etag1.ShouldBe(etag2);
        etag1.ShouldNotBeEmpty();
    }

    [Test]
    public async Task Handle_ShouldReturnNotModified_WhenETagMatches()
    {
        // Arrange
        var axonId = AxonId.New();
        var principal = await CreateAndSaveTestPrincipal(axonId);
        await AddTestCredentialToPrincipal(principal, "dynamic", "https://issuer.example.com", "test-user");

        // Get the current ETag
        var currentETag = await _readRepository.GetPrincipalFingerprintAsync(axonId);

        var query = new GetMyPrincipalQuery(
            ProviderType.From("dynamic"),
            "https://issuer.example.com",
            "test-user",
            currentETag);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("NOT_MODIFIED");
        result.Error.Metadata.ShouldContainKey("ETag");
        result.Error.Metadata["ETag"].ShouldBe(currentETag);
        result.Error.Metadata.ShouldContainKey("IsNotModified");
        result.Error.Metadata["IsNotModified"].ShouldBe(true);
    }

    [Test]
    public async Task Handle_ShouldHandleETagComparison_CaseInsensitive()
    {
        // Arrange
        var axonId = AxonId.New();
        var principal = await CreateAndSaveTestPrincipal(axonId);
        await AddTestCredentialToPrincipal(principal, "dynamic", "https://issuer.example.com", "test-user");

        var currentETag = await _readRepository.GetPrincipalFingerprintAsync(axonId);
        var upperCaseETag = currentETag.ToUpperInvariant();

        var query = new GetMyPrincipalQuery(
            ProviderType.From("dynamic"),
            "https://issuer.example.com",
            "test-user",
            upperCaseETag);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe("NOT_MODIFIED");
    }

    private async Task<AxonPrincipal> CreateAndSaveTestPrincipal(AxonId axonId)
    {
        var principalResult = AxonPrincipal.CreateHumanPrincipal();
        var principal = principalResult.Value;
        
        // Use reflection to set the ID for test purposes
        var idField = typeof(AxonPrincipal).GetField("_id", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        idField?.SetValue(principal, axonId);

        await _writeRepository.AddAsync(principal);
        await _writeContext.SaveChangesAsync();

        return principal;
    }

    private async Task<Wallet> CreateAndSaveTestWallet(WalletId walletId, string chainId)
    {
        var chainIdObj = ChainId.From(chainId);
        var address = "0x1234567890123456789012345678901234567890";
        
        var walletResult = await Wallet.RegisterAsync(
            chainIdObj,
            address,
            DateTimeOffset.UtcNow,
            async (_, _) => false, // Not existing
            TimeProvider.System);

        var wallet = walletResult.Value;
        
        // Use reflection to set the ID for test purposes
        var idField = typeof(Wallet).GetField("_id", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        idField?.SetValue(wallet, walletId);

        await _walletWriteRepository.AddAsync(wallet);
        await _writeContext.SaveChangesAsync();

        return wallet;
    }

    private async Task AddTestCredentialToPrincipal(AxonPrincipal principal, string provider, string issuer, string subject)
    {
        var credentialResult = principal.AddCredential(
            ProviderType.From(provider),
            issuer,
            subject,
            TimeProvider.System);

        credentialResult.IsSuccess.ShouldBeTrue();
        
        await _writeContext.SaveChangesAsync();
    }

    private async Task AddWalletOwnershipToPrincipal(AxonPrincipal principal, WalletId walletId, AccessMode accessMode)
    {
        var linkResult = principal.LinkWallet(
            walletId,
            ChainId.From("ethereum"),
            ProofType.DynamicVerified,
            accessMode,
            "Test Wallet",
            TimeProvider.System);

        linkResult.IsSuccess.ShouldBeTrue();
        
        await _writeContext.SaveChangesAsync();
    }
}