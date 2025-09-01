using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using BuildingBlocks.Core.Domain.Rules;

namespace Axon.Modules.Identity.Domain.Rules;

/// <summary>
/// Ensures that operations can only be performed on active (non-deleted) wallets.
/// Enforces W7 invariant - soft delete guard.
/// </summary>
internal sealed class WalletMustBeActiveRule : BusinessRule
{
    private readonly Wallet _wallet;

    public WalletMustBeActiveRule(Wallet wallet)
        : base(
            message: "Wallet must be active (not deleted) to perform this operation.",
            code: "WALLET.MUST_BE_ACTIVE")
    {
        _wallet = wallet;
    }

    public override bool IsBroken() => _wallet.IsDeleted;

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}