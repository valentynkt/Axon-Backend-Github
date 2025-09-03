using Axon.Modules.Identity.Domain.Aggregates.AxonPrincipal;
using BuildingBlocks.Core.Domain.Rules;

namespace Axon.Modules.Identity.Domain.Rules;

/// <summary>
/// Ensures that operations can only be performed on active (non-deleted) principals.
/// </summary>
internal sealed class PrincipalMustBeActiveRule : BusinessRule
{
    private readonly AxonPrincipal _principal;

    public PrincipalMustBeActiveRule(AxonPrincipal principal)
        : base(
            message: "Principal must be active to perform this operation.",
            code: "IDENTITY.PRINCIPAL.MUST_BE_ACTIVE")
    {
        _principal = principal;
    }

    public override bool IsBroken() => _principal.IsDeleted;

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}