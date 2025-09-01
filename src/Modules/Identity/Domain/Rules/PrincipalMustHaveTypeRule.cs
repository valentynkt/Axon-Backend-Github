using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Domain.Rules;

/// <summary>
/// Ensures that a principal has a valid type assigned.
/// </summary>
internal sealed class PrincipalMustHaveTypeRule : BusinessRule
{
    private readonly PrincipalType _principalType;

    public PrincipalMustHaveTypeRule(PrincipalType principalType)
        : base(
            message: "Principal must have a valid type assigned.",
            code: "IDENTITY.PRINCIPAL.TYPE.REQUIRED")
    {
        _principalType = principalType;
    }

    public override bool IsBroken()
    {
        return string.IsNullOrWhiteSpace(_principalType.Value);
    }

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}