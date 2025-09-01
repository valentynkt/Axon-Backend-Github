using Axon.Modules.Identity.Domain.ValueObjects;

namespace Axon.Modules.Identity.Domain.Rules;

/// <summary>
/// Ensures that a risk tier change is appropriate for the principal's context.
/// While RiskTier has built-in validation, this rule adds context-specific constraints
/// (e.g., service principals might have different risk tier constraints).
/// </summary>
internal sealed class ValidRiskTierRule : BusinessRule
{
    private readonly RiskTier _riskTier;
    private readonly PrincipalType _principalType;

    public ValidRiskTierRule(RiskTier riskTier, PrincipalType principalType)
        : base(
            message: $"Risk tier '{riskTier.Value}' is not valid for principal type '{principalType.Value}'.",
            code: "IDENTITY.PROFILE.RISK_TIER.INVALID_FOR_PRINCIPAL_TYPE")
    {
        _riskTier = riskTier;
        _principalType = principalType;
    }

    public override bool IsBroken()
    {
        // Service principals should only use conservative risk tier for security
        if (_principalType.IsService && !_riskTier.IsConservative)
            return true;

        // Human principals can use any risk tier
        return false;
    }

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}