using BuildingBlocks.Primitives.Ids;
using FluentValidation;

namespace Axon.Modules.Identity.Application.Queries.GetPrincipal;

/// <summary>
/// Validator for GetPrincipalQuery ensuring valid AxonId format.
/// </summary>
public sealed class GetPrincipalValidator : AbstractValidator<GetPrincipalQuery>
{
    public GetPrincipalValidator()
    {
        RuleFor(x => x.AxonId)
            .NotEmpty()
            .WithMessage("AxonId is required")
            .WithErrorCode("IDENTITY.QUERY.GET_PRINCIPAL.AXON_ID.REQUIRED");
    }
}