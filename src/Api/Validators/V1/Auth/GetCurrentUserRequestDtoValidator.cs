using Axon.Api.Contracts.V1.Auth;
using FluentValidation;

namespace Axon.Api.Validators.V1.Auth;

/// <summary>
/// Validator for GetCurrentUserRequestDto
/// </summary>
public sealed class GetCurrentUserRequestDtoValidator : AbstractValidator<GetCurrentUserRequestDto>
{
    public GetCurrentUserRequestDtoValidator()
    {
        // For this DTO, there's no body validation needed since it's an empty GET request
        // Authentication is handled by the endpoint itself
    }
}