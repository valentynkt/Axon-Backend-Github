using System.Security.Claims;
using Axon.Modules.Identity.Application.Contracts.ExternalServices;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Application.Providers;
using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Identity.Application.Tests.Providers._TestInfrastructure;

/// <summary>
/// Base class for DynamicAuthenticationProvider tests.
/// Provides common mock setup, test data builders, and helper assertions.
/// </summary>
public abstract class DynamicProviderTestBase
{
    // Core mocks
    protected IDynamicAuthService DynamicAuthService { get; set; } = null!;
    protected IAxonPrincipalWriteRepository PrincipalRepo { get; set; } = null!;
    protected IWalletWriteRepository WalletRepo { get; set; } = null!;
    protected IPrincipalResolutionService ResolutionService { get; set; } = null!;
    protected IAddressNormalizationService AddressNormalizer { get; set; } = null!;
    protected IWalletVerificationService WalletVerificationService { get; set; } = null!;
    protected IMemoryCache MemoryCache { get; set; } = null!;
    protected IHttpContextAccessor HttpContextAccessor { get; set; } = null!;
    protected UserManager<AxonUserAuth> UserManager { get; set; } = null!;
    protected ILogger<DynamicAuthenticationProvider> Logger { get; set; } = null!;

    // System under test
    protected DynamicAuthenticationProvider Provider { get; set; } = null!;

    [SetUp]
    public virtual void SetUp()
    {
        // Initialize all mocks
        DynamicAuthService = Substitute.For<IDynamicAuthService>();
        PrincipalRepo = Substitute.For<IAxonPrincipalWriteRepository>();
        WalletRepo = Substitute.For<IWalletWriteRepository>();
        ResolutionService = Substitute.For<IPrincipalResolutionService>();
        AddressNormalizer = Substitute.For<IAddressNormalizationService>();
        WalletVerificationService = Substitute.For<IWalletVerificationService>();
        MemoryCache = Substitute.For<IMemoryCache>();
        HttpContextAccessor = Substitute.For<IHttpContextAccessor>();
        Logger = Substitute.For<ILogger<DynamicAuthenticationProvider>>();

        // Mock UserManager (requires complex setup)
        UserManager = MockUserManager();

        // Configure default behaviors
        ConfigureDefaultMockBehaviors();

        // Create provider instance
        Provider = new DynamicAuthenticationProvider(
            DynamicAuthService,
            PrincipalRepo,
            WalletRepo,
            ResolutionService,
            AddressNormalizer,
            WalletVerificationService,
            MemoryCache,
            HttpContextAccessor,
            UserManager,
            Logger);
    }

    [TearDown]
    public virtual void TearDown()
    {
        // Cleanup if needed
    }

    #region Mock Setup Helpers

    protected virtual void ConfigureDefaultMockBehaviors()
    {
        // Default: Address normalization succeeds
        AddressNormalizer.NormalizeAddress(Arg.Any<string>(), Arg.Any<string>())
            .Returns(call =>
            {
                var address = call.ArgAt<string>(1);
                var addressResult = Address.Create(address);
                return addressResult;
            });

        // Default: Unit of work returns mocked save changes
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(1));
        PrincipalRepo.UnitOfWork.Returns(unitOfWork);

        // Default: UserManager GetUsersForClaimAsync returns empty
        UserManager.GetUsersForClaimAsync(Arg.Any<Claim>())
            .Returns(Task.FromResult<IList<AxonUserAuth>>(new List<AxonUserAuth>()));

        // Default: UserManager CreateAsync succeeds
        UserManager.CreateAsync(Arg.Any<AxonUserAuth>())
            .Returns(IdentityResult.Success);

        // Default: UserManager AddClaimAsync succeeds
        UserManager.AddClaimAsync(Arg.Any<AxonUserAuth>(), Arg.Any<Claim>())
            .Returns(IdentityResult.Success);

        // Default: UserManager UpdateAsync succeeds
        UserManager.UpdateAsync(Arg.Any<AxonUserAuth>())
            .Returns(IdentityResult.Success);

        // Default: Memory cache set succeeds (no-op for tests)
        object? cacheEntry;
        MemoryCache.TryGetValue(Arg.Any<object>(), out cacheEntry)
            .Returns(false);
    }

    protected static UserManager<AxonUserAuth> MockUserManager()
    {
        var store = Substitute.For<IUserStore<AxonUserAuth>>();
        var userManager = Substitute.For<UserManager<AxonUserAuth>>(
            store,
            null, null, null, null, null, null, null, null);
        return userManager;
    }

    #endregion

    #region Test Data Builders

    /// <summary>
    /// Creates a mock DynamicUserData for testing
    /// </summary>
    protected static DynamicUserData CreateDynamicUserData(
        string? axonUserId = null,
        string? email = null,
        string? environmentId = null,
        List<WalletData>? wallets = null,
        bool isNewUser = false)
    {
        return new DynamicUserData(
            AxonUserId: axonUserId ?? Guid.NewGuid().ToString(),
            Email: email ?? "test@example.com",
            EnvironmentId: environmentId ?? "test-env-123",
            Wallets: wallets ?? new List<WalletData>(),
            FirstVisitUtc: DateTimeOffset.UtcNow.AddDays(-7),
            LastVisitUtc: DateTimeOffset.UtcNow,
            IsNewUser: isNewUser);
    }

    /// <summary>
    /// Creates a mock WalletData for testing
    /// </summary>
    protected static WalletData CreateWalletData(
        string? address = null,
        string? chain = null,
        string? walletName = null,
        string? provider = null)
    {
        return new WalletData(
            Id: Guid.NewGuid().ToString(),
            Address: address ?? "0x1234567890123456789012345678901234567890",
            Chain: chain ?? "evm-1",
            WalletName: walletName ?? "MetaMask",
            Provider: provider ?? "metamask",
            ConnectedAtUtc: DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Creates an AxonPrincipal with Dynamic credential
    /// </summary>
    protected static AxonPrincipal CreatePrincipalWithDynamicCredential(
        ProviderType? providerType = null,
        string? issuer = null,
        string? subject = null)
    {
        var provider = providerType ?? ProviderType.Create("dynamic").Value;
        var iss = issuer ?? "https://app.dynamic.xyz/test-env";
        var sub = subject ?? Guid.NewGuid().ToString();

        var result = AxonPrincipal.CreateWithDynamicCredential(provider, iss, sub);
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    /// <summary>
    /// Creates a Wallet entity for testing
    /// </summary>
    protected Wallet CreateWallet(
        ChainId? chainId = null,
        Address? address = null)
    {
        var chain = chainId ?? ChainId.Create("evm-1").Value;
        var addr = address ?? Address.Create("0x1234567890123456789012345678901234567890").Value;

        var result = Wallet.Create(chain, addr);
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    /// <summary>
    /// Creates a WalletOwnership entity for testing
    /// </summary>
    protected WalletOwnership CreateWalletOwnership(
        WalletId walletId,
        AxonUserId principalId,
        AccessMode accessMode = AccessMode.Signing,
        OwnershipStatus status = OwnershipStatus.Verified,
        VerificationSource source = VerificationSource.DynamicAttested)
    {
        var result = WalletOwnership.Create(
            walletId,
            principalId,
            accessMode,
            status,
            source,
            DateTimeOffset.UtcNow);

        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    /// <summary>
    /// Creates an AxonUserAuth (Identity user) for testing
    /// </summary>
    protected AxonUserAuth CreateAxonUserAuth(
        AxonUserId? principalId = null,
        string? dynamicUserId = null,
        string? email = null)
    {
        var user = AxonUserAuth.Create(
            principalId: principalId ?? AxonUserId.Create(),
            providerType: "dynamic",
            issuer: "https://app.dynamic.xyz",
            subject: dynamicUserId ?? Guid.NewGuid().ToString(),
            dynamicEnvironmentId: "test-env-123",
            dynamicUserId: dynamicUserId ?? Guid.NewGuid().ToString());

        if (email != null)
        {
            user.Email = email;
            user.NormalizedEmail = email.ToUpperInvariant();
        }

        return user;
    }

    #endregion

    #region Helper Assertions

    /// <summary>
    /// Asserts that a principal was created with the expected credential
    /// </summary>
    protected static void AssertPrincipalHasCredential(
        AxonPrincipal principal,
        string provider,
        string issuer,
        string subject)
    {
        principal.Credentials.ShouldNotBeEmpty();
        var credential = principal.Credentials.FirstOrDefault(c =>
            c.Provider == provider &&
            c.Issuer == issuer &&
            c.Subject == subject);
        credential.ShouldNotBeNull();
    }

    /// <summary>
    /// Asserts that a principal has wallet ownership linked
    /// </summary>
    protected static void AssertPrincipalHasWalletOwnership(
        AxonPrincipal principal,
        WalletId walletId,
        AccessMode expectedMode = AccessMode.Signing)
    {
        principal.WalletOwnerships.ShouldNotBeEmpty();
        var ownership = principal.WalletOwnerships.FirstOrDefault(wo => wo.WalletId == walletId);
        ownership.ShouldNotBeNull();
        ownership.AccessMode.ShouldBe(expectedMode);
    }

    /// <summary>
    /// Asserts that repository was called to add principal
    /// </summary>
    protected void AssertPrincipalWasAdded()
    {
        PrincipalRepo.Received(1).AddAsync(Arg.Any<AxonPrincipal>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Asserts that repository was called to update principal
    /// </summary>
    protected void AssertPrincipalWasUpdated()
    {
        PrincipalRepo.Received(1).UpdateAsync(Arg.Any<AxonPrincipal>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Asserts that changes were saved to database
    /// </summary>
    protected void AssertChangesSaved()
    {
        PrincipalRepo.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    #endregion
}