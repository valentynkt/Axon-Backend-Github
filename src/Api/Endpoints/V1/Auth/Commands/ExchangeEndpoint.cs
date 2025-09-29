using Axon.Api.Contracts.V1.Auth;
using Axon.Api.Modules;
using Axon.Modules.Identity.Application.Commands.ExchangeCredential;
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
        AuthTokenResponseDto,
        ExchangeCredentialCommand,
        ExchangeOutcome>
{
    public ExchangeEndpoint(IMediator mediator, ILogger<ExchangeEndpoint> logger)
        : base(mediator, logger)
    {
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

    protected override Task<Result<ExchangeCredentialCommand, Error>> ExecuteCommand(
        ExchangeTokenRequestDto _,
        CancellationToken ct)
    {
        // Log that this endpoint bypasses middleware JWT validation
        Logger.LogDebug("Exchange endpoint processing - JWT validation handled by provider (middleware bypassed)");

        // Verify middleware was indeed bypassed by checking HttpContext.User
        if (HttpContext.User.Identity?.IsAuthenticated == true)
        {
            Logger.LogWarning("Exchange endpoint received authenticated user - this suggests middleware wasn't bypassed. User: {@User}",
                new { Claims = HttpContext.User.Claims.Select(c => new { c.Type, c.Value }) });
        }

        // Extract bearer token from Authorization header - that's ALL we do here
        var authHeader = HttpContext.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogWarning("Missing or invalid Authorization header");
            return Task.FromResult(Result.Failure<ExchangeCredentialCommand, Error>(
                Error.Unauthorized("Missing or invalid Authorization header", "AUTH.MISSING_TOKEN")));
        }

        var bearerToken = authHeader["Bearer ".Length..].Trim();

        // Prevent caching of auth responses
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers.Pragma = "no-cache";

        // Pass the raw token to the command - let the handler do EVERYTHING else
        var command = new ExchangeCredentialCommand(bearerToken);
        return Task.FromResult(Result.Success<ExchangeCredentialCommand, Error>(command));
    }

    protected override Task<Result<AuthTokenResponseDto, Error>> MapDomainToResponseAsync(ExchangeOutcome outcome, CancellationToken ct)
    {
        // Simply map the outcome to response DTO - the outcome already contains everything we need
        var response = new AuthTokenResponseDto(
            AccessToken:        outcome.AccessToken,
            TokenType:          outcome.TokenType,
            ExpiresIn:          outcome.ExpiresIn,
            AxonUserId:         outcome.AxonUserId.ToString(),
            Created:            outcome.Created,
            WalletsLinked:      outcome.WalletsLinked,
            Conflicts:          outcome.Conflicts
        );

        return Task.FromResult(Result.Success<AuthTokenResponseDto, Error>(response));
    }

}
