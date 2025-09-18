using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Identity.Domain.Tests.TestData;

/// <summary>
/// Test data builders for Identity Domain entities matching story 1.1 implementation.
/// </summary>
public static class Builders
{
    // Enum values
    public static RiskTier LowRiskTier => RiskTier.Low;
    public static RiskTier MediumRiskTier => RiskTier.Medium;
    public static RiskTier HighRiskTier => RiskTier.High;
    
    public static PrincipalType HumanPrincipal => PrincipalType.Human;
    public static PrincipalType ServicePrincipal => PrincipalType.Service;
    
    public static AccessMode SigningAccess => AccessMode.Signing;
    public static AccessMode WatchOnlyAccess => AccessMode.WatchOnly;
    
    public static OwnershipStatus PendingStatus => OwnershipStatus.Pending;
    public static OwnershipStatus VerifiedStatus => OwnershipStatus.Verified;
    public static OwnershipStatus RevokedStatus => OwnershipStatus.Revoked;

    // Test constants
    public static string DynamicProvider => "dynamic";
    public static string SolanaChain => "solana-mainnet";
    public static string EthereumChain => "ethereum-mainnet";
    
    public static Address SolanaAddress => Address.From(TestConstants.ValidSolanaAddress);
    public static Address EthereumAddress => Address.From(TestConstants.ValidEthAddress);

    // Entity builders
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
            timestamp
        );
    }

    public static WalletOwnership CreateWalletOwnership(
        AxonUserId? principalId = null,
        WalletId? walletId = null,
        AccessMode accessMode = AccessMode.Signing,
        OwnershipStatus status = OwnershipStatus.Pending)
    {
        return WalletOwnership.Create(
            principalId ?? AxonUserId.New(),
            walletId ?? WalletId.New(),
            accessMode,
            status
        );
    }

    public static PrincipalChainDefault CreatePrincipalChainDefault(
        AxonUserId? principalId = null,
        string chainId = "solana-mainnet",
        WalletId? walletId = null)
    {
        return PrincipalChainDefault.Create(
            principalId ?? AxonUserId.New(),
            chainId,
            walletId ?? WalletId.New()
        );
    }
}