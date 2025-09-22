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

    public ExchangeEndpoint(
        IMediator mediator,
        ILogger<ExchangeEndpoint> logger,
        IDynamicAuthService dynamicAuthService)
        : base(mediator, logger)
    {
        _dynamicAuthService = dynamicAuthService ?? throw new ArgumentNullException(nameof(dynamicAuthService));
    }

    protected override string GetRoute() => "/api/v1/auth/exchange";
    protected override string GetSummary() => "Exchange Dynamic JWT for Axon identity";
    protected override string GetDescription() =>
        """
        Validates a Dynamic.xyz JWT and creates/updates the Axon principal and wallet links.

        • JWT is supplied via Authorization: Bearer <token>
        • Endpoint is AllowAnonymous (token handled as input data)
        • Idempotent: safe to retry
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
            s.Responses[401] = "Invalid or missing Dynamic JWT";
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
        // 1) Extract JWT from Authorization header
        var authHeader = HttpContext.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) ||
            !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogWarning("Exchange request missing Authorization header or Bearer token");
            return Result.Failure<ExchangeCredentialCommand, Error>(
                Error.Unauthorized("Authorization header with Bearer token is required"));
        }

        var jwt = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(jwt))
        {
            Logger.LogWarning("Exchange request has empty Bearer token");
            return Result.Failure<ExchangeCredentialCommand, Error>(
                Error.Unauthorized("Bearer token cannot be empty"));
        }

        // 2) Validate JWT with Dynamic service (signature, lifetime, issuer/audience)
        var validationResult = await _dynamicAuthService.ValidateTokenAsync(jwt, ct);
        if (validationResult.IsFailure)
        {
            Logger.LogWarning("Dynamic JWT validation failed: {Code} {Message}",
                validationResult.Error.Code, validationResult.Error.Message);

            // Keep mapping simple; application layer will emit precise codes if needed
            return Result.Failure<ExchangeCredentialCommand, Error>(
                Error.Unauthorized("Invalid or expired Dynamic token"));
        }

        var dynamicUser = validationResult.Value;

        // 3) (Optional) extract raw claims for accurate issuer capture
        string? issuer = null;
        var rawClaimsResult = await _dynamicAuthService.GetRawClaimsAsync(jwt, ct);
        if (rawClaimsResult.IsSuccess)
        {
            issuer = rawClaimsResult.Value.FindFirst("iss")?.Value;
        }

        // 4) Map Dynamic data to command input (pure data; no business logic here)
        var exchangeWallets = dynamicUser.Wallets.Select(w => new ExchangeWalletData(
            Address:        w.Address,
            Chain:          w.Chain,
            WalletName:     w.WalletName,
            Provider:       w.Provider,
            ConnectedAtUtc: w.ConnectedAtUtc
        )).ToList();

        var additional = new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(issuer))
            additional[JwtIssuerMetadataKey] = issuer;

        var userData = new ExchangeUserData(
            AxonUserId:       dynamicUser.AxonUserId,
            Email:            dynamicUser.Email,
            EnvironmentId:    dynamicUser.EnvironmentId,
            Wallets:          exchangeWallets,
            FirstVisitUtc:    dynamicUser.FirstVisitUtc,
            LastVisitUtc:     dynamicUser.LastVisitUtc,
            IsNewUser:        dynamicUser.IsNewUser,
            AdditionalMetadata: additional.Count > 0 ? additional : null
        );

        // 5) Prevent caching of auth responses
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers.Pragma = "no-cache";

        return Result.Success<ExchangeCredentialCommand, Error>(new ExchangeCredentialCommand(userData));
    }

    protected override Result<ExchangeTokenResponseDto, Error> MapDomainToResponse(ExchangeOutcome outcome)
    {
        // Pure mapping to API contract; token minting (Axon JWT) is handled elsewhere
        // TODO: Implement JWT token minting service to generate AccessToken
        var response = new ExchangeTokenResponseDto(
            AccessToken:      "TODO_IMPLEMENT_JWT_MINTING", // TODO: Generate actual Axon JWT
            TokenType:        "Bearer",
            ExpiresIn:        3600, // TODO: Use actual token expiration
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
