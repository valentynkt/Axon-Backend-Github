using Axon.Api.Contracts.V1.Auth;
using FluentValidation;

namespace Axon.Api.Validators.V1.Auth;

/// <summary>
/// Validator for ExchangeTokenRequestDto
/// </summary>
public sealed class ExchangeTokenRequestDtoValidator : AbstractValidator<ExchangeTokenRequestDto>
{
    public ExchangeTokenRequestDtoValidator()
    {
        // For this DTO, there's no body validation needed since JWT comes from header
        // However, we can add custom rules if needed in the future
    }
}