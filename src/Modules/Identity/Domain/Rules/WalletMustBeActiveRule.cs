using Axon.Modules.Identity.Domain.Aggregates.Wallet;
using Axon.Modules.Identity.Domain.Errors;
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
            message: WalletDomainErrors.Wallet.SoftDeleted().Message,
            code: WalletDomainErrors.Wallet.SoftDeleted().Code)
    {
        _wallet = wallet;
    }

    public override bool IsBroken() => _wallet.IsDeleted;

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}