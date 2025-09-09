using Axon.Modules.Identity.Domain.Enums;
using BuildingBlocks.Application.Pagination;
using Axon.Modules.Identity.Application.Specifications.AxonPrincipals;
using Axon.Modules.Identity.Application.Specifications.Wallets;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Application.Common.Specifications;

/// <summary>
/// Factory class for creating Identity module specifications.
/// Provides a clean, fluent API for building common specification combinations.
/// </summary>
public static class IdentitySpecs
{
    #region AxonPrincipal Specifications

    /// <summary>
    /// Creates a specification for active credentials (not deleted).
    /// </summary>
    public static ActiveCredentialSpec ActiveCredentials() => new();

    /// <summary>
    /// Creates a specification for credentials of a specific provider type.
    /// </summary>
    public static ProviderCredentialSpec CredentialsForProvider(ProviderType providerType) => 
        new(providerType);

    /// <summary>
    /// Creates a specification for credentials belonging to a specific principal.
    /// </summary>
    public static PrincipalCredentialSpec CredentialsForPrincipal(AxonId principalId) => 
        new(principalId);

    /// <summary>
    /// Creates a specification for a unique credential lookup.
    /// </summary>
    public static UniqueCredentialSpec UniqueCredential(ProviderType providerType, string issuer, string subject) => 
        new(providerType, issuer, subject);

    /// <summary>
    /// Creates a specification for recently seen credentials.
    /// </summary>
    public static RecentlySeenCredentialSpec RecentlySeenCredentials(DateTimeOffset cutoffDate) => 
        new(cutoffDate);

    /// <summary>
    /// Creates a specification for credentials with a specific environment ID.
    /// </summary>
    public static EnvironmentCredentialSpec CredentialsForEnvironment(string environmentId) => 
        new(environmentId);

    /// <summary>
    /// Creates a paginated specification for AxonPrincipals by provider type.
    /// </summary>
    public static AxonPrincipalsForProviderSpec PrincipalsForProvider(
        ProviderType providerType,
        Page page,
        bool includeCredentials = false) => 
        new(providerType, page, includeCredentials);

    /// <summary>
    /// Creates a count specification for AxonPrincipals by provider type.
    /// </summary>
    public static AxonPrincipalsForProviderCountSpec PrincipalsForProviderCount(ProviderType providerType) => 
        new(providerType);

    /// <summary>
    /// Creates a specification for recently created AxonPrincipals.
    /// </summary>
    public static RecentlyCreatedPrincipalsSpec RecentlyCreatedPrincipals(
        TimeSpan within,
        Page page,
        bool includeCredentials = false) => 
        new(within, page, includeCredentials);

    /// <summary>
    /// Creates a specification for AxonPrincipals that own wallets on a specific chain.
    /// </summary>
    public static WalletOwnersByChainSpec WalletOwnersByChain(
        string chainId,
        Page page,
        bool includeWalletOwnerships = false) => 
        new(chainId, page, includeWalletOwnerships);

    #endregion

    #region Wallet Specifications

    /// <summary>
    /// Creates a specification for active wallet ownerships.
    /// </summary>
    public static ActiveWalletOwnershipSpec ActiveWalletOwnerships() => new();

    /// <summary>
    /// Creates a specification for signing wallet ownerships.
    /// </summary>
    public static SigningWalletOwnershipSpec SigningWalletOwnerships() => new();

    /// <summary>
    /// Creates a specification for wallet ownerships of a specific principal.
    /// </summary>
    public static PrincipalWalletOwnershipSpec WalletOwnershipsByPrincipal(AxonId principalId) => 
        new(principalId);

    /// <summary>
    /// Creates a specification for wallet ownerships with a specific wallet ID.
    /// </summary>
    public static SpecificWalletOwnershipSpec WalletOwnership(WalletId walletId) => 
        new(walletId);

    /// <summary>
    /// Creates a specification for conflicting wallet ownerships.
    /// </summary>
    public static ConflictingWalletOwnershipSpec ConflictingWalletOwnerships() => new();

    /// <summary>
    /// Creates a paginated specification for wallets by chain.
    /// </summary>
    public static WalletsForChainSpec WalletsByChain(
        string chainId,
        Page page) => 
        new(chainId, page);

    /// <summary>
    /// Creates a count specification for wallets by chain.
    /// </summary>
    public static WalletsForChainCountSpec WalletsByChainCount(string chainId) => 
        new(chainId);

    #endregion
}