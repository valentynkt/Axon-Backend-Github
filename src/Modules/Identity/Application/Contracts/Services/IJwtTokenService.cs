using Axon.Modules.Identity.Application.Common;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Http;

namespace Axon.Modules.Identity.Application.Contracts.Services;

/// <summary>
/// Service for JWT token operations (generation, validation, refresh)
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Generates an Axon access token for authenticated user
    /// </summary>
    Task<Result<AxonToken, Error>> GenerateAccessTokenAsync(
        AxonUserId userId,
        ProviderType providerType,
        string providerSubject,
        string issuer,
        int expiryMinutes = 15,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates an Axon access token
    /// </summary>
    Task<Result<AuthenticatedContext, Error>> ValidateAxonTokenAsync(
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates refresh token response with access and refresh tokens
    /// </summary>
    Task<Result<RefreshTokenResponse, Error>> GenerateRefreshTokenAsync(
        AxonUserId userId,
        ProviderType providerType,
        string providerSubject,
        string issuer,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes access token using valid refresh token
    /// </summary>
    Task<Result<RefreshTokenResponse, Error>> RefreshAccessTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    // ExtractBearerToken removed - JWT middleware handles token extraction automatically
    // Access token via HttpContext.User after authentication middleware runs
}