using Axon.Api.Contracts.V1.Auth;
using Axon.Api.Modules;
using Axon.Modules.Identity.Application.Commands.ExchangeToken;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using Axon.Modules.Identity.Application.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Api.Endpoints.V1.Auth;

/// <summary>
/// POST /auth/exchange - Exchange Dynamic JWT for Axon identity
/// Uses BaseIdentityCommandEndpoint for standardized error handling and validation
/// </summary>
public sealed class ExchangeEndpoint : BaseIdentityCommandEndpoint<ExchangeTokenRequestDto, ExchangeTokenResponseDto, ExchangeTokenCommand, ExchangeOutcome>
{
    public ExchangeEndpoint(IMediator mediator, ILogger<ExchangeEndpoint> logger) 
        : base(mediator, logger)
    {
    }

    protected override string GetRoute() => "/auth/exchange";

    protected override string GetSummary() => "Exchange Dynamic JWT for Axon identity";

    protected override string GetDescription() => 
        """
        Validates a Dynamic.xyz JWT token and creates/updates the corresponding Axon principal.
        
        **JWT is supplied via Authorization: Bearer <token>**
        
        **Rate Limited**: 10 requests per minute per IP
        """;

    protected override string GetSuccessResponse() => "Returns exchange outcome with wallet processing details";

    protected override async Task<Result<ExchangeTokenCommand, Error>> ExecuteCommand(ExchangeTokenRequestDto request, CancellationToken ct)
    {
        // Extract JWT from Authorization header
        var authHeader = HttpContext.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogWarning("Exchange request missing Authorization header or Bearer token");
            return Result.Failure<ExchangeTokenCommand, Error>(
                Error.Validation("Authorization header with Bearer token is required"));
        }

        var jwt = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(jwt))
        {
            Logger.LogWarning("Exchange request has empty Bearer token");
            return Result.Failure<ExchangeTokenCommand, Error>(
                Error.Validation("Bearer token cannot be empty"));
        }

        return Result.Success<ExchangeTokenCommand, Error>(new ExchangeTokenCommand(jwt));
    }
}