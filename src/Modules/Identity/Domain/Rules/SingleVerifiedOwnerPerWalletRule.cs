using Axon.Modules.Identity.Domain.Abstractions;

namespace Axon.Modules.Identity.Domain.Rules;

/// <summary>
/// Ensures that a wallet can only have one verified owner globally across all principals.
/// This rule requires repository access for cross-aggregate validation.
/// </summary>
internal sealed class SingleVerifiedOwnerPerWalletRule : BusinessRule
{
    private readonly long _walletId;
    private readonly AxonId _currentPrincipalId;
    private readonly IAxonPrincipalRepository _repository;

    public SingleVerifiedOwnerPerWalletRule(
        long walletId,
        AxonId currentPrincipalId,
        IAxonPrincipalRepository repository)
        : base(
            message: $"Wallet ID {walletId} already has a verified owner.",
            code: "IDENTITY.WALLET.ALREADY_OWNED")
    {
        _walletId = walletId;
        _currentPrincipalId = currentPrincipalId;
        _repository = repository;
    }

    public override bool IsBroken()
    {
        // Synchronous version throws - should use async version
        throw new InvalidOperationException("Use IsBrokenAsync for cross-aggregate validation.");
    }

    public override async ValueTask<bool> IsBrokenAsync(CancellationToken ct = default)
    {
        var existingOwner = await _repository.FindByWalletIdAsync(_walletId, ct);

        if (existingOwner is null)
            return false;

        // It's broken if the wallet belongs to a different principal
        return existingOwner.Id != _currentPrincipalId;
    }
}