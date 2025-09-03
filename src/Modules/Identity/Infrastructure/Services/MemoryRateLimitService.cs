using Axon.Modules.Identity.Application.Services;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Memory-based rate limiting service using sliding window approach.
/// For production with multiple instances, consider using Redis for distributed rate limiting.
/// </summary>
public sealed class MemoryRateLimitService : IRateLimitService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<MemoryRateLimitService> _logger;
    
    // Rate limit configuration - could be moved to options
    private readonly Dictionary<string, RateLimitConfig> _configs = new()
    {
        ["jwt_exchange"] = new RateLimitConfig(MaxRequests: 10, WindowMinutes: 1),
        ["default"] = new RateLimitConfig(MaxRequests: 60, WindowMinutes: 1)
    };

    public MemoryRateLimitService(
        IMemoryCache cache,
        ILogger<MemoryRateLimitService> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<Result<Unit, RateLimitError>> CheckRateLimitAsync(
        string identifier, 
        string operation, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            _logger.LogWarning("Rate limit check with empty identifier");
            return Task.FromResult(Result.Success<Unit, RateLimitError>(Unit.Value));
        }

        var config = _configs.GetValueOrDefault(operation, _configs["default"]);
        var cacheKey = $"rate_limit:{operation}:{identifier}";
        
        var now = DateTimeOffset.UtcNow;
        var windowStart = now.AddMinutes(-config.WindowMinutes);
        var windowEnd = now.AddMinutes(config.WindowMinutes);

        // Get or create request window
        var window = _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(config.WindowMinutes * 2);
            return new RequestWindow();
        })!;

        lock (window)
        {
            // Clean old requests outside the sliding window
            window.CleanOldRequests(windowStart);
            
            // Check if we're over the limit
            if (window.RequestCount >= config.MaxRequests)
            {
                var oldestRequest = window.GetOldestRequestTime();
                var retryAfter = oldestRequest?.AddMinutes(config.WindowMinutes).Subtract(now) ?? TimeSpan.FromMinutes(1);
                var retryAfterSeconds = Math.Max(1, (int)retryAfter.TotalSeconds);
                
                _logger.LogWarning("Rate limit exceeded for {Identifier} on operation {Operation}: {RequestCount}/{MaxRequests}", 
                    identifier, operation, window.RequestCount, config.MaxRequests);

                var error = new RateLimitError(
                    RetryAfterSeconds: retryAfterSeconds,
                    RequestsRemaining: 0,
                    WindowResetAt: oldestRequest?.AddMinutes(config.WindowMinutes) ?? now.AddMinutes(1)
                );
                
                return Task.FromResult(Result.Failure<Unit, RateLimitError>(error));
            }

            // Add current request
            window.AddRequest(now);
            var remaining = config.MaxRequests - window.RequestCount;
            
            _logger.LogDebug("Rate limit check passed for {Identifier} on operation {Operation}: {RequestCount}/{MaxRequests}, {Remaining} remaining", 
                identifier, operation, window.RequestCount, config.MaxRequests, remaining);

            return Task.FromResult(Result.Success<Unit, RateLimitError>(Unit.Value));
        }
    }

    /// <summary>
    /// Configuration for rate limiting
    /// </summary>
    private sealed record RateLimitConfig(int MaxRequests, double WindowMinutes);

    /// <summary>
    /// Sliding window for tracking requests
    /// </summary>
    private sealed class RequestWindow
    {
        private readonly List<DateTimeOffset> _requests = new();
        
        public RequestWindow()
        {
            // Initialize empty request window
        }

        public int RequestCount => _requests.Count;

        public void AddRequest(DateTimeOffset timestamp)
        {
            _requests.Add(timestamp);
        }

        public void CleanOldRequests(DateTimeOffset cutoffTime)
        {
            _requests.RemoveAll(r => r < cutoffTime);
        }

        public DateTimeOffset? GetOldestRequestTime()
        {
            return _requests.Count > 0 ? _requests.Min() : null;
        }
    }
}