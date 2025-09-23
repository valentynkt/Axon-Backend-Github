namespace Axon.Modules.Identity.Infrastructure.Services;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service for distributed replay protection of JWT tokens using cache
/// Prevents token replay attacks by tracking used JTI (JWT ID) values
/// </summary>
public interface IReplayProtectionService
{
    Task<bool> IsTokenReplayedAsync(string jti, CancellationToken cancellationToken = default);
    Task MarkTokenAsUsedAsync(string jti, TimeSpan expiry, CancellationToken cancellationToken = default);
    Task InvalidateTokenAsync(string jti, CancellationToken cancellationToken = default);
}

/// <summary>
/// Distributed cache-based implementation of replay protection
/// Falls back to memory cache if distributed cache is unavailable
/// </summary>
public sealed class DistributedReplayProtection : IReplayProtectionService
{
    private readonly IDistributedCache? _distributedCache;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<DistributedReplayProtection> _logger;
    private readonly string _keyPrefix = "jwt:replay:";
    private readonly TimeSpan _defaultExpiry = TimeSpan.FromHours(1);

    public DistributedReplayProtection(
        ILogger<DistributedReplayProtection> logger,
        IMemoryCache memoryCache,
        IDistributedCache? distributedCache = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _distributedCache = distributedCache;

        if (_distributedCache == null)
        {
            _logger.LogWarning("No distributed cache configured, falling back to memory cache for replay protection");
        }
    }

    /// <summary>
    /// Check if a token has been used before (replay attack detection)
    /// </summary>
    public async Task<bool> IsTokenReplayedAsync(string jti, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(jti))
        {
            throw new ArgumentNullException(nameof(jti));
        }

        var key = GetCacheKey(jti);

        try
        {
            // Try distributed cache first
            if (_distributedCache != null)
            {
                var cached = await _distributedCache.GetStringAsync(key, cancellationToken);
                if (!string.IsNullOrEmpty(cached))
                {
                    _logger.LogDebug("Token replay detected in distributed cache for JTI: {Jti}", jti);
                    return true;
                }
            }

            // Fallback to memory cache
            if (_memoryCache.TryGetValue(key, out _))
            {
                _logger.LogDebug("Token replay detected in memory cache for JTI: {Jti}", jti);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking token replay for JTI: {Jti}", jti);
            // In case of error, be conservative and assume replay
            return true;
        }
    }

    /// <summary>
    /// Mark a token as used to prevent replay attacks
    /// </summary>
    public async Task MarkTokenAsUsedAsync(string jti, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(jti))
        {
            throw new ArgumentNullException(nameof(jti));
        }

        var key = GetCacheKey(jti);
        var effectiveExpiry = expiry > TimeSpan.Zero ? expiry : _defaultExpiry;

        try
        {
            // Store in distributed cache if available
            if (_distributedCache != null)
            {
                await _distributedCache.SetStringAsync(
                    key,
                    DateTimeOffset.UtcNow.ToString("O"),
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = effectiveExpiry
                    },
                    cancellationToken);

                _logger.LogDebug("Marked token as used in distributed cache. JTI: {Jti}, Expiry: {Expiry}",
                    jti, effectiveExpiry);
            }

            // Also store in memory cache as backup
            _memoryCache.Set(key, DateTimeOffset.UtcNow.ToString("O"), effectiveExpiry);

            _logger.LogDebug("Marked token as used in memory cache. JTI: {Jti}, Expiry: {Expiry}",
                jti, effectiveExpiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking token as used for JTI: {Jti}", jti);
            // Still try to set in memory cache even if distributed cache fails
            try
            {
                _memoryCache.Set(key, DateTimeOffset.UtcNow.ToString("O"), effectiveExpiry);
            }
            catch (Exception memEx)
            {
                _logger.LogError(memEx, "Failed to set token in memory cache for JTI: {Jti}", jti);
                throw;
            }
        }
    }

    /// <summary>
    /// Invalidate a specific token (useful for logout scenarios)
    /// </summary>
    public async Task InvalidateTokenAsync(string jti, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(jti))
        {
            throw new ArgumentNullException(nameof(jti));
        }

        var key = GetCacheKey(jti);

        try
        {
            // Mark as invalidated with a longer expiry to ensure it stays blocked
            var invalidationExpiry = TimeSpan.FromDays(7); // Keep invalidated tokens for a week

            if (_distributedCache != null)
            {
                await _distributedCache.SetStringAsync(
                    key,
                    $"invalidated:{DateTimeOffset.UtcNow:O}",
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = invalidationExpiry
                    },
                    cancellationToken);
            }

            _memoryCache.Set(key, $"invalidated:{DateTimeOffset.UtcNow:O}", invalidationExpiry);

            _logger.LogInformation("Token invalidated for JTI: {Jti}", jti);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating token for JTI: {Jti}", jti);
            throw;
        }
    }

    private string GetCacheKey(string jti) => $"{_keyPrefix}{jti}";
}

/// <summary>
/// Extension methods for registering replay protection services
/// </summary>
public static class ReplayProtectionServiceExtensions
{
    public static IServiceCollection AddReplayProtection(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<IReplayProtectionService, DistributedReplayProtection>();
        return services;
    }

    public static IServiceCollection AddReplayProtectionWithRedis(
        this IServiceCollection services,
        string connectionString)
    {
        _ = connectionString; // Will be used when Redis package is added
        services.AddMemoryCache();
        // Note: Redis cache configuration should be added in the main application startup
        // services.AddStackExchangeRedisCache is not available in this project
        // The distributed cache will use the IDistributedCache registered by the application
        services.AddSingleton<IReplayProtectionService, DistributedReplayProtection>();
        return services;
    }
}