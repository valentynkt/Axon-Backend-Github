using Axon.Api.Contracts.V1.Auth;
using Axon.Modules.Identity.Application.Commands.RefreshToken;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Web.Endpoints.Base;
using CSharpFunctionalExtensions;
using FastEndpoints;
using MediatR;

namespace Axon.Api.Endpoints.V1.Auth.Commands;

/// <summary>
/// POST /auth/refresh - Refresh access token using refresh token
/// Uses unified FastEndpoints pattern with CQRS command handling
/// </summary>
public sealed class RefreshEndpoint : BaseResultEndpoint<RefreshTokenRequestDto, RefreshTokenResponseDto>
{
    private readonly IMediator _mediator;

    public RefreshEndpoint(IMediator mediator, ILogger<RefreshEndpoint> logger)
        : base(logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public override void Configure()
    {
        Post("/api/v1/auth/refresh");
        AllowAnonymous();

        // Apply rate limiting for refresh endpoint
        Options(x => x.RequireRateLimiting("AuthExchange"));

        // Document responses succinctly
        Summary(s =>
        {
            s.Summary = "Refresh access token using refresh token";
            s.Description = """
                Refreshes an expired access token using a valid refresh token.

                **Behavior**:
                • Validates the provided refresh token
                • Generates a new access token with 15-minute expiration
                • Generates a new refresh token with 30-day expiration
                • Revokes the old refresh token for security
                • Rate Limited: 10 requests per minute per IP
                """;
            s.Responses[200] = "New token pair generated successfully";
            s.Responses[400] = "Invalid request parameters";
            s.Responses[401] = "Invalid or expired refresh token";
            s.Responses[429] = "Too many requests";
            s.Responses[500] = "Internal server error";
        });

        Tags("Authentication");
    }

    protected override async Task<Result<RefreshTokenResponseDto, Error>> ExecuteAsync(
        RefreshTokenRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            Logger.LogWarning("Refresh request missing refresh token");
            return Result.Failure<RefreshTokenResponseDto, Error>(
                Error.Validation("Refresh token is required", "AUTH.TOKEN_REQUIRED"));
        }

        // Prevent caching of auth responses
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers.Pragma = "no-cache";

        var command = new RefreshTokenCommand(request.RefreshToken);
        var domainResult = await _mediator.Send(command, ct);
        if (domainResult.IsFailure)
            return Result.Failure<RefreshTokenResponseDto, Error>(domainResult.Error);

        // Calculate ExpiresIn from domain result timestamps to ensure consistency
        var now = DateTimeOffset.UtcNow;
        var expiresIn = Math.Max(0, (int)(domainResult.Value.ExpiresAt - now).TotalSeconds);

        var response = new RefreshTokenResponseDto
        {
            Success = true,
            AccessToken = domainResult.Value.AccessToken,
            RefreshToken = domainResult.Value.RefreshToken,
            TokenType = "Bearer",
            ExpiresIn = expiresIn,
            IssuedAt = now,
            AccessTokenExpiresAt = domainResult.Value.ExpiresAt,
            RefreshTokenExpiresAt = domainResult.Value.RefreshExpiresAt
        };

        return Result.Success<RefreshTokenResponseDto, Error>(response);
    }
}
