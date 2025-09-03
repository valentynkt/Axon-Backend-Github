using FluentValidation;

namespace Axon.Modules.Identity.Application.Queries.GetPrincipalByCredential;

/// <summary>
/// Validator for GetPrincipalByCredentialQuery ensuring valid credential parameters.
/// Matches domain constraints: Issuer/Subject max 255 chars (aligned with command-side).
/// </summary>
public sealed class GetPrincipalByCredentialValidator : AbstractValidator<GetPrincipalByCredentialQuery>
{
    public GetPrincipalByCredentialValidator()
    {
        RuleFor(x => x.ProviderType)
            .NotEmpty()
            .WithMessage("ProviderType is required")
            .WithErrorCode("IDENTITY.QUERY.GET_PRINCIPAL_BY_CREDENTIAL.PROVIDER_TYPE.REQUIRED")
            .MaximumLength(50)
            .WithMessage("ProviderType cannot exceed 50 characters")
            .WithErrorCode("IDENTITY.QUERY.GET_PRINCIPAL_BY_CREDENTIAL.PROVIDER_TYPE.TOO_LONG");

        RuleFor(x => x.Issuer)
            .NotEmpty()
            .WithMessage("Issuer is required")
            .WithErrorCode("IDENTITY.QUERY.GET_PRINCIPAL_BY_CREDENTIAL.ISSUER.REQUIRED")
            .MaximumLength(255)
            .WithMessage("Issuer cannot exceed 255 characters")
            .WithErrorCode("IDENTITY.QUERY.GET_PRINCIPAL_BY_CREDENTIAL.ISSUER.TOO_LONG");

        RuleFor(x => x.Subject)
            .NotEmpty()
            .WithMessage("Subject is required")
            .WithErrorCode("IDENTITY.QUERY.GET_PRINCIPAL_BY_CREDENTIAL.SUBJECT.REQUIRED")
            .MaximumLength(255)
            .WithMessage("Subject cannot exceed 255 characters")
            .WithErrorCode("IDENTITY.QUERY.GET_PRINCIPAL_BY_CREDENTIAL.SUBJECT.TOO_LONG");
    }
}