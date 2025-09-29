using FluentValidation;

namespace Axon.Modules.Identity.Application.Commands.ExchangeCredential;

/// <summary>
/// Validator for ExchangeCredentialCommand that ensures the bearer token
/// is valid for processing through the authentication orchestrator.
/// </summary>
public sealed class ExchangeCredentialCommandValidator : AbstractValidator<ExchangeCredentialCommand>
{
    public ExchangeCredentialCommandValidator()
    {
        RuleFor(x => x.BearerToken)
            .NotEmpty()
            .WithMessage("Bearer token is required")
            .Must(BeValidBearerToken)
            .WithMessage("Bearer token format is invalid");
    }

    private static bool BeValidBearerToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        // JWT tokens have 3 parts separated by dots: header.payload.signature
        var parts = token.Split('.');
        return parts.Length == 3 && parts.All(part => !string.IsNullOrWhiteSpace(part));
    }
}
