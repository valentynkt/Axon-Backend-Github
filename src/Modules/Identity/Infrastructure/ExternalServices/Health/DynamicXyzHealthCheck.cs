using Axon.Modules.Identity.Infrastructure.ExternalServices.Configuration;
using Axon.Modules.Identity.Infrastructure.ExternalServices.DynamicXyz.Client;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Axon.Modules.Identity.Infrastructure.ExternalServices.Health;

/// <summary>
/// Health check for Dynamic.xyz API connectivity
/// </summary>
public class DynamicXyzHealthCheck : IHealthCheck
{
    private readonly IDynamicApiClient _apiClient;
    private readonly DynamicXyzOptions _options;
    
    public DynamicXyzHealthCheck(IDynamicApiClient apiClient, IOptions<DynamicXyzOptions> options)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }
    
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Use a simple health check endpoint - most APIs have /health or /status
            var result = await _apiClient.GetAsync<object>("/health", cancellationToken);
            
            if (result.IsSuccess)
            {
                return HealthCheckResult.Healthy($"Dynamic.xyz API is reachable at {_options.BaseUrl}");
            }
            
            return HealthCheckResult.Unhealthy(
                $"Dynamic.xyz API returned error: {result.Error.Message}",
                data: new Dictionary<string, object>
                {
                    ["BaseUrl"] = _options.BaseUrl,
                    ["ErrorCode"] = result.Error.Code,
                    ["ErrorMessage"] = result.Error.Message
                });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Dynamic.xyz API health check failed",
                ex,
                data: new Dictionary<string, object>
                {
                    ["BaseUrl"] = _options.BaseUrl,
                    ["Error"] = ex.Message
                });
        }
    }
}