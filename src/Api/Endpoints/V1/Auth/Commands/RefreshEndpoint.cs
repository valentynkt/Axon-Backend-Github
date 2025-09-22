using Axon.Api.Contracts.V1.Auth;
using Axon.Api.Modules;
using Axon.Modules.Identity.Application.Commands.RefreshToken;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;

namespace Axon.Api.Endpoints.V1.Auth.Commands;

/// <summary>
/// POST /auth/refresh - Refresh access token using refresh token
/// Uses unified FastEndpoints pattern with CQRS command handling
/// </summary>
public sealed class RefreshEndpoint
    : BaseIdentityCommandEndpoint<
        RefreshTokenRequestDto,
        RefreshTokenResponseDto,
        RefreshTokenCommand,
        RefreshTokenResult>
{
    public RefreshEndpoint(IMediator mediator, ILogger<RefreshEndpoint> logger)
        : base(mediator, logger)
    {
    }

    public override void Configure()
    {
        base.Configure();

        // Apply rate limiting for refresh endpoint
        Options(x => x.RequireRateLimiting("AuthExchange"));

        // Document responses succinctly
        Summary(s =>
        {
            s.Summary = GetSummary();
            s.Description = GetDescription();
            s.Responses[200] = GetSuccessResponse();
            s.Responses[400] = "Invalid request parameters";
            s.Responses[401] = "Invalid or expired refresh token";
            s.Responses[429] = "Too many requests";
            s.Responses[500] = "Internal server error";
        });
    }

    protected override string GetRoute() => "/api/v1/auth/refresh";
    protected override string GetSummary() => "Refresh access token using refresh token";
    protected override string GetDescription() =>
        """
        Refreshes an expired access token using a valid refresh token.

        **Behavior**:
        • Validates the provided refresh token
        • Generates a new access token with 15-minute expiration
        • Generates a new refresh token with 30-day expiration
        • Revokes the old refresh token for security
        • Rate Limited: 10 requests per minute per IP
        """;
    protected override string GetSuccessResponse() => "New token pair generated successfully";

    protected override Task<Result<RefreshTokenCommand, Error>> ExecuteCommand(
        RefreshTokenRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            Logger.LogWarning("Refresh request missing refresh token");
            return Task.FromResult(Result.Failure<RefreshTokenCommand, Error>(
                Error.Validation("Refresh token is required", "AUTH.TOKEN_REQUIRED")));
        }

        var command = new RefreshTokenCommand(request.RefreshToken);
        return Task.FromResult(Result.Success<RefreshTokenCommand, Error>(command));
    }

    protected override Result<RefreshTokenResponseDto, Error> MapDomainToResponse(RefreshTokenResult result)
    {
        var response = new RefreshTokenResponseDto
        {
            Success = true,
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken,
            TokenType = "Bearer",
            ExpiresIn = (int)(result.ExpiresAt - DateTime.UtcNow).TotalSeconds,
            IssuedAt = DateTimeOffset.UtcNow,
            AccessTokenExpiresAt = result.ExpiresAt,
            RefreshTokenExpiresAt = result.RefreshExpiresAt
        };

        return Result.Success<RefreshTokenResponseDto, Error>(response);
    }
}