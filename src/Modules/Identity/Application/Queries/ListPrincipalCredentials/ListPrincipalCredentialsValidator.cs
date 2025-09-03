using FluentValidation;

namespace Axon.Modules.Identity.Application.Queries.ListPrincipalCredentials;

/// <summary>
/// Validator for ListPrincipalCredentialsQuery ensuring valid AxonId format.
/// </summary>
public sealed class ListPrincipalCredentialsValidator : AbstractValidator<ListPrincipalCredentialsQuery>
{
    public ListPrincipalCredentialsValidator()
    {
        RuleFor(x => x.AxonId)
            .NotEmpty()
            .WithMessage("AxonId is required")
            .WithErrorCode("IDENTITY.QUERY.LIST_PRINCIPAL_CREDENTIALS.AXON_ID.REQUIRED");
    }
}