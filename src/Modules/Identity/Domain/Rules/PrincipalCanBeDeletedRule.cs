using Axon.Modules.Identity.Domain.Entities;

namespace Axon.Modules.Identity.Domain.Rules;

/// <summary>
/// Ensures that a principal can be safely deleted (no active wallet ownerships or credentials).
/// </summary>
internal sealed class PrincipalCanBeDeletedRule : BusinessRule
{
    private readonly IEnumerable<WalletOwnership> _walletOwnerships;
    private readonly IEnumerable<IdentityCredential> _credentials;

    public PrincipalCanBeDeletedRule(
        IEnumerable<WalletOwnership> walletOwnerships,
        IEnumerable<IdentityCredential> credentials)
        : base(
            message: "Principal cannot be deleted while having active wallet ownerships or credentials.",
            code: "IDENTITY.PRINCIPAL.CANNOT_BE_DELETED")
    {
        _walletOwnerships = walletOwnerships;
        _credentials = credentials;
    }

    public override bool IsBroken()
    {
        var hasActiveWallets = _walletOwnerships.Any(w => w.IsActive);
        var hasActiveCredentials = _credentials.Any(c => !c.IsDeleted);
        
        return hasActiveWallets || hasActiveCredentials;
    }

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}