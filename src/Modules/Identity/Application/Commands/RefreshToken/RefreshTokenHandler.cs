using Axon.Modules.Identity.Application.Contracts.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Application.Commands.RefreshToken;

/// <summary>
/// Handler for refresh token operations
/// </summary>
public class RefreshTokenHandler : IRequestHandler<RefreshTokenCommand, Result<RefreshTokenResult, Error>>
{
    private readonly IAuthenticationOrchestrator _orchestrator;
    private readonly ILogger<RefreshTokenHandler> _logger;

    public RefreshTokenHandler(
        IAuthenticationOrchestrator orchestrator,
        ILogger<RefreshTokenHandler> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<RefreshTokenResult, Error>> Handle(
        RefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            _logger.LogWarning("Refresh token command received with empty refresh token");
            return Result.Failure<RefreshTokenResult, Error>(
                Error.Validation("Refresh token is required", "AUTH.TOKEN_REQUIRED"));
        }

        _logger.LogDebug("Processing refresh token request");

        var refreshResult = await _orchestrator.RefreshTokenAsync(
            request.RefreshToken,
            cancellationToken);

        if (refreshResult.IsFailure)
        {
            _logger.LogWarning("Failed to refresh access token: {Error}", refreshResult.Error.Message);
            return Result.Failure<RefreshTokenResult, Error>(refreshResult.Error);
        }

        var response = refreshResult.Value;
        var result = new RefreshTokenResult(
            AccessToken: response.AccessToken,
            RefreshToken: response.RefreshToken,
            ExpiresAt: response.AccessTokenExpiresAt.DateTime,
            RefreshExpiresAt: response.RefreshTokenExpiresAt.DateTime);

        _logger.LogDebug("Successfully refreshed access token");
        return Result.Success<RefreshTokenResult, Error>(result);
    }
}