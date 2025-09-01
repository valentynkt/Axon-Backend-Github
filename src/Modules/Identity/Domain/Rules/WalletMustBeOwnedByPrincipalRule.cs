using Axon.Modules.Identity.Domain.Entities;

namespace Axon.Modules.Identity.Domain.Rules;

/// <summary>
/// Ensures that a wallet is owned by the principal before performing operations on it.
/// </summary>
internal sealed class WalletMustBeOwnedByPrincipalRule : BusinessRule
{
    private readonly WalletId _walletId;
    private readonly IEnumerable<WalletOwnership> _existingOwnerships;

    public WalletMustBeOwnedByPrincipalRule(WalletId walletId, IEnumerable<WalletOwnership> existingOwnerships)
        : base(
            message: $"Wallet {walletId} is not owned by this principal.",
            code: "IDENTITY.WALLET.NOT_OWNED_BY_PRINCIPAL")
    {
        _walletId = walletId;
        _existingOwnerships = existingOwnerships;
    }

    public override bool IsBroken()
    {
        return !_existingOwnerships.Any(w => 
            !w.IsDeleted && w.IsForWallet(_walletId) && w.IsActive);
    }

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}