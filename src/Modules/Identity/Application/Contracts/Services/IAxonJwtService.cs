using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Application.Contracts.Services;

/// <summary>
/// Service interface for Axon JWT token generation and validation operations.
/// Provides tokens for internal API authentication per High-Level Flow Architecture.
/// </summary>
public interface IAxonJwtService
{
    /// <summary>
    /// Generates an Axon JWT access token for authenticated principal
    /// </summary>
    /// <param name="axonUserId">The internal Axon user identifier</param>
    /// <param name="providerType">Authentication provider type (e.g., "dynamic", "siws")</param>
    /// <param name="issuer">Original token issuer</param>
    /// <param name="subject">Original token subject</param>
    /// <param name="expiresIn">Token expiration time in seconds (default: 1 hour)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with JWT access token if successful, error if generation fails</returns>
    Task<Result<AxonJwtToken, Error>> GenerateAccessTokenAsync(
        AxonUserId axonUserId,
        ProviderType providerType,
        string issuer,
        string subject,
        int expiresIn = 3600,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates an Axon JWT access token
    /// </summary>
    /// <param name="token">The JWT token to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with validated token data if successful, error if invalid</returns>
    Task<Result<AxonJwtClaims, Error>> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Axon JWT access token information
/// </summary>
public record AxonJwtToken(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Validated claims from Axon JWT token
/// </summary>
public record AxonJwtClaims(
    AxonUserId AxonUserId,
    ProviderType ProviderType,
    string Issuer,
    string Subject,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);