using FluentValidation;

namespace Axon.Modules.Identity.Application.Commands.ExchangeToken;

/// <summary>
/// Validator for ExchangeTokenCommand
/// </summary>
public sealed class ExchangeTokenCommandValidator : AbstractValidator<ExchangeTokenCommand>
{
    public ExchangeTokenCommandValidator()
    {
        RuleFor(x => x.Jwt)
            .NotEmpty()
            .WithMessage("JWT token is required")
            .Must(BeValidJwtFormat)
            .WithMessage("JWT token must have valid format (three parts separated by dots)");
    }

    private static bool BeValidJwtFormat(string jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt))
            return false;

        var parts = jwt.Split('.');
        return parts.Length == 3 && parts.All(part => !string.IsNullOrWhiteSpace(part));
    }
}