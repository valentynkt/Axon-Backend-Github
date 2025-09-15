using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Enums;

namespace Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;

/// <summary>
/// Query methods for AxonPrincipal aggregate.
/// </summary>
public sealed partial class AxonPrincipal
{
    /// <summary>
    /// Gets the active (non-deleted) chain defaults dictionary.
    /// </summary>
    public IReadOnlyDictionary<string, WalletId> ChainDefaults =>
        _principalChainDefaults
            .Where(pcd => !pcd.IsDeleted)
            .ToDictionary(pcd => pcd.ChainId, pcd => pcd.WalletId);

    /// <summary>
    /// Checks if the principal has a specific credential.
    /// </summary>
    public bool HasCredential(string provider, string issuer, string subject)
    {
        return _credentials.Any(c =>
            c.Provider == provider &&
            c.Issuer == issuer &&
            c.Subject == subject);
    }

    /// <summary>
    /// Checks if the principal owns a specific wallet.
    /// </summary>
    public bool OwnsWallet(WalletId walletId)
    {
        return _walletOwnerships.Any(o => o.WalletId == walletId);
    }

    /// <summary>
    /// Checks if the principal has verified signing ownership of a wallet.
    /// </summary>
    public bool HasVerifiedSigningOwnership(WalletId walletId)
    {
        return _walletOwnerships.Any(o => 
            o.WalletId == walletId && 
            o.IsVerifiedSigning);
    }

    /// <summary>
    /// Gets the wallet ownership for a specific wallet.
    /// </summary>
    public WalletOwnership? GetWalletOwnership(WalletId walletId)
    {
        return _walletOwnerships.FirstOrDefault(o => o.WalletId == walletId);
    }

    /// <summary>
    /// Gets the default wallet for a specific chain.
    /// </summary>
    public WalletId? GetDefaultWalletForChain(string chainId)
    {
        return _principalChainDefaults.FirstOrDefault(pcd => pcd.ChainId == chainId)?.WalletId;
    }

    /// <summary>
    /// Gets all verified signing wallets.
    /// </summary>
    public IEnumerable<WalletOwnership> GetVerifiedSigningWallets()
    {
        return _walletOwnerships.Where(o => o.IsVerifiedSigning);
    }

    /// <summary>
    /// Checks if a wallet can be set as default for a chain.
    /// </summary>
    public bool CanSetAsDefault(WalletId walletId)
    {
        var ownership = _walletOwnerships.FirstOrDefault(o => o.WalletId == walletId);
        return ownership?.IsVerifiedSigning ?? false;
    }

    /// <summary>
    /// Gets the count of owned wallets.
    /// </summary>
    public int WalletCount => _walletOwnerships.Count;

    /// <summary>
    /// Gets the count of credentials.
    /// </summary>
    public int CredentialCount => _credentials.Count;

    /// <summary>
    /// Checks if the principal is at the maximum wallet limit.
    /// </summary>
    public bool IsAtWalletLimit => _walletOwnerships.Count >= 10;

    /// <summary>
    /// Gets all credentials for a specific provider.
    /// </summary>
    public IEnumerable<IdentityCredential> GetCredentialsForProvider(string provider)
    {
        return _credentials.Where(c => c.Provider == provider);
    }

    /// <summary>
    /// Validates if the principal can accept a new risk tier.
    /// </summary>
    public bool CanAcceptRiskTier(RiskTier riskTier)
    {
        // Service principals can only have Low (Conservative) risk tier
        if (Type == PrincipalType.Service && riskTier != RiskTier.Low)
            return false;
        
        return true;
    }
}