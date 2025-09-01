using Axon.Modules.Identity.Domain.Entities;

namespace Axon.Modules.Identity.Domain.Rules;

/// <summary>
/// Ensures that a principal can only have one default wallet per blockchain chain.
/// This rule validates that setting a new default for a chain will properly replace any existing default.
/// </summary>
internal sealed class SingleDefaultPerChainRule : BusinessRule
{
    private readonly string _chain;
    private readonly long _newDefaultWalletId;
    private readonly PrincipalProfile _profile;
    private readonly IEnumerable<WalletOwnership> _walletOwnerships;

    public SingleDefaultPerChainRule(
        string chain,
        long newDefaultWalletId,
        PrincipalProfile profile,
        IEnumerable<WalletOwnership> walletOwnerships)
        : base(
            message: $"Chain '{chain}' already has a default wallet configured.",
            code: "IDENTITY.PROFILE.DEFAULT.CHAIN.DUPLICATE")
    {
        _chain = chain;
        _newDefaultWalletId = newDefaultWalletId;
        _profile = profile;
        _walletOwnerships = walletOwnerships;
    }

    public override bool IsBroken()
    {
        var currentDefault = _profile.GetDefaultWalletForChain(_chain);
        
        // Not broken if no current default
        if (!currentDefault.HasValue)
            return false;

        // Not broken if we're setting the same wallet as default (idempotent operation)
        if (currentDefault.Value == _newDefaultWalletId)
            return false;

        // Check if the current default wallet is still owned by the principal
        var currentDefaultWallet = _walletOwnerships
            .FirstOrDefault(w => w.IsForWallet(currentDefault.Value) && w.IsActive);

        // Not broken if the current default is no longer owned (cleanup scenario)
        if (currentDefaultWallet is null)
            return false;

        // Broken if we're trying to set a different wallet as default when one already exists
        return true;
    }

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}