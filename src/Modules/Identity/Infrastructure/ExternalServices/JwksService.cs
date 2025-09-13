using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Polly;
using Polly.Extensions.Http;
using Polly.Retry;
using Axon.Modules.Identity.Application.Contracts.ExternalServices;
using Axon.Modules.Identity.Infrastructure.ExternalServices.Configuration;
using BuildingBlocks.Core.Diagnostics.Errors;
using CSharpFunctionalExtensions;

namespace Axon.Modules.Identity.Infrastructure.ExternalServices;

/// <summary>
/// Service for fetching and caching JWKS (JSON Web Key Set) keys from Dynamic.xyz.
/// Handles key retrieval, caching with configurable TTL, and retry logic with exponential backoff.
/// </summary>
public sealed class JwksService : IJwksService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<JwksService> _logger;
    private readonly DynamicXyzOptions _options;
    private readonly TimeSpan _jwksCacheExpiration;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    public JwksService(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<JwksService> logger,
        IOptions<DynamicXyzOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        
        // Cache JWKS keys for configurable duration (default 10 minutes)
        _jwksCacheExpiration = TimeSpan.FromMinutes(_options.Jwt?.JwksCacheMinutes ?? 10);
        
        // Configure Polly retry policy with exponential backoff and jitter
        _retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromMilliseconds(500 * Math.Pow(2, retryAttempt - 1)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogDebug("Retrying JWKS fetch (attempt {Attempt}/3) after {Delay}ms due to: {Error}",
                        retryCount, timespan.TotalMilliseconds, 
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                });
    }

    /// <summary>
    /// Gets JWKS keys for JWT token validation, with automatic caching and retry logic.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>
    /// Result containing collection of security keys for JWT validation,
    /// or error if keys cannot be fetched or parsed
    /// </returns>
    public async Task<Result<ICollection<SecurityKey>, Error>> GetJwksKeysAsync(CancellationToken cancellationToken = default)
    {
        const string jwksCacheKey = "dynamic_jwks_keys";
        
        // Check cache first
        if (_cache.TryGetValue<ICollection<SecurityKey>>(jwksCacheKey, out var cachedKeys) && cachedKeys != null)
        {
            _logger.LogDebug("JWKS cache hit - returning {KeyCount} cached keys", cachedKeys.Count);
            return Result.Success<ICollection<SecurityKey>, Error>(cachedKeys);
        }

        _logger.LogDebug("JWKS cache miss - fetching keys from Dynamic.xyz endpoint: {JwksUri}", _options.JwksUri);

        try
        {
            using var activity = Activity.Current?.Source.StartActivity("JwksService.GetKeys");
            activity?.SetTag("provider", "dynamic");
            activity?.SetTag("cache_hit", false);

            // Execute HTTP request with Polly retry policy
            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _httpClient.GetAsync(_options.JwksUri, cancellationToken);
            });

            // Ensure success status
            response.EnsureSuccessStatusCode();
            
            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var jwks = JsonDocument.Parse(jsonContent);
            
            var keys = new List<SecurityKey>();
            
            // Parse JWKS exactly as current implementation
            if (jwks.RootElement.TryGetProperty("keys", out var keysArray))
            {
                foreach (var keyElement in keysArray.EnumerateArray())
                {
                    var keyJson = keyElement.GetRawText();
                    var jwk = JsonWebKey.Create(keyJson);
                    keys.Add(jwk);
                }
            }

            if (keys.Count == 0)
            {
                _logger.LogWarning("No keys found in JWKS response from {JwksUri}", _options.JwksUri);
                activity?.SetTag("error", "no_keys");
                return Result.Failure<ICollection<SecurityKey>, Error>(
                    Error.External("No keys found in JWKS response", "AUTH.NO_JWKS_KEYS"));
            }

            // Cache the keys with configured expiration
            _cache.Set(jwksCacheKey, (ICollection<SecurityKey>)keys, _jwksCacheExpiration);
            
            _logger.LogInformation("Successfully fetched and cached {KeyCount} JWKS keys", keys.Count);
            activity?.SetTag("keys_count", keys.Count);
            activity?.SetTag("cache_duration_minutes", _jwksCacheExpiration.TotalMinutes);
            
            return Result.Success<ICollection<SecurityKey>, Error>((ICollection<SecurityKey>)keys);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("JWKS fetch cancelled by client");
            return Result.Failure<ICollection<SecurityKey>, Error>(
                Error.External("JWKS fetch was cancelled", "AUTH.JWKS_FETCH_CANCELLED"));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching JWKS from {JwksUri}: {Message}", _options.JwksUri, ex.Message);
            return Result.Failure<ICollection<SecurityKey>, Error>(
                Error.External("Failed to fetch JWKS keys due to HTTP error", "AUTH.JWKS_FETCH_ERROR", ex));
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Timeout fetching JWKS from {JwksUri}", _options.JwksUri);
            return Result.Failure<ICollection<SecurityKey>, Error>(
                Error.External("JWKS fetch timeout", "AUTH.JWKS_FETCH_TIMEOUT", ex));
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON in JWKS response from {JwksUri}: {Message}", _options.JwksUri, ex.Message);
            return Result.Failure<ICollection<SecurityKey>, Error>(
                Error.External("Invalid JSON in JWKS response", "AUTH.JWKS_INVALID_JSON", ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching JWKS from {JwksUri}", _options.JwksUri);
            return Result.Failure<ICollection<SecurityKey>, Error>(
                Error.External("Failed to fetch JWKS keys", "AUTH.JWKS_FETCH_ERROR", ex));
        }
    }
}