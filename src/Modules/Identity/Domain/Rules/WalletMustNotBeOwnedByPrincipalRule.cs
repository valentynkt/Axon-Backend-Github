using Axon.Modules.Identity.Domain.Entities;

namespace Axon.Modules.Identity.Domain.Rules;

/// <summary>
/// Ensures that a wallet is not already owned by the same principal.
/// </summary>
internal sealed class WalletMustNotBeOwnedByPrincipalRule : BusinessRule
{
    private readonly WalletId _walletId;
    private readonly IEnumerable<WalletOwnership> _existingOwnerships;

    public WalletMustNotBeOwnedByPrincipalRule(WalletId walletId, IEnumerable<WalletOwnership> existingOwnerships)
        : base(
            message: $"Wallet {walletId} is already owned by this principal.",
            code: "IDENTITY.WALLET.ALREADY_OWNED_BY_PRINCIPAL")
    {
        _walletId = walletId;
        _existingOwnerships = existingOwnerships;
    }

    public override bool IsBroken()
    {
        return _existingOwnerships.Any(w => 
            !w.IsDeleted && w.IsForWallet(_walletId));
    }

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}