namespace Axon.Modules.Identity.Domain.Rules;

/// <summary>
/// Ensures that a principal cannot exceed the maximum number of linked wallets.
/// </summary>
internal sealed class MaxWalletsPerPrincipalRule : BusinessRule
{
    private const int MaxWallets = 10;
    private readonly int _currentWalletCount;

    public MaxWalletsPerPrincipalRule(int currentWalletCount)
        : base(
            message: $"Principal cannot have more than {MaxWallets} linked wallets. Current count: {currentWalletCount}.",
            code: "IDENTITY.WALLET.MAX_EXCEEDED")
    {
        _currentWalletCount = currentWalletCount;
    }

    public override bool IsBroken() => _currentWalletCount >= MaxWallets;

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}