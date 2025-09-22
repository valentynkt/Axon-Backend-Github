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
    private readonly IDynamicAuthService _dynamicAuthService;
    private readonly IAxonJwtService _axonJwtService;
    private readonly IUnifiedBearerTokenValidator _tokenValidator;
    private readonly IAddressNormalizationService _addressNormalizationService;

    public ExchangeEndpoint(
        IMediator mediator,
        ILogger<ExchangeEndpoint> logger,
        IDynamicAuthService dynamicAuthService,
        IAxonJwtService axonJwtService,
        IUnifiedBearerTokenValidator tokenValidator,
        IAddressNormalizationService addressNormalizationService)
        : base(mediator, logger)
    {
        _dynamicAuthService = dynamicAuthService ?? throw new ArgumentNullException(nameof(dynamicAuthService));
        _axonJwtService = axonJwtService ?? throw new ArgumentNullException(nameof(axonJwtService));
        _tokenValidator = tokenValidator ?? throw new ArgumentNullException(nameof(tokenValidator));
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
        // 1) Extract bearer token from Authorization header
        var authHeader = HttpContext.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) ||
            !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogWarning("Exchange request missing Authorization header or Bearer token");
            return Result.Failure<ExchangeCredentialCommand, Error>(
                Error.Unauthorized("Authorization header with Bearer token is required"));
        }

        var bearerToken = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            Logger.LogWarning("Exchange request has empty Bearer token");
            return Result.Failure<ExchangeCredentialCommand, Error>(
                Error.Unauthorized("Bearer token cannot be empty"));
        }

        // 2) Validate token using unified validator (supports both Dynamic JWT and Axon Access Token)
        var tokenValidationResult = await _tokenValidator.ValidateTokenAsync(bearerToken, ct);
        if (tokenValidationResult.IsFailure)
        {
            Logger.LogWarning("Bearer token validation failed: {Code} {Message}",
                tokenValidationResult.Error.Code, tokenValidationResult.Error.Message);
            return Result.Failure<ExchangeCredentialCommand, Error>(tokenValidationResult.Error);
        }

        var tokenContext = tokenValidationResult.Value;

        // 3) Handle different token types
        ExchangeUserData userData;
        if (tokenContext.TokenType == TokenType.DynamicJwt)
        {
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
                [JwtIssuerMetadataKey] = tokenContext.Issuer
            };

            // TODO: NetworkEnvironment determination strategy needed
            // Dynamic's EnvironmentId (e.g., UUID) ≠ NetworkEnvironment (mainnet/devnet/testnet)
            // Current approach passes Dynamic's EnvironmentId through, but handlers need
            // to determine appropriate NetworkEnvironment for wallet triple-key lookup
            userData = new ExchangeUserData(
                AxonUserId:       dynamicUser.AxonUserId,
                Email:            dynamicUser.Email,
                EnvironmentId:    dynamicUser.EnvironmentId, // TODO: Consider NetworkEnvironment mapping
                Wallets:          exchangeWallets,
                FirstVisitUtc:    dynamicUser.FirstVisitUtc,
                LastVisitUtc:     dynamicUser.LastVisitUtc,
                IsNewUser:        dynamicUser.IsNewUser,
                AdditionalMetadata: additional
            );
        }
        else
        {
            // For Axon Access Tokens, create minimal user data for identity refresh
            var additional = new Dictionary<string, object>
            {
                [JwtIssuerMetadataKey] = tokenContext.Issuer
            };

            // TODO: Determine NetworkEnvironment strategy
            // Currently defaults to mainnet, but should be properly determined based on:
            // - User preference stored in database, or
            // - Request parameter (requires updating DTO), or
            // - Mapping from Dynamic's EnvironmentId (if applicable), or
            // - Context-based detection (production deployment → mainnet)
            // Note: EnvironmentId ≠ NetworkEnvironment (different concepts)
            userData = new ExchangeUserData(
                AxonUserId:       tokenContext.AxonUserId,
                Email:            "", // Not available from Axon tokens
                EnvironmentId:    "mainnet", // TODO: Replace with proper NetworkEnvironment determination
                Wallets:          new List<ExchangeWalletData>(), // No wallet updates for refresh
                FirstVisitUtc:    null,
                LastVisitUtc:     DateTimeOffset.UtcNow,
                IsNewUser:        false, // Always false for refresh
                AdditionalMetadata: additional
            );
        }

        // 4) Prevent caching of auth responses
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers.Pragma = "no-cache";

        return Result.Success<ExchangeCredentialCommand, Error>(new ExchangeCredentialCommand(userData));
    }

    protected override Result<ExchangeTokenResponseDto, Error> MapDomainToResponse(ExchangeOutcome outcome)
    {
        // Extract provider context from the exchange request
        var authHeader = HttpContext.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogWarning("MapDomainToResponse: Missing Authorization header during token generation");
            return Result.Failure<ExchangeTokenResponseDto, Error>(
                Error.Internal("Unable to generate access token: missing authorization context", "EXCHANGE.MISSING_AUTH_CONTEXT"));
        }

        var jwt = authHeader["Bearer ".Length..].Trim();

        // Get raw claims to extract provider information
        var rawClaimsResult = _dynamicAuthService.GetRawClaimsAsync(jwt, CancellationToken.None).GetAwaiter().GetResult();
        if (rawClaimsResult.IsFailure)
        {
            Logger.LogWarning("MapDomainToResponse: Failed to get raw claims for token generation: {Error}", rawClaimsResult.Error.Message);
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

        // Generate Axon JWT access token
        var tokenResult = _axonJwtService.GenerateAccessTokenAsync(
            outcome.AxonUserId,
            providerTypeResult.Value,
            issuer,
            subject,
            expiresIn: 3600,
            CancellationToken.None).GetAwaiter().GetResult();

        if (tokenResult.IsFailure)
        {
            Logger.LogError("Failed to generate Axon JWT token for AxonUserId={AxonUserId}: {Error}",
                outcome.AxonUserId.Value, tokenResult.Error.Message);
            return Result.Failure<ExchangeTokenResponseDto, Error>(tokenResult.Error);
        }

        var axonToken = tokenResult.Value;

        var response = new ExchangeTokenResponseDto(
            AccessToken:      axonToken.AccessToken,
            TokenType:        axonToken.TokenType,
            ExpiresIn:        axonToken.ExpiresIn,
            AxonUserId:       outcome.AxonUserId.ToString(),
            Created:          outcome.Created,
            WalletsProcessed: outcome.WalletsProcessed,
            WalletsLinked:    outcome.WalletsLinked,
            DefaultsApplied:  outcome.DefaultsApplied,
            Skipped:          outcome.Skipped,
            Conflicts:        outcome.Conflicts
        );

        return Result.Success<ExchangeTokenResponseDto, Error>(response);
    }
}
