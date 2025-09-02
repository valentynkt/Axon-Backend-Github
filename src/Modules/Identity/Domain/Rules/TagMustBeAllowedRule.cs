using Axon.Modules.Identity.Domain.Errors;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Domain.Rules;

namespace Axon.Modules.Identity.Domain.Rules;

/// <summary>
/// Ensures tags belong to the predefined allow-list.
/// Enforces W6 invariant - tag policy validation.
/// </summary>
internal sealed class TagMustBeAllowedRule : BusinessRule
{
    private readonly string _tagValue;

    public TagMustBeAllowedRule(string tagValue)
        : base(
            message: WalletDomainErrors.Tag.NotAllowed(tagValue).Message,
            code: WalletDomainErrors.Tag.NotAllowed(tagValue).Code)
    {
        _tagValue = tagValue;
    }

    public override bool IsBroken()
    {
        var tagResult = Tag.Create(_tagValue);
        return tagResult.IsFailure;
    }

    public override ValueTask<bool> IsBrokenAsync(CancellationToken ct = default) 
        => ValueTask.FromResult(IsBroken());
}