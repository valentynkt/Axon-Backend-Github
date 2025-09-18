using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.Tests.TestData;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Primitives.Ids;
using CSharpFunctionalExtensions;
using NUnit.Framework;

namespace Axon.Modules.Identity.Domain.Tests.Common;

/// <summary>
/// Base test class providing common setup, teardown, and helper methods for Identity Domain tests.
/// Implements shared patterns and builders to reduce test duplication following 20/80 principle.
/// </summary>
[TestFixture]
public abstract class IdentityTestBase
{
    protected AxonPrincipal DefaultPrincipal { get; set; } = null!;
    protected WalletId DefaultWalletId { get; set; }
    protected WalletOwnership DefaultOwnership { get; set; } = null!;
    protected DateTime TestTimestamp { get; set; }

    [SetUp]
    public virtual void BaseSetUp()
    {
        TestTimestamp = DateTime.UtcNow;
        DefaultPrincipal = CreatePrincipal();
        DefaultWalletId = WalletId.New();
        DefaultOwnership = CreateOwnership();
    }

    [TearDown]
    public virtual void BaseTearDown()
    {
        // Reset any static state if needed
        DefaultPrincipal = null!;
        DefaultOwnership = null!;
    }

    #region Principal Factory Methods

    /// <summary>
    /// Creates a principal with specified type and optional custom ID.
    /// </summary>
    protected static AxonPrincipal CreatePrincipal(PrincipalType type = PrincipalType.Human, AxonUserId? customId = null)
    {
        return type switch
        {
            PrincipalType.Human => AxonPrincipal.CreateHuman(customId),
            PrincipalType.Service => AxonPrincipal.CreateService(customId),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    /// <summary>
    /// Creates a principal with specific risk tier.
    /// </summary>
    protected static AxonPrincipal CreatePrincipalWithRiskTier(RiskTier riskTier, PrincipalType type = PrincipalType.Human)
    {
        var principal = CreatePrincipal(type);
        if (riskTier != RiskTier.Low) // Default is Low
        {
            var result = principal.UpdateRiskTier(riskTier);
            if (result.IsFailure)
                throw new InvalidOperationException($"Failed to set risk tier: {result.Error}");
        }
        return principal;
    }

    #endregion

    #region Wallet Factory Methods

    /// <summary>
    /// Creates a wallet with standard test values.
    /// </summary>
    protected static Wallet CreateWallet(
        WalletId? walletId = null,
        string chainId = TestConstants.SolanaChain,
        Address? address = null,
        DateTime? timestamp = null)
    {
        return Wallet.Create(
            walletId,
            chainId,
            address ?? Builders.SolanaAddress,
            timestamp);
    }

    /// <summary>
    /// Creates multiple wallets for testing collections.
    /// </summary>
    protected static List<Wallet> CreateWallets(int count, string chainId = TestConstants.SolanaChain)
    {
        var wallets = new List<Wallet>();
        for (int i = 0; i < count; i++)
        {
            var address = chainId == TestConstants.SolanaChain 
                ? Address.From($"9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWW{i}")
                : Address.From($"0x742d35Cc6634C0532925a3b8D2aE39e7ec5B8e4{i}");
            
            wallets.Add(CreateWallet(address: address, chainId: chainId));
        }
        return wallets;
    }

    #endregion

    #region Ownership Factory Methods

    /// <summary>
    /// Creates wallet ownership with specified parameters.
    /// </summary>
    protected WalletOwnership CreateOwnership(
        AxonUserId? principalId = null,
        WalletId? walletId = null,
        AccessMode accessMode = AccessMode.Signing,
        OwnershipStatus status = OwnershipStatus.Verified)
    {
        return WalletOwnership.Create(
            principalId ?? DefaultPrincipal?.Id ?? AxonUserId.New(),
            walletId ?? DefaultWalletId,
            accessMode,
            status);
    }

    /// <summary>
    /// Creates a conflicting ownership scenario for testing invariants.
    /// </summary>
    protected static (WalletOwnership first, WalletOwnership conflicting) CreateConflictingOwnerships(
        WalletId? sharedWalletId = null,
        AccessMode mode = AccessMode.Signing,
        OwnershipStatus status = OwnershipStatus.Verified)
    {
        var walletId = sharedWalletId ?? WalletId.New();
        var firstPrincipal = CreatePrincipal();
        var secondPrincipal = CreatePrincipal();

        return (
            WalletOwnership.Create(firstPrincipal.Id, walletId, mode, status),
            WalletOwnership.Create(secondPrincipal.Id, walletId, mode, status)
        );
    }

    #endregion

    #region Credential Factory Methods

    /// <summary>
    /// Creates an identity credential with test defaults.
    /// </summary>
    protected static IdentityCredential CreateCredential(
        AxonUserId? principalId = null,
        string provider = "dynamic",
        string issuer = "test-issuer",
        string subject = "test-subject",
        DateTime? timestamp = null)
    {
        return IdentityCredential.Create(
            principalId ?? AxonUserId.New(),
            provider,
            issuer,
            subject,
            timestamp);
    }

    #endregion

    #region Conflict Resolution Helpers

    /// <summary>
    /// Creates a conflict resolution function that always returns no conflict.
    /// </summary>
    protected static Func<WalletId, AccessMode, OwnershipStatus, Result<bool, Error>> NoConflictResolver =>
        (wId, mode, status) => Result.Success<bool, Error>(false);

    /// <summary>
    /// Creates a conflict resolution function that always returns conflict.
    /// </summary>
    protected static Func<WalletId, AccessMode, OwnershipStatus, Result<bool, Error>> ConflictResolver =>
        (wId, mode, status) => Result.Success<bool, Error>(true);

    /// <summary>
    /// Creates a conflict resolution function that returns error.
    /// </summary>
    protected static Func<WalletId, AccessMode, OwnershipStatus, Result<bool, Error>> ErrorResolver =>
        (wId, mode, status) => Result.Failure<bool, Error>(Error.Failure("Resolution failed", "TEST_ERROR"));

    #endregion

    #region Common Assertions

    /// <summary>
    /// Asserts that a principal has expected default values after creation.
    /// </summary>
    protected static void AssertPrincipalDefaults(AxonPrincipal principal, PrincipalType expectedType)
    {
        Assert.Multiple(() =>
        {
            Assert.That(principal, Is.Not.Null);
            Assert.That(principal.Id, Is.Not.EqualTo(default(AxonUserId)));
            Assert.That(principal.Type, Is.EqualTo(expectedType));
            Assert.That(principal.RiskTier, Is.EqualTo(RiskTier.Low));
            Assert.That(principal.Credentials, Is.Empty);
            Assert.That(principal.WalletOwnerships, Is.Empty);
        });
    }

    /// <summary>
    /// Asserts that a wallet has expected default values after creation.
    /// </summary>
    protected static void AssertWalletDefaults(Wallet wallet, string expectedChainId, Address expectedAddress)
    {
        Assert.Multiple(() =>
        {
            Assert.That(wallet, Is.Not.Null);
            Assert.That(wallet.Id, Is.Not.EqualTo(default(WalletId)));
            Assert.That(wallet.ChainId, Is.EqualTo(expectedChainId));
            Assert.That(wallet.Address, Is.EqualTo(expectedAddress));
            Assert.That(wallet.LastSeenAt, Is.EqualTo(wallet.FirstSeenAt));
        });
    }

    #endregion
}