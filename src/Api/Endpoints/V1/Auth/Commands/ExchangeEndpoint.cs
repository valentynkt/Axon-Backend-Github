using Axon.Api.Contracts.V1.Auth;
using Axon.Api.Modules;
using Axon.Modules.Identity.Application.Commands.ExchangeCredential;
using Axon.Modules.Identity.Application.Contracts.ExternalServices;
using Axon.Modules.Identity.Application.Contracts.Services;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using Axon.Modules.Identity.Domain.ValueObjects;
using Axon.Modules.Identity.Infrastructure.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
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
        ExchangeTokenResponseDto,
        ExchangeCredentialCommand,
        ExchangeOutcome>
{
    private const string JwtIssuerMetadataKey = "jwt_issuer";
    private readonly Axon.Modules.Identity.Application.Contracts.Services.IAuthenticationService _authenticationService;
    private readonly IDynamicAuthService _dynamicAuthService;
    private readonly IAddressNormalizationService _addressNormalizationService;
    private readonly IBearerTokenExtractor _bearerTokenExtractor;
    private readonly INetworkEnvironmentResolver _networkEnvironmentResolver;

    public ExchangeEndpoint(
        IMediator mediator,
        ILogger<ExchangeEndpoint> logger,
        Axon.Modules.Identity.Application.Contracts.Services.IAuthenticationService authenticationService,
        IDynamicAuthService dynamicAuthService,
        IAddressNormalizationService addressNormalizationService,
        IBearerTokenExtractor bearerTokenExtractor,
        INetworkEnvironmentResolver networkEnvironmentResolver)
        : base(mediator, logger)
    {
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
        _dynamicAuthService = dynamicAuthService ?? throw new ArgumentNullException(nameof(dynamicAuthService));
        _addressNormalizationService = addressNormalizationService ?? throw new ArgumentNullException(nameof(addressNormalizationService));
        _bearerTokenExtractor = bearerTokenExtractor ?? throw new ArgumentNullException(nameof(bearerTokenExtractor));
        _networkEnvironmentResolver = networkEnvironmentResolver ?? throw new ArgumentNullException(nameof(networkEnvironmentResolver));
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

        // Extract bearer token for Dynamic service call
        var tokenResult = _bearerTokenExtractor.ExtractBearerToken(HttpContext);
        if (tokenResult.IsFailure)
        {
            return Result.Failure<ExchangeCredentialCommand, Error>(tokenResult.Error);
        }

        var bearerToken = tokenResult.Value;

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
            // Apply address normalization at API edge per Story 5.3 AC#13
            var normalizedAddressResult = _addressNormalizationService.NormalizeAddress(wallet.Chain, wallet.Address);
            if (normalizedAddressResult.IsFailure)
            {
                Logger.LogWarning("Failed to normalize address {Address} for chain {Chain}: {Error}",
                    wallet.Address, wallet.Chain, normalizedAddressResult.Error.Message);

                // Skip invalid addresses rather than failing the entire exchange
                continue;
            }

            exchangeWallets.Add(new ExchangeWalletData(
                Address:        normalizedAddressResult.Value.Value,
                Chain:          wallet.Chain,
                WalletName:     wallet.WalletName,
                Provider:       wallet.Provider,
                ConnectedAtUtc: wallet.ConnectedAtUtc
            ));
        }

        var additional = new Dictionary<string, object>
        {
            [JwtIssuerMetadataKey] = issuer
        };

        // Resolve NetworkEnvironment from Dynamic's EnvironmentId
        var networkEnvResult = _networkEnvironmentResolver.ResolveFromDynamicEnvironment(dynamicUser.EnvironmentId);
        if (networkEnvResult.IsFailure)
        {
            Logger.LogWarning("Failed to resolve NetworkEnvironment from Dynamic EnvironmentId '{EnvironmentId}': {Error}",
                dynamicUser.EnvironmentId, networkEnvResult.Error.Message);
            return Result.Failure<ExchangeCredentialCommand, Error>(networkEnvResult.Error);
        }

        var userData = new ExchangeUserData(
            AxonUserId:       dynamicUser.AxonUserId,
            Email:            dynamicUser.Email,
            EnvironmentId:    networkEnvResult.Value.Value, // Now properly mapped NetworkEnvironment
            Wallets:          exchangeWallets,
            FirstVisitUtc:    dynamicUser.FirstVisitUtc,
            LastVisitUtc:     dynamicUser.LastVisitUtc,
            IsNewUser:        dynamicUser.IsNewUser,
            AdditionalMetadata: additional
        );

        // 4) Prevent caching of auth responses
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers.Pragma = "no-cache";

        return Result.Success<ExchangeCredentialCommand, Error>(new ExchangeCredentialCommand(userData));
    }

    protected override async Task<Result<ExchangeTokenResponseDto, Error>> MapDomainToResponseAsync(ExchangeOutcome outcome, CancellationToken ct)
    {
        // Extract provider context from the exchange request
        var authHeader = HttpContext.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogWarning("MapDomainToResponseAsync: Missing Authorization header during token generation");
            return Result.Failure<ExchangeTokenResponseDto, Error>(
                Error.Internal("Unable to generate access token: missing authorization context", "EXCHANGE.MISSING_AUTH_CONTEXT"));
        }

        var jwt = authHeader["Bearer ".Length..].Trim();

        // Get raw claims to extract provider information
        var rawClaimsResult = await _dynamicAuthService.GetRawClaimsAsync(jwt, ct);
        if (rawClaimsResult.IsFailure)
        {
            Logger.LogWarning("MapDomainToResponseAsync: Failed to get raw claims for token generation: {Error}", rawClaimsResult.Error.Message);
            return Result.Failure<ExchangeTokenResponseDto, Error>(
                Error.Internal("Unable to generate access token: invalid authorization context", "EXCHANGE.INVALID_AUTH_CONTEXT"));
        }

        var claims = rawClaimsResult.Value;
        var issuer = claims.FindFirst("iss")?.Value ?? "unknown";
        var subject = claims.FindFirst("sub")?.Value ?? "unknown";

        // Create provider type for Dynamic
        var providerTypeResult = ProviderType.Create("dynamic");
        if (providerTypeResult.IsFailure)
        {
            return Result.Failure<ExchangeTokenResponseDto, Error>(providerTypeResult.Error);
        }

        // Generate Axon JWT refresh token (includes access token) using unified service
        var refreshTokenResult = await _authenticationService.GenerateRefreshTokenAsync(
            outcome.AxonUserId,
            providerTypeResult.Value,
            issuer,
            subject,
            ct);

        if (refreshTokenResult.IsFailure)
        {
            Logger.LogError("Failed to generate Axon JWT tokens for AxonUserId={AxonUserId}: {Error}",
                outcome.AxonUserId.Value, refreshTokenResult.Error.Message);
            return Result.Failure<ExchangeTokenResponseDto, Error>(refreshTokenResult.Error);
        }

        var tokens = refreshTokenResult.Value;

        var response = new ExchangeTokenResponseDto(
            AccessToken:            tokens.AccessToken,
            RefreshToken:           tokens.RefreshToken,
            TokenType:              tokens.TokenType,
            ExpiresIn:              tokens.ExpiresIn,
            AxonUserId:             outcome.AxonUserId.ToString(),
            Created:                outcome.Created,
            WalletsProcessed:       outcome.WalletsProcessed,
            WalletsLinked:          outcome.WalletsLinked,
            DefaultsApplied:        outcome.DefaultsApplied,
            Skipped:                outcome.Skipped,
            Conflicts:              outcome.Conflicts,
            IssuedAt:               tokens.IssuedAt,
            AccessTokenExpiresAt:   tokens.AccessTokenExpiresAt,
            RefreshTokenExpiresAt:  tokens.RefreshTokenExpiresAt
        );

        return Result.Success<ExchangeTokenResponseDto, Error>(response);
    }
}
