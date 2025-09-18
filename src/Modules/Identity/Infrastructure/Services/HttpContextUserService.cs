using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Claims;
using Axon.Modules.Identity.Application.Contracts.Persistence;
using Axon.Modules.Identity.Domain.ValueObjects;
using BuildingBlocks.Core.Abstractions.Authentication;
using BuildingBlocks.Primitives.Ids;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Axon.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Implementation of ICurrentUserService that reads user information from HTTP context claims
/// This service extracts user data from the authenticated principal created by DynamicXyzAuthHandler
/// </summary>
public sealed class HttpContextUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMemoryCache _memoryCache;
    private readonly IAxonPrincipalReadRepository _principalRepository;
    private readonly ILogger<HttpContextUserService> _logger;

    // OpenTelemetry metrics for identity resolution performance
    private static readonly Meter Meter = new("Axon.Identity", "1.0");
    private static readonly Counter<long> CacheHitCounter =
        Meter.CreateCounter<long>("axon.identity.cache_hits", description: "Identity cache hits by source");
    private static readonly Counter<long> CacheMissCounter =
        Meter.CreateCounter<long>("axon.identity.cache_misses", description: "Identity cache misses by source");
    private static readonly Histogram<double> ResolutionLatency =
        Meter.CreateHistogram<double>("axon.identity.resolution_duration_ms", unit: "ms", description: "Identity resolution latency");

    public HttpContextUserService(
        IHttpContextAccessor httpContextAccessor,
        IMemoryCache memoryCache,
        IAxonPrincipalReadRepository principalRepository,
        ILogger<HttpContextUserService> logger)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _principalRepository = principalRepository ?? throw new ArgumentNullException(nameof(principalRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the current authenticated user's unique identifier from claims
    /// </summary>
    public string? UserId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            // Try to get user ID from standard ClaimTypes.NameIdentifier
            var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier);
            return userIdClaim?.Value;
        }
    }

    /// <summary>
    /// Gets the current authenticated user's display name (email in Dynamic.xyz case)
    /// </summary>
    public string? UserName
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            // Try email first, then name claim
            var emailClaim = httpContext.User.FindFirst(ClaimTypes.Email);
            if (emailClaim != null)
            {
                return emailClaim.Value;
            }

            var nameClaim = httpContext.User.FindFirst(ClaimTypes.Name);
            return nameClaim?.Value;
        }
    }

    /// <summary>
    /// Indicates whether a user is currently authenticated
    /// </summary>
    public bool IsAuthenticated
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            return httpContext?.User.Identity?.IsAuthenticated == true;
        }
    }

    /// <summary>
    /// Gets the current user ID or returns a default system user identifier
    /// </summary>
    public string GetUserIdOrDefault(string systemUserId = "SYSTEM")
    {
        return UserId ?? systemUserId;
    }

    /// <summary>
    /// Gets the current user ID or returns "SYSTEM" for system operations
    /// </summary>
    public string GetCurrentUserIdOrSystem()
    {
        return UserId ?? "SYSTEM";
    }
    
    /// <summary>
    /// Gets additional user claims for extended functionality
    /// </summary>
    public IEnumerable<Claim> GetUserClaims()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User.Identity?.IsAuthenticated != true)
        {
            return Enumerable.Empty<Claim>();
        }

        return httpContext.User.Claims;
    }
    
    /// <summary>
    /// Gets user's wallet addresses from claims
    /// </summary>
    public IEnumerable<string> GetUserWallets()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User.Identity?.IsAuthenticated != true)
        {
            return Enumerable.Empty<string>();
        }

        return httpContext.User.Claims
            .Where(c => c.Type == "wallet")
            .Select(c => c.Value)
            .Distinct();
    }
    
    /// <summary>
    /// Checks if the current user is a new user
    /// </summary>
    public bool IsNewUser()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var newUserClaim = httpContext.User.FindFirst("is_new_user");
        return newUserClaim != null && bool.TryParse(newUserClaim.Value, out var isNew) && isNew;
    }

    /// <summary>
    /// Gets the current authenticated user's internal AxonUserId with smart caching.
    /// Progressive cache hierarchy: HttpContext.Items (0ms) → IMemoryCache (&lt;1ms) → Database (20-50ms)
    /// Cache key pattern: "AxonUserId" in HttpContext.Items and "axon:user:{dynamicUserId}" in IMemoryCache
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for database fallback operations</param>
    /// <returns>AxonUserId if user is authenticated and found, null otherwise</returns>
    public async Task<AxonUserId?> GetAxonUserIdAsync(CancellationToken cancellationToken = default)
    {
        using var activity = Activity.Current?.Source.StartActivity("GetAxonUserIdAsync");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Layer 1: Request-scoped cache (0ms)
            if (_httpContextAccessor.HttpContext?.Items.TryGetValue("AxonUserId", out var cached) == true && cached is AxonUserId cachedUserId)
            {
                _logger.LogDebug("AxonUserId cache hit (request-scoped)");
                CacheHitCounter.Add(1, new KeyValuePair<string, object?>("source", "request"));
                activity?.SetTag("cache_source", "request");
                activity?.SetTag("cache_hit", true);

                stopwatch.Stop();
                ResolutionLatency.Record(stopwatch.Elapsed.TotalMilliseconds,
                    new KeyValuePair<string, object?>("source", "request"));

                return cachedUserId;
            }

            var dynamicUserId = UserId;
            if (string.IsNullOrEmpty(dynamicUserId))
            {
                _logger.LogDebug("No authenticated user found");
                activity?.SetTag("authenticated", false);
                return null;
            }

            activity?.SetTag("dynamic_user_id", dynamicUserId);
            activity?.SetTag("authenticated", true);

            // Layer 2: Memory cache (<1ms) - NEW for Story 4b.3
            var cacheKey = $"axon:user:{dynamicUserId}";
            var axonUserId = await _memoryCache.GetOrCreateAsync(
                cacheKey,
                async entry =>
                {
                    _logger.LogDebug("AxonUserId memory cache miss, fetching from database for {DynamicUserId}", dynamicUserId);
                    CacheMissCounter.Add(1, new KeyValuePair<string, object?>("source", "memory"));
                    activity?.SetTag("cache_source", "database");

                    // Configure cache entry options (Task 2 requirements)
                    entry.SlidingExpiration = TimeSpan.FromMinutes(15);
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
                    entry.Priority = CacheItemPriority.High;

                    // Layer 3: Database fallback - Use existing FindByCredentialAsync
                    var principal = await _principalRepository.FindByCredentialAsync(
                        ProviderType.Dynamic,
                        "https://app.dynamic.xyz",
                        dynamicUserId,
                        cancellationToken);

                    return principal?.Id;
                });

            // Store in request cache for subsequent calls in same request (Task 3.4)
            var httpContext = _httpContextAccessor.HttpContext;
            if (axonUserId.HasValue && httpContext != null)
            {
                httpContext.Items["AxonUserId"] = axonUserId.Value;
                _logger.LogDebug("AxonUserId cached for request from memory cache: {AxonUserId}", axonUserId.Value);
                CacheHitCounter.Add(1, new KeyValuePair<string, object?>("source", "memory"));
                activity?.SetTag("cache_source", "memory");
                activity?.SetTag("axon_user_id", axonUserId.Value.ToString());
                activity?.SetTag("cache_populated", true);
            }
            else
            {
                activity?.SetTag("cache_populated", false);
                activity?.SetTag("principal_found", axonUserId.HasValue);
            }

            stopwatch.Stop();
            var sourceTag = axonUserId.HasValue ? "memory" : "database";
            ResolutionLatency.Record(stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("source", sourceTag));

            return axonUserId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving AxonUserId for {DynamicUserId}", UserId);
            activity?.SetTag("error", true);
            activity?.SetTag("error.message", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Attempts to get AxonUserId from cache only (no database fallback).
    /// Used for sync contexts where database queries are not acceptable.
    /// Checks HttpContext.Items (0ms) and IMemoryCache (&lt;1ms) only.
    /// </summary>
    /// <param name="axonUserId">The cached AxonUserId if found</param>
    /// <returns>True if AxonUserId found in cache, false if cache miss or not authenticated</returns>
    public bool TryGetAxonUserId(out AxonUserId axonUserId)
    {
        using var activity = Activity.Current?.Source.StartActivity("TryGetAxonUserId");
        var stopwatch = Stopwatch.StartNew();

        // Layer 1: Request cache check (0ms)
        if (_httpContextAccessor.HttpContext?.Items.TryGetValue("AxonUserId", out var cached) == true && cached is AxonUserId cachedUserId)
        {
            axonUserId = cachedUserId;
            _logger.LogDebug("AxonUserId sync cache hit (request): {AxonUserId}", axonUserId);

            CacheHitCounter.Add(1, new KeyValuePair<string, object?>("source", "request_sync"));
            activity?.SetTag("cache_source", "request_sync");
            activity?.SetTag("cache_hit", true);
            activity?.SetTag("axon_user_id", axonUserId.ToString());

            stopwatch.Stop();
            ResolutionLatency.Record(stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("source", "request_sync"));

            return true;
        }

        // Layer 2: Memory cache check (<1ms) - NEW for Story 4b.3
        var dynamicUserId = UserId;
        if (!string.IsNullOrEmpty(dynamicUserId))
        {
            var cacheKey = $"axon:user:{dynamicUserId}";
            if (_memoryCache.TryGetValue(cacheKey, out var memoryCached) && memoryCached is AxonUserId memoryCachedUserId)
            {
                axonUserId = memoryCachedUserId;
                _logger.LogDebug("AxonUserId sync cache hit (memory): {AxonUserId}", axonUserId);

                // Populate request cache when memory cache hit occurs (Subtask 5.2)
                if (_httpContextAccessor.HttpContext != null)
                {
                    _httpContextAccessor.HttpContext.Items["AxonUserId"] = axonUserId;
                }

                CacheHitCounter.Add(1, new KeyValuePair<string, object?>("source", "memory_sync"));
                activity?.SetTag("cache_source", "memory_sync");
                activity?.SetTag("cache_hit", true);
                activity?.SetTag("axon_user_id", axonUserId.ToString());

                stopwatch.Stop();
                ResolutionLatency.Record(stopwatch.Elapsed.TotalMilliseconds,
                    new KeyValuePair<string, object?>("source", "memory_sync"));

                return true;
            }
        }

        axonUserId = default;
        _logger.LogDebug("AxonUserId sync cache miss");

        CacheMissCounter.Add(1, new KeyValuePair<string, object?>("source", "sync"));
        activity?.SetTag("cache_hit", false);

        stopwatch.Stop();
        ResolutionLatency.Record(stopwatch.Elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("source", "sync"));

        return false;
    }
}