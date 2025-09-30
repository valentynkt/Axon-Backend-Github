using FluentValidation;

namespace Axon.Modules.Identity.Application.Commands.RefreshToken;

/// <summary>
/// Validator for RefreshTokenCommand that ensures the refresh token
/// is valid for processing through the authentication orchestrator.
/// Validates JWT format (3 parts separated by dots with non-empty segments).
/// </summary>
public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .WithMessage("Refresh token is required")
            .Must(BeValidJwtFormat)
            .WithMessage("Refresh token format is invalid");
    }

    private static bool BeValidJwtFormat(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        // JWT tokens have 3 parts separated by dots: header.payload.signature
        var parts = token.Split('.');
        return parts.Length == 3 && parts.All(part => !string.IsNullOrWhiteSpace(part));
    }
}