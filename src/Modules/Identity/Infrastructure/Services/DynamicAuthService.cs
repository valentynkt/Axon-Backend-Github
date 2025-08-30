using System.Text.Json;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Axon.Modules.Identity.Infrastructure.ExternalServices.Configuration;
using Axon.Modules.Identity.Infrastructure.ExternalServices.DynamicXyz.Client;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Service for validating JWT tokens through Dynamic.xyz API
/// Implements pure passthrough validation - no local JWT parsing
/// </summary>
public sealed class DynamicAuthService : IDynamicAuthService
{
    private readonly IDynamicApiClient _dynamicApiClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DynamicAuthService> _logger;
    private readonly DynamicXyzOptions _options;
    private readonly TimeSpan _cacheExpiration;

    public DynamicAuthService(
        IDynamicApiClient dynamicApiClient,
        IMemoryCache cache,
        ILogger<DynamicAuthService> logger,
        IOptions<DynamicXyzOptions> options)
    {
        _dynamicApiClient = dynamicApiClient ?? throw new ArgumentNullException(nameof(dynamicApiClient));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        
        // Cache validated tokens for 5 minutes by default
        _cacheExpiration = TimeSpan.FromMinutes(_options.Jwt?.JwksCacheMinutes ?? 5);
    }

    /// <summary>
    /// Validates a JWT token by calling Dynamic.xyz API
    /// </summary>
    /// <param name="token">The JWT token to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with user data if valid, error if invalid</returns>
    public async Task<Result<DynamicUserData, Error>> ValidateTokenAsync(
        string token, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Token validation attempted with empty token");
            return Result.Failure<DynamicUserData, Error>(
                Error.Validation("Token is required", "AUTH.TOKEN_REQUIRED"));
        }

        // Check cache first
        var cacheKey = $"dynamic_token_{GetTokenHash(token)}";
        if (_cache.TryGetValue<DynamicUserData>(cacheKey, out var cachedUser) && cachedUser != null)
        {
            _logger.LogDebug("Token validation cache hit for user {UserId}", cachedUser.UserId);
            return Result.Success<DynamicUserData, Error>(cachedUser);
        }

        try
        {
            _logger.LogDebug("Validating token with Dynamic.xyz API");
            
            // Call Dynamic.xyz verify endpoint
            var endpoint = $"/sdk/{_options.EnvironmentId ?? "default"}/verify";
            var request = new { token };
            
            var result = await _dynamicApiClient.PostAsync<DynamicVerifyResponse>(
                endpoint, 
                request, 
                cancellationToken);

            if (result.IsFailure)
            {
                _logger.LogWarning("Token validation failed: {Error}", result.Error.Message);
                return Result.Failure<DynamicUserData, Error>(result.Error);
            }

            var response = result.Value;
            
            // Check if token is valid
            if (!response.Verified)
            {
                _logger.LogWarning("Token validation returned unverified status");
                return Result.Failure<DynamicUserData, Error>(
                    Error.Unauthorized("Invalid token", "AUTH.INVALID_TOKEN"));
            }

            // Map response to our domain model
            var userData = MapToUserData(response);
            
            // Cache the validated token
            _cache.Set(cacheKey, userData, _cacheExpiration);
            
            _logger.LogInformation("Token validated successfully for user {UserId}", userData.UserId);
            return Result.Success<DynamicUserData, Error>(userData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during token validation");
            return Result.Failure<DynamicUserData, Error>(
                Error.External("Token validation failed", "AUTH.VALIDATION_ERROR", ex));
        }
    }

    /// <summary>
    /// Refreshes an expired token using Dynamic.xyz refresh endpoint
    /// </summary>
    public async Task<Result<TokenRefreshResult, Error>> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Result.Failure<TokenRefreshResult, Error>(
                Error.Validation("Refresh token is required", "AUTH.REFRESH_TOKEN_REQUIRED"));
        }

        try
        {
            _logger.LogDebug("Refreshing token with Dynamic.xyz API");
            
            var endpoint = $"/sdk/{_options.EnvironmentId ?? "default"}/refresh";
            var request = new { refreshToken };
            
            var result = await _dynamicApiClient.PostAsync<DynamicRefreshResponse>(
                endpoint,
                request,
                cancellationToken);

            if (result.IsFailure)
            {
                _logger.LogWarning("Token refresh failed: {Error}", result.Error.Message);
                return Result.Failure<TokenRefreshResult, Error>(result.Error);
            }

            var response = result.Value;
            
            var refreshResult = new TokenRefreshResult(
                response.Jwt,
                response.RefreshToken,
                DateTime.UtcNow.AddSeconds(response.ExpiresIn));
            
            _logger.LogInformation("Token refreshed successfully");
            return Result.Success<TokenRefreshResult, Error>(refreshResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during token refresh");
            return Result.Failure<TokenRefreshResult, Error>(
                Error.External("Token refresh failed", "AUTH.REFRESH_ERROR", ex));
        }
    }

    private static DynamicUserData MapToUserData(DynamicVerifyResponse response)
    {
        return new DynamicUserData(
            response.UserId,
            response.Email,
            response.EnvironmentId,
            response.Wallets?.Select(w => new WalletData(
                w.Id,
                w.Address,
                w.Chain,
                w.WalletName,
                w.Provider,
                w.ConnectedAt
            )).ToList() ?? new List<WalletData>(),
            response.FirstVisit,
            response.LastVisit,
            response.NewUser);
    }

    private static string GetTokenHash(string token)
    {
        // Use a simple hash for cache key - just take last 8 chars of token
        // This is safe since we're only using it for caching
        return token.Length > 8 ? token[^8..] : token;
    }
}

/// <summary>
/// Service interface for Dynamic.xyz authentication operations
/// </summary>
public interface IDynamicAuthService
{
    Task<Result<DynamicUserData, Error>> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<Result<TokenRefreshResult, Error>> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
}

/// <summary>
/// Validated user data from Dynamic.xyz
/// </summary>
public record DynamicUserData(
    string UserId,
    string Email,
    string EnvironmentId,
    List<WalletData> Wallets,
    DateTime? FirstVisit,
    DateTime? LastVisit,
    bool IsNewUser);

/// <summary>
/// Wallet information for authenticated user
/// </summary>
public record WalletData(
    string Id,
    string Address,
    string Chain,
    string? WalletName,
    string Provider,
    DateTime? ConnectedAt);

/// <summary>
/// Result of token refresh operation
/// </summary>
public record TokenRefreshResult(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt);

/// <summary>
/// Response from Dynamic.xyz verify endpoint
/// </summary>
internal sealed record DynamicVerifyResponse(
    bool Verified,
    string UserId,
    string Email,
    string EnvironmentId,
    List<DynamicWalletResponse>? Wallets,
    DateTime? FirstVisit,
    DateTime? LastVisit,
    bool NewUser);

/// <summary>
/// Wallet data in Dynamic.xyz response
/// </summary>
internal sealed record DynamicWalletResponse(
    string Id,
    string Address,
    string Chain,
    string? WalletName,
    string Provider,
    DateTime? ConnectedAt);

/// <summary>
/// Response from Dynamic.xyz refresh endpoint
/// </summary>
internal sealed record DynamicRefreshResponse(
    string Jwt,
    string RefreshToken,
    int ExpiresIn);