using Axon.Modules.Identity.Domain.Entities;
using Axon.Modules.Identity.Domain.Errors;
using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;

/// <summary>
/// AxonPrincipal partial class containing query operations (read-only methods).
/// </summary>
public sealed partial class AxonPrincipal
{
    /// <summary>
    /// Validates that the principal can be accessed by checking if it's not deleted.
    /// </summary>
    public Result<Unit, Error> ValidateAccess()
    {
        if (IsDeleted)
            return Result.Failure<Unit, Error>(IdentityDomainErrors.Principal.Deleted());

        return Result.Success<Unit, Error>(Unit.Value);
    }

    /// <summary>
    /// Finds a credential by provider, issuer, and subject.
    /// </summary>
    public IdentityCredential? FindCredential(ProviderType providerType, string issuer, string subject)
    {
        return _credentials.FirstOrDefault(c => 
            !c.IsDeleted && c.Matches(providerType, issuer, subject));
    }

    /// <summary>
    /// Finds credentials by provider type. More efficient than multiple single lookups.
    /// </summary>
    public IReadOnlyCollection<IdentityCredential> FindCredentialsByProvider(ProviderType providerType)
    {
        return _credentials
            .Where(c => !c.IsDeleted && c.ProviderType == providerType)
            .ToList();
    }

    /// <summary>
    /// Finds wallet ownership by wallet ID.
    /// </summary>
    public WalletOwnership? FindWalletOwnership(WalletId walletId)
    {
        return _walletOwnerships.FirstOrDefault(w => 
            !w.IsDeleted && w.IsForWallet(walletId));
    }

    /// <summary>
    /// Finds wallet ownerships by multiple wallet IDs. More efficient than multiple single lookups.
    /// </summary>
    public IReadOnlyCollection<WalletOwnership> FindWalletOwnerships(IEnumerable<WalletId> walletIds)
    {
        var walletIdSet = walletIds.ToHashSet();
        return _walletOwnerships
            .Where(w => !w.IsDeleted && walletIdSet.Contains(w.WalletId))
            .ToList();
    }

    /// <summary>
    /// Gets active wallet ownerships (not deleted and verified).
    /// </summary>
    public IReadOnlyCollection<WalletOwnership> GetActiveWalletOwnerships()
    {
        return _walletOwnerships
            .Where(w => w.IsActive)
            .ToList();
    }

    /// <summary>
    /// Gets active credentials (not deleted).
    /// </summary>
    public IReadOnlyCollection<IdentityCredential> GetActiveCredentials()
    {
        return _credentials
            .Where(c => !c.IsDeleted)
            .ToList();
    }

    /// <summary>
    /// Gets the count of linked wallets (not deleted).
    /// Computed on demand to avoid performance issues.
    /// </summary>
    public int GetLinkedWalletCount()
    {
        return _walletOwnerships.Count(w => !w.IsDeleted);
    }

    /// <summary>
    /// Gets the count of active credentials (not deleted).
    /// Computed on demand to avoid performance issues.
    /// </summary>
    public int GetActiveCredentialCount()
    {
        return _credentials.Count(c => !c.IsDeleted);
    }

    /// <summary>
    /// Gets wallet ownerships for a specific chain.
    /// </summary>
    public IReadOnlyCollection<WalletOwnership> GetWalletOwnershipsForChain(ChainId chainId)
    {
        return _walletOwnerships
            .Where(w => w.IsActive && w.IsForChain(chainId))
            .ToList();
    }

    /// <summary>
    /// Checks if the principal has any active wallet ownerships.
    /// </summary>
    public bool HasActiveWallets()
    {
        return _walletOwnerships.Any(w => w.IsActive);
    }

    /// <summary>
    /// Checks if the principal has any active credentials.
    /// </summary>
    public bool HasActiveCredentials()
    {
        return _credentials.Any(c => !c.IsDeleted);
    }

    /// <summary>
    /// Gets the default wallet ownership for a specific chain.
    /// </summary>
    public WalletOwnership? GetDefaultWalletOwnershipForChain(ChainId chainId)
    {
        var walletId = GetDefaultWalletForChain(chainId);
        if (walletId == null)
            return null;

        return FindWalletOwnership(walletId.Value);
    }

    /// <summary>
    /// Checks if the principal can be safely deleted (no active resources).
    /// Optimized to avoid double enumeration.
    /// </summary>
    public bool CanBeDeleted()
    {
        return !_walletOwnerships.Any(w => w.IsActive) && 
               !_credentials.Any(c => !c.IsDeleted);
    }

    /// <summary>
    /// Gets a summary of active resources for this principal.
    /// Single enumeration for multiple statistics.
    /// </summary>
    public (int ActiveWallets, int ActiveCredentials, bool HasActiveResources) GetResourceSummary()
    {
        var activeWallets = 0;
        var activeCredentials = 0;

        foreach (var wallet in _walletOwnerships)
        {
            if (wallet.IsActive)
                activeWallets++;
        }

        foreach (var credential in _credentials)
        {
            if (!credential.IsDeleted)
                activeCredentials++;
        }

        return (activeWallets, activeCredentials, activeWallets > 0 || activeCredentials > 0);
    }

    /// <summary>
    /// Checks if this principal has a verified email hash.
    /// Used for email-based identification and verification flows.
    /// </summary>
    public bool HasEmailHash() => PrimaryEmailHash.HasValue;

    /// <summary>
    /// Gets the email hash if available.
    /// Used for privacy-preserving email lookups.
    /// </summary>
    public EmailHash? GetEmailHash() => PrimaryEmailHash;

    /// <summary>
    /// Checks if this principal matches the given email hash.
    /// Used for email-based authentication and verification.
    /// </summary>
    public bool MatchesEmailHash(EmailHash emailHash) => 
        PrimaryEmailHash.HasValue && PrimaryEmailHash.Value.Value == emailHash.Value;
}