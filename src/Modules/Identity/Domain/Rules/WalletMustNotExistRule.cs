using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Domain.Rules;

namespace Axon.Modules.Identity.Domain.Rules;

/// <summary>
/// Ensures global uniqueness of (Chain, Address) pairs during wallet registration.
/// Prevents duplicate wallet registrations that would violate W1 invariant.
/// </summary>
internal sealed class WalletMustNotExistRule : BusinessRule
{
    private readonly ChainId _chainId;
    private readonly Address _address;
    private readonly Func<ChainId, Address, ValueTask<bool>> _walletExistsCheck;

    public WalletMustNotExistRule(
        ChainId chainId, 
        Address address, 
        Func<ChainId, Address, ValueTask<bool>> walletExistsCheck)
        : base(
            message: "Wallet with this chain and address combination already exists.",
            code: "WALLET.ALREADY_EXISTS")
    {
        _chainId = chainId;
        _address = address;
        _walletExistsCheck = walletExistsCheck;
    }

    public override bool IsBroken()
    {
        // Synchronous version - should avoid if possible
        return _walletExistsCheck(_chainId, _address).AsTask().GetAwaiter().GetResult();
    }

    public override async ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => await _walletExistsCheck(_chainId, _address);
}