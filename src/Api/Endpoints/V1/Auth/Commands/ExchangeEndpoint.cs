using Axon.Api.Contracts.V1.Auth;
using Axon.Api.Modules;
using Axon.Modules.Identity.Application.Commands.ExchangeCredential;
using Axon.Modules.Identity.Application.Contracts.ExternalServices;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Utilities;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.AspNetCore.Authentication;

namespace Axon.Api.Endpoints.V1.Auth;

/// <summary>
/// POST /auth/exchange - Exchange Dynamic JWT for Axon identity
/// NOTE: JWT is provided via Authorization: Bearer <token>. Endpoint is AllowAnonymous
/// and performs validation manually (does not rely on ASP.NET auth pipeline).
/// </summary>
public sealed class ExchangeEndpoint
    : BaseIdentityCommandEndpoint<
        ExchangeTokenRequestDto,
        AuthTokenResponseDto,
        ExchangeCredentialCommand,
        ExchangeOutcome>
{
    private const string JwtIssuerMetadataKey = "jwt_issuer";
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IDynamicAuthService _dynamicAuthService;
    private readonly IAddressNormalizationService _addressNormalizationService;
    public ExchangeEndpoint(
        IMediator mediator,
        ILogger<ExchangeEndpoint> logger,
        IJwtTokenService jwtTokenService,
        IDynamicAuthService dynamicAuthService,
        IAddressNormalizationService addressNormalizationService)
        : base(mediator, logger)
    {
        _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
        _dynamicAuthService = dynamicAuthService ?? throw new ArgumentNullException(nameof(dynamicAuthService));
        _addressNormalizationService = addressNormalizationService ?? throw new ArgumentNullException(nameof(addressNormalizationService));
    }

    protected override string GetRoute() => "/api/v1/auth/exchange";
    protected override string GetSummary() => "Exchange bearer token for Axon identity and access token";
    protected override string GetDescription() =>
        """
        Validates a bearer token (Dynamic JWT or Axon Access Token) and creates/updates the Axon principal and wallet links.

        **Supported Token Types**:
        • Dynamic JWT (from Dynamic.xyz authentication)
        • Axon Access Token (from manual wallet sign-in)

        **Behavior**:
        • Token is supplied via Authorization: Bearer <token>
        • Endpoint is AllowAnonymous (token handled as input data)
        • Idempotent: safe to retry
        • Returns new Axon JWT access token for subsequent API calls
        • Rate Limited: configured globally (e.g., 10 req/min/IP)
        """;
    protected override string GetSuccessResponse() =>
        "Returns exchange outcome with wallet processing metrics";

    public override void Configure()
    {
        base.Configure();

        // Apply rate limiting policy for exchange endpoint
        Options(x => x.RequireRateLimiting("AuthExchange"));

        // Document responses succinctly
        Summary(s =>
        {
            s.Summary = GetSummary();
            s.Description = GetDescription();
            s.Responses[200] = GetSuccessResponse();
            s.Responses[400] = "Invalid request parameters";
            s.Responses[401] = "Invalid or missing bearer token";
            s.Responses[409] = "Ownership conflict (wallet already owned by another principal)";
            s.Responses[422] = "Business rule violation";
            s.Responses[429] = "Too many requests";
            s.Responses[500] = "Internal server error";
        });
    }

    protected override async Task<Result<ExchangeCredentialCommand, Error>> ExecuteCommand(
        ExchangeTokenRequestDto _,
        CancellationToken ct)
    {
        // Manual authentication against DynamicJwt scheme
        var authResult = await HttpContext.AuthenticateAsync("DynamicJwt");

        if (!authResult.Succeeded)
        {
            var reason = authResult.None
                ? "Missing or invalid Authorization header"
                : authResult.Failure?.Message;

            Logger.LogWarning("Dynamic JWT authentication failed: {Reason}", reason);

            return Result.Failure<ExchangeCredentialCommand, Error>(
                Error.Unauthorized(
                    reason ?? "Invalid Dynamic JWT",
                    "AUTH.INVALID_TOKEN"));
        }

        var principal = authResult.Principal!;

        // Extract token context from authenticated principal
        var issuer = principal.FindFirst("iss")?.Value ?? "https://app.dynamic.xyz";

        // Extract bearer token from Authorization header for Dynamic service call
        var authHeader = HttpContext.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<ExchangeCredentialCommand, Error>(
                Error.Unauthorized("Missing or invalid Authorization header"));
        }

        var bearerToken = authHeader["Bearer ".Length..].Trim();

        // For Dynamic JWT tokens, extract user data from Dynamic service
        var dynamicValidationResult = await _dynamicAuthService.ValidateTokenAsync(bearerToken, ct);
        if (dynamicValidationResult.IsFailure)
        {
            return Result.Failure<ExchangeCredentialCommand, Error>(dynamicValidationResult.Error);
        }

        var dynamicUser = dynamicValidationResult.Value;

        // Map Dynamic data to command input with address normalization
        var exchangeWallets = new List<ExchangeWalletData>();
        foreach (var wallet in dynamicUser.Wallets)
        {
            // Convert simple chain IDs from Dynamic to compound format
            var compoundChainId = ChainIdConverter.ConvertToCompoundChainId(wallet.Chain);

            // Apply address normalization at API edge per Story 5.3 AC#13
            var normalizedAddressResult = _addressNormalizationService.NormalizeAddress(compoundChainId, wallet.Address);
            if (normalizedAddressResult.IsFailure)
            {
                Logger.LogWarning("Failed to normalize address {Address} for chain {Chain}: {Error}",
                    wallet.Address, compoundChainId, normalizedAddressResult.Error.Message);

                // Skip invalid addresses rather than failing the entire exchange
                continue;
            }

            exchangeWallets.Add(new ExchangeWalletData(
                Address:        normalizedAddressResult.Value.Value,
                Chain:          compoundChainId,
                WalletName:     wallet.WalletName,
                Provider:       wallet.Provider,
                ConnectedAtUtc: wallet.ConnectedAtUtc
            ));
        }

        var additional = new Dictionary<string, object>
        {
            [JwtIssuerMetadataKey] = issuer
        };


        var userData = new ExchangeUserData(
            AxonUserId:              dynamicUser.AxonUserId,
            Email:                   dynamicUser.Email,
            DynamicEnvironmentId:    dynamicUser.EnvironmentId, // Keep original Dynamic environment ID for issuer construction
            Wallets:                 exchangeWallets,
            FirstVisitUtc:           dynamicUser.FirstVisitUtc,
            LastVisitUtc:            dynamicUser.LastVisitUtc,
            IsNewUser:               dynamicUser.IsNewUser,
            AdditionalMetadata:      additional
        );

        // 4) Prevent caching of auth responses
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers.Pragma = "no-cache";

        return Result.Success<ExchangeCredentialCommand, Error>(new ExchangeCredentialCommand(userData));
    }

    protected override async Task<Result<AuthTokenResponseDto, Error>> MapDomainToResponseAsync(ExchangeOutcome outcome, CancellationToken ct)
    {
        // Extract provider context from the exchange request
        var authHeader = HttpContext.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogWarning("MapDomainToResponseAsync: Missing Authorization header during token generation");
            return Result.Failure<AuthTokenResponseDto, Error>(
                Error.Internal("Unable to generate access token: missing authorization context", "EXCHANGE.MISSING_AUTH_CONTEXT"));
        }

        var jwt = authHeader["Bearer ".Length..].Trim();

        // Get raw claims to extract provider information
        var rawClaimsResult = await _dynamicAuthService.GetRawClaimsAsync(jwt, ct);
        if (rawClaimsResult.IsFailure)
        {
            Logger.LogWarning("MapDomainToResponseAsync: Failed to get raw claims for token generation: {Error}", rawClaimsResult.Error.Message);
            return Result.Failure<AuthTokenResponseDto, Error>(
                Error.Internal("Unable to generate access token: invalid authorization context", "EXCHANGE.INVALID_AUTH_CONTEXT"));
        }

        var claims = rawClaimsResult.Value;
        var issuer = claims.FindFirst("iss")?.Value ?? "unknown";
        var subject = claims.FindFirst("sub")?.Value ?? "unknown";

        // Create provider type for Dynamic
        var providerTypeResult = ProviderType.Create("dynamic");
        if (providerTypeResult.IsFailure)
        {
            return Result.Failure<AuthTokenResponseDto, Error>(providerTypeResult.Error);
        }

        // Generate Axon JWT access token using JWT token service (uses configuration default expiration)
        var accessTokenResult = await _jwtTokenService.GenerateAccessTokenAsync(
            outcome.AxonUserId,
            providerTypeResult.Value,
            issuer,
            subject,
            -1, // Use configuration default
            ct);

        if (accessTokenResult.IsFailure)
        {
            Logger.LogError("Failed to generate Axon JWT token for AxonUserId={AxonUserId}: {Error}",
                outcome.AxonUserId.Value, accessTokenResult.Error.Message);
            return Result.Failure<AuthTokenResponseDto, Error>(accessTokenResult.Error);
        }

        var token = accessTokenResult.Value;

        var response = new AuthTokenResponseDto(
            AccessToken:        token.AccessToken,
            TokenType:          token.TokenType,
            ExpiresIn:          token.ExpiresIn,
            AxonUserId:         outcome.AxonUserId.ToString(),
            Created:            outcome.Created,
            WalletsLinked:      outcome.WalletsLinked,
            Conflicts:          outcome.Conflicts
        );

        return Result.Success<AuthTokenResponseDto, Error>(response);
    }

}
