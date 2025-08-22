using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Axon.BuildingBlocks.Web.Configuration;

/// <summary>
/// Interface for endpoint configuration
/// </summary>
public interface IEndpointConfiguration
{
    /// <summary>
    /// Checks if an endpoint is enabled
    /// </summary>
    bool IsEndpointEnabled(string endpointName);

    /// <summary>
    /// Checks if a feature is enabled
    /// </summary>
    bool IsFeatureEnabled(string featureName);

    /// <summary>
    /// Gets configuration value for an endpoint
    /// </summary>
    T? GetEndpointConfig<T>(string endpointName, string key);

    /// <summary>
    /// Gets rate limit configuration for an endpoint
    /// </summary>
    RateLimitConfig? GetRateLimitConfig(string endpointName);
}

/// <summary>
/// Rate limit configuration
/// </summary>
public sealed record RateLimitConfig(
    int RequestsPerMinute,
    int BurstSize,
    bool IsEnabled = true
);

/// <summary>
/// Default implementation of endpoint configuration
/// </summary>
public sealed class EndpointConfiguration : IEndpointConfiguration
{
    private readonly IConfiguration _configuration;
    private readonly Dictionary<string, bool> _featureFlags;
    private readonly Dictionary<string, bool> _endpointFlags;

    public EndpointConfiguration(IConfiguration configuration)
    {
        _configuration = configuration;
        _featureFlags = new Dictionary<string, bool>();
        _endpointFlags = new Dictionary<string, bool>();
        
        LoadFeatureFlags();
        LoadEndpointFlags();
    }

    private void LoadFeatureFlags()
    {
        var section = _configuration.GetSection("Features");
        foreach (var child in section.GetChildren())
        {
            if (bool.TryParse(child.Value, out var enabled))
            {
                _featureFlags[child.Key] = enabled;
            }
        }
    }

    private void LoadEndpointFlags()
    {
        var section = _configuration.GetSection("Endpoints");
        foreach (var child in section.GetChildren())
        {
            var enabledValue = child["Enabled"];
            if (bool.TryParse(enabledValue, out var enabled))
            {
                _endpointFlags[child.Key] = enabled;
            }
        }
    }

    public bool IsEndpointEnabled(string endpointName)
    {
        // Check explicit endpoint configuration
        if (_endpointFlags.TryGetValue(endpointName, out var enabled))
        {
            return enabled;
        }

        // Check by feature
        var feature = GetFeatureForEndpoint(endpointName);
        if (!string.IsNullOrEmpty(feature))
        {
            return IsFeatureEnabled(feature);
        }

        // Default to enabled
        return true;
    }

    public bool IsFeatureEnabled(string featureName)
    {
        return _featureFlags.TryGetValue(featureName, out var enabled) ? enabled : true;
    }

    public T? GetEndpointConfig<T>(string endpointName, string key)
    {
        var section = _configuration.GetSection($"Endpoints:{endpointName}:{key}");
        if (!section.Exists())
        {
            return default;
        }

        return section.Get<T>();
    }

    public RateLimitConfig? GetRateLimitConfig(string endpointName)
    {
        var section = _configuration.GetSection($"Endpoints:{endpointName}:RateLimit");
        if (!section.Exists())
        {
            return null;
        }

        var requestsPerMinute = section.GetValue<int>("RequestsPerMinute");
        var burstSize = section.GetValue<int>("BurstSize");
        var isEnabled = section.GetValue<bool>("Enabled");

        if (requestsPerMinute <= 0)
        {
            return null;
        }

        return new RateLimitConfig(requestsPerMinute, burstSize, isEnabled);
    }

    private static string? GetFeatureForEndpoint(string endpointName)
    {
        // Map endpoints to features
        return endpointName switch
        {
            var name when name.Contains("Chat", StringComparison.OrdinalIgnoreCase) => "Chat",
            var name when name.Contains("User", StringComparison.OrdinalIgnoreCase) => "Users",
            var name when name.Contains("Admin", StringComparison.OrdinalIgnoreCase) => "Admin",
            _ => null
        };
    }
}

/// <summary>
/// Extension methods for endpoint configuration
/// </summary>
public static class EndpointConfigurationExtensions
{
    /// <summary>
    /// Adds endpoint configuration services
    /// </summary>
    public static IServiceCollection AddEndpointConfiguration(this IServiceCollection services)
    {
        services.AddSingleton<IEndpointConfiguration, EndpointConfiguration>();
        return services;
    }

    /// <summary>
    /// Configures endpoints with feature flags
    /// </summary>
    public static void ConfigureEndpointsWithFeatureFlags(
        this WebApplication app,
        IEndpointConfiguration configuration)
    {
        _ = app ?? throw new ArgumentNullException(nameof(app));
        _ = configuration ?? throw new ArgumentNullException(nameof(configuration));
        
        // This would be called in Program.cs to apply feature flags
        // Implementation would filter endpoints based on configuration
    }
}