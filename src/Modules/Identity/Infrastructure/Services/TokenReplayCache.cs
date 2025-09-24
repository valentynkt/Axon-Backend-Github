using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Unified token replay protection service using Microsoft's built-in caching infrastructure.
/// Implements ITokenReplayCache for JWT Bearer integration and provides nonce tracking for challenges.
/// </summary>
public sealed class TokenReplayCache : ITokenReplayCache
{
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<TokenReplayCache> _logger;

    public TokenReplayCache(
        IDistributedCache distributedCache,
        ILogger<TokenReplayCache> logger)
    {
        _distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Microsoft's ITokenReplayCache interface implementation for JWT Bearer authentication (synchronous)
    /// </summary>
    public bool TryAdd(string securityToken, DateTime expiresOn)
    {
        ArgumentNullException.ThrowIfNull(securityToken);

        return TryAddInternalAsync(securityToken, expiresOn, "jwt").GetAwaiter().GetResult();
    }

    /// <summary>
    /// Microsoft's ITokenReplayCache interface implementation for JWT Bearer authentication (synchronous)
    /// </summary>
    public bool TryFind(string securityToken)
    {
        ArgumentNullException.ThrowIfNull(securityToken);

        return TryFindInternalAsync(securityToken, "jwt").GetAwaiter().GetResult();
    }

    /// <summary>
    /// Microsoft's ITokenReplayCache interface implementation for JWT Bearer authentication (asynchronous)
    /// </summary>
    public Task<bool> TryAddAsync(string securityToken, DateTime expiresOn)
    {
        ArgumentNullException.ThrowIfNull(securityToken);

        return TryAddInternalAsync(securityToken, expiresOn, "jwt");
    }

    /// <summary>
    /// Microsoft's ITokenReplayCache interface implementation for JWT Bearer authentication (asynchronous)
    /// </summary>
    public Task<bool> TryFindAsync(string securityToken)
    {
        ArgumentNullException.ThrowIfNull(securityToken);

        return TryFindInternalAsync(securityToken, "jwt");
    }

    /// <summary>
    /// Add a nonce to the replay protection cache for challenge-response flows
    /// </summary>
    public async Task<bool> TryAddNonceAsync(string nonce, TimeSpan expiration)
    {
        ArgumentNullException.ThrowIfNull(nonce);

        var expiresOn = DateTime.UtcNow.Add(expiration);
        return await TryAddInternalAsync(nonce, expiresOn, "nonce");
    }

    /// <summary>
    /// Check if a nonce has already been used in challenge-response flows
    /// </summary>
    public async Task<bool> TryFindNonceAsync(string nonce)
    {
        ArgumentNullException.ThrowIfNull(nonce);

        return await TryFindInternalAsync(nonce, "nonce");
    }

    /// <summary>
    /// Add a JWT token ID (jti) to the replay protection cache
    /// </summary>
    public async Task<bool> TryAddJtiAsync(string jti, DateTime expiresOn)
    {
        ArgumentNullException.ThrowIfNull(jti);

        return await TryAddInternalAsync(jti, expiresOn, "jti");
    }

    /// <summary>
    /// Check if a JWT token ID (jti) has already been used
    /// </summary>
    public async Task<bool> TryFindJtiAsync(string jti)
    {
        ArgumentNullException.ThrowIfNull(jti);

        return await TryFindInternalAsync(jti, "jti");
    }

    /// <summary>
    /// Internal implementation for adding tokens/nonces to the cache with automatic expiration
    /// </summary>
    private async Task<bool> TryAddInternalAsync(string token, DateTime expiresOn, string tokenType)
    {
        try
        {
            var cacheKey = $"replay:{tokenType}:{ComputeHash(token)}";

            // Check if already exists
            var existing = await _distributedCache.GetStringAsync(cacheKey);
            if (existing != null)
            {
                _logger.LogDebug("Token replay detected for {TokenType}: {TokenHash}", tokenType, ComputeHash(token));
                return false;
            }

            // Calculate expiration with buffer
            var expiration = expiresOn > DateTime.UtcNow
                ? expiresOn.Subtract(DateTime.UtcNow).Add(TimeSpan.FromMinutes(5))
                : TimeSpan.FromMinutes(5);

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };

            await _distributedCache.SetStringAsync(cacheKey, "used", options);

            _logger.LogDebug("Added {TokenType} to replay cache with expiration {Expiration}",
                tokenType, expiration);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add {TokenType} to replay cache", tokenType);
            // In case of cache failure, allow the request to proceed but log the error
            return true;
        }
    }

    /// <summary>
    /// Internal implementation for finding tokens/nonces in the cache
    /// </summary>
    private async Task<bool> TryFindInternalAsync(string token, string tokenType)
    {
        try
        {
            var cacheKey = $"replay:{tokenType}:{ComputeHash(token)}";
            var existing = await _distributedCache.GetStringAsync(cacheKey);

            var found = existing != null;
            if (found)
            {
                _logger.LogDebug("Found {TokenType} in replay cache: {TokenHash}", tokenType, ComputeHash(token));
            }

            return found;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check {TokenType} in replay cache", tokenType);
            // In case of cache failure, assume not found to allow the request
            return false;
        }
    }

    /// <summary>
    /// Compute a secure hash of the token for cache key generation (avoiding storing full tokens)
    /// </summary>
    private static string ComputeHash(string input)
    {
        var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hashBytes)[..16]; // Use first 16 chars for compact keys
    }
}