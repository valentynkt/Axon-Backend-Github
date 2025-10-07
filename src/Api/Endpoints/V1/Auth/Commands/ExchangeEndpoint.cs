using Axon.Api.Contracts.V1.Auth;
using Axon.Modules.Identity.Application.Commands.ExchangeCredential;
using Axon.Modules.Identity.Application.DTOs.Exchange;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Web.Endpoints.Base;
using CSharpFunctionalExtensions;
using FastEndpoints;
using MediatR;

namespace Axon.Api.Endpoints.V1.Auth;

/// <summary>
/// POST /auth/exchange - Exchange Dynamic JWT for Axon identity
/// NOTE: JWT is provided via Authorization: Bearer <token>. Endpoint is AllowAnonymous
/// and performs validation manually (does not rely on ASP.NET auth pipeline).
/// </summary>
public sealed class ExchangeEndpoint : BaseResultEndpoint<ExchangeTokenRequestDto, AuthTokenResponseDto>
{
    private readonly IMediator _mediator;

    public ExchangeEndpoint(IMediator mediator, ILogger<ExchangeEndpoint> logger)
        : base(logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public override void Configure()
    {
        Post("/api/v1/auth/exchange");
        AllowAnonymous();

        // Apply rate limiting policy for exchange endpoint
        Options(x => x.RequireRateLimiting("AuthExchange"));

        // Document responses succinctly
        Summary(s =>
        {
            s.Summary = "Exchange bearer token for Axon identity and access token";
            s.Description = """
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
            s.Responses[200] = "Returns exchange outcome with wallet processing metrics";
            s.Responses[400] = "Invalid request parameters";
            s.Responses[401] = "Invalid or missing bearer token";
            s.Responses[409] = "Ownership conflict (wallet already owned by another principal)";
            s.Responses[422] = "Business rule violation";
            s.Responses[429] = "Too many requests";
            s.Responses[500] = "Internal server error";
        });

        Tags("Authentication");
    }

    protected override async Task<Result<AuthTokenResponseDto, Error>> ExecuteAsync(
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
            return Result.Failure<AuthTokenResponseDto, Error>(
                Error.Unauthorized("Missing or invalid Authorization header", "AUTH.MISSING_TOKEN"));
        }

        var bearerToken = authHeader["Bearer ".Length..].Trim();

        // Prevent caching of auth responses
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers.Pragma = "no-cache";

        // Pass the raw token to the command - let the handler do EVERYTHING else
        var command = new ExchangeCredentialCommand(bearerToken);
        var domainResult = await _mediator.Send(command, ct);
        if (domainResult.IsFailure)
            return Result.Failure<AuthTokenResponseDto, Error>(domainResult.Error);

        // Simply map the outcome to response DTO - the outcome already contains everything we need
        var response = new AuthTokenResponseDto(
            AccessToken:        domainResult.Value.AccessToken,
            RefreshToken:       domainResult.Value.RefreshToken,
            TokenType:          domainResult.Value.TokenType,
            ExpiresIn:          domainResult.Value.ExpiresIn,
            AxonUserId:         domainResult.Value.AxonUserId.ToString(),
            Created:            domainResult.Value.Created,
            WalletsLinked:      domainResult.Value.WalletsLinked,
            Conflicts:          domainResult.Value.Conflicts
        );

        return Result.Success<AuthTokenResponseDto, Error>(response);
    }
}
