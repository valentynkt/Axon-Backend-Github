using Axon.Api.Contracts.V1.Auth;
using Axon.Api.Modules;
using Axon.Modules.Identity.Application.Commands.ExchangeCredential;
using Axon.Modules.Identity.Application.Contracts.ExternalServices;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Api.Endpoints.V1.Auth;

/// <summary>
/// POST /auth/exchange - Exchange Dynamic JWT for Axon identity
/// Uses BaseIdentityCommandEndpoint for standardized error handling and validation
/// </summary>
public sealed class ExchangeEndpoint : BaseIdentityCommandEndpoint<ExchangeTokenRequestDto, ExchangeTokenResponseDto, ExchangeCredentialCommand, ExchangeOutcome>
{
    private readonly IDynamicAuthService _dynamicAuthService;

    public ExchangeEndpoint(IMediator mediator, ILogger<ExchangeEndpoint> logger, IDynamicAuthService dynamicAuthService) 
        : base(mediator, logger)
    {
        _dynamicAuthService = dynamicAuthService ?? throw new ArgumentNullException(nameof(dynamicAuthService));
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

    protected override async Task<Result<ExchangeCredentialCommand, Error>> ExecuteCommand(ExchangeTokenRequestDto request, CancellationToken ct)
    {
        // Extract JWT from Authorization header
        var authHeader = HttpContext.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogWarning("Exchange request missing Authorization header or Bearer token");
            return Result.Failure<ExchangeCredentialCommand, Error>(
                Error.Validation("Authorization header with Bearer token is required"));
        }

        var jwt = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(jwt))
        {
            Logger.LogWarning("Exchange request has empty Bearer token");
            return Result.Failure<ExchangeCredentialCommand, Error>(
                Error.Validation("Bearer token cannot be empty"));
        }

        // Validate JWT and extract user data using Dynamic service
        var validationResult = await _dynamicAuthService.ValidateTokenAsync(jwt, ct);
        if (validationResult.IsFailure)
        {
            Logger.LogWarning("JWT validation failed: {Error}", validationResult.Error.Message);
            return Result.Failure<ExchangeCredentialCommand, Error>(validationResult.Error);
        }

        var dynamicUserData = validationResult.Value;

        // Convert DynamicUserData to ExchangeUserData
        var exchangeUserData = ConvertToExchangeUserData(dynamicUserData);

        return Result.Success<ExchangeCredentialCommand, Error>(new ExchangeCredentialCommand(exchangeUserData));
    }

    /// <summary>
    /// Converts DynamicUserData from JWT validation to ExchangeUserData for command processing
    /// </summary>
    private static ExchangeUserData ConvertToExchangeUserData(DynamicUserData dynamicUserData)
    {
        var exchangeWallets = dynamicUserData.Wallets.Select(w => new ExchangeWalletData(
            Address: w.Address,
            Chain: w.Chain,
            WalletName: w.WalletName,
            Provider: w.Provider,
            ConnectedAtUtc: w.ConnectedAtUtc
        )).ToList();

        return new ExchangeUserData(
            UserId: dynamicUserData.UserId,
            Email: dynamicUserData.Email,
            EnvironmentId: dynamicUserData.EnvironmentId,
            Wallets: exchangeWallets,
            FirstVisitUtc: dynamicUserData.FirstVisitUtc,
            LastVisitUtc: dynamicUserData.LastVisitUtc,
            IsNewUser: dynamicUserData.IsNewUser
        );
    }
}