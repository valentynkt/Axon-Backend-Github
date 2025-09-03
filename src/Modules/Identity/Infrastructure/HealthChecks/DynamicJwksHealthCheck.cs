using System.Text.Json;
using Axon.Modules.Identity.Infrastructure.ExternalServices.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Axon.Modules.Identity.Infrastructure.HealthChecks;

/// <summary>
/// Health check for Dynamic.xyz JWKS endpoint availability
/// </summary>
public sealed class DynamicJwksHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DynamicJwksHealthCheck> _logger;
    private readonly DynamicXyzOptions _options;

    public DynamicJwksHealthCheck(
        HttpClient httpClient,
        ILogger<DynamicJwksHealthCheck> logger,
        IOptions<DynamicXyzOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var jwksUrl = $"https://app.dynamic.xyz/api/v0/sdk/{_options.EnvironmentId}/.well-known/jwks";
            _logger.LogDebug("Checking JWKS endpoint health: {JwksUrl}", jwksUrl);

            using var response = await _httpClient.GetAsync(jwksUrl, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("JWKS endpoint returned {StatusCode}: {ReasonPhrase}", 
                    response.StatusCode, response.ReasonPhrase);
                
                return HealthCheckResult.Unhealthy($"JWKS endpoint returned {response.StatusCode}: {response.ReasonPhrase}");
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            
            // Validate that we can parse the JWKS response
            using var document = JsonDocument.Parse(content);
            if (!document.RootElement.TryGetProperty("keys", out var keysElement) || keysElement.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("JWKS endpoint returned invalid format - missing or invalid 'keys' array");
                return HealthCheckResult.Unhealthy("JWKS endpoint returned invalid format - missing or invalid 'keys' array");
            }

            var keyCount = keysElement.GetArrayLength();
            _logger.LogDebug("JWKS endpoint healthy with {KeyCount} keys", keyCount);
            
            return HealthCheckResult.Healthy($"JWKS endpoint responsive with {keyCount} keys");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error checking JWKS endpoint");
            return HealthCheckResult.Unhealthy("JWKS endpoint HTTP error", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Timeout checking JWKS endpoint");
            return HealthCheckResult.Unhealthy("JWKS endpoint timeout", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON response from JWKS endpoint");
            return HealthCheckResult.Unhealthy("JWKS endpoint returned invalid JSON", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error checking JWKS endpoint");
            return HealthCheckResult.Unhealthy("Unexpected error checking JWKS endpoint", ex);
        }
    }
}