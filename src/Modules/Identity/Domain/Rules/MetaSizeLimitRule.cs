using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Domain.Rules;

namespace Axon.Modules.Identity.Domain.Rules;

/// <summary>
/// Ensures wallet metadata does not exceed size and complexity limits.
/// Enforces W5 invariant - meta size and shape constraints.
/// </summary>
internal sealed class MetaSizeLimitRule : BusinessRule
{
    private readonly WalletMeta _currentMeta;
    private readonly Dictionary<string, object>? _additionalData;

    public MetaSizeLimitRule(WalletMeta currentMeta, Dictionary<string, object>? additionalData = null)
        : base(
            message: "Wallet metadata exceeds size or complexity limits.",
            code: "WALLET.META_SIZE_LIMIT")
    {
        _currentMeta = currentMeta;
        _additionalData = additionalData;
    }

    public override bool IsBroken()
    {
        if (_additionalData is null || _additionalData.Count == 0)
            return _currentMeta.EstimatedSizeBytes > WalletMeta.MaxSizeBytes;

        // Test merge to check limits
        var mergeResult = _currentMeta.Merge(_additionalData);
        return mergeResult.IsFailure;
    }

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}