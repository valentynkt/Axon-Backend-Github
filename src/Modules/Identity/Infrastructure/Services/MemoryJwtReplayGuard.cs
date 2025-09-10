using Axon.Modules.Identity.Application.Common.Constants;
using Axon.Modules.Identity.Application.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Memory-based implementation of JWT replay guard using IMemoryCache.
/// Tracks used JWT IDs to prevent replay attacks.
/// For production with multiple instances, consider using a distributed cache like Redis.
/// </summary>
public sealed class MemoryJwtReplayGuard : IJwtReplayGuard
{
    private readonly IMemoryCache _cache;
    private readonly IExchangeMetricsService _metricsService;
    private readonly ILogger<MemoryJwtReplayGuard> _logger;
    private static readonly object LockObject = new();

    public MemoryJwtReplayGuard(
        IMemoryCache cache,
        IExchangeMetricsService metricsService,
        ILogger<MemoryJwtReplayGuard> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<Result<Unit, Error>> CheckAndMarkUsedAsync(
        string jti, 
        DateTimeOffset expiresAt, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            _logger.LogWarning("JWT replay check attempted with empty jti");
            return Task.FromResult(Result.Failure<Unit, Error>(
                Error.Validation("JWT ID (jti) is required for replay protection", DynamicAuthConstants.ErrorCodes.TokenRequired)));
        }

        try
        {
            var cacheKey = $"jwt_used_{jti}";
            
            // Use lock to ensure atomic check-and-set operation
            lock (LockObject)
            {
                // Check if JWT ID is already used
                if (_cache.TryGetValue(cacheKey, out _))
                {
                    _metricsService.RecordReplayAttempt(jti);
                    _logger.LogWarning("JWT replay attempt detected for jti: {Jti}", jti);
                    return Task.FromResult(Result.Failure<Unit, Error>(
                        Error.Unauthorized("JWT token has already been used", DynamicAuthConstants.ErrorCodes.TokenReplayed)));
                }

                // Mark JWT ID as used until token expires
                var cacheExpiration = expiresAt.Subtract(DateTimeOffset.UtcNow);
                
                // Don't cache for negative durations (expired tokens)
                if (cacheExpiration <= TimeSpan.Zero)
                {
                    _logger.LogWarning("Attempt to cache expired JWT with jti: {Jti}", jti);
                    return Task.FromResult(Result.Failure<Unit, Error>(
                        Error.Unauthorized("JWT token has expired", DynamicAuthConstants.ErrorCodes.TokenExpired)));
                }

                // Add buffer time to ensure we don't accept tokens near expiration
                var bufferTime = TimeSpan.FromMinutes(1);
                var effectiveExpiration = cacheExpiration.Add(bufferTime);
                
                _cache.Set(cacheKey, true, effectiveExpiration);
                
                _logger.LogDebug("JWT jti marked as used: {Jti}, expires in: {Duration}", jti, effectiveExpiration);
                return Task.FromResult(Result.Success<Unit, Error>(Unit.Value));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during JWT replay check for jti: {Jti}", jti);
            return Task.FromResult(Result.Failure<Unit, Error>(
                Error.External("JWT replay check failed", "AUTH.REPLAY_CHECK_ERROR", ex)));
        }
    }
}