using BuildingBlocks.Core.Domain.Rules;

namespace Axon.Modules.Identity.Domain.Rules;

/// <summary>
/// Ensures LastSeenAt timestamp moves monotonically forward.
/// Enforces W4 invariant - time monotonicity.
/// </summary>
internal sealed class TimestampMonotonicityRule : BusinessRule
{
    private readonly DateTimeOffset _currentLastSeenAt;
    private readonly DateTimeOffset _firstSeenAt;
    private readonly DateTimeOffset _newObservedAt;

    public TimestampMonotonicityRule(
        DateTimeOffset currentLastSeenAt, 
        DateTimeOffset firstSeenAt, 
        DateTimeOffset newObservedAt)
        : base(
            message: "Observed timestamp cannot regress. LastSeenAt must be greater than or equal to FirstSeenAt and current LastSeenAt.",
            code: "WALLET.TIMESTAMP_REGRESSION")
    {
        _currentLastSeenAt = currentLastSeenAt;
        _firstSeenAt = firstSeenAt;
        _newObservedAt = newObservedAt;
    }

    public override bool IsBroken()
    {
        return _newObservedAt < _firstSeenAt || _newObservedAt < _currentLastSeenAt;
    }

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}