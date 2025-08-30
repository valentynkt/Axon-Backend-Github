using System.Threading.RateLimiting;
using Axon.Modules.Identity.Infrastructure.ExternalServices.DynamicXyz.Client;
using Axon.Modules.Identity.Infrastructure.ExternalServices.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace Axon.Modules.Identity.Infrastructure.ExternalServices.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDynamicXyzInfrastructure(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        // Register configuration options
        services.Configure<DynamicXyzOptions>(configuration.GetSection(DynamicXyzOptions.SectionName));
        services.AddSingleton<IValidateOptions<DynamicXyzOptions>, DynamicXyzOptionsValidator>();

        // Register memory cache for JWKS caching
        services.AddMemoryCache();

        // Register health check
        services.AddScoped<DynamicXyzHealthCheck>();

        // Register main Dynamic.xyz API HttpClient with policies
        services.AddHttpClient<IDynamicApiClient, DynamicApiClient>("DynamicXyzClient", (serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<DynamicXyzOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.ApiToken}");
            client.Timeout = TimeSpan.FromSeconds(options.HttpClient.TimeoutSeconds);
        })
        .AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.Delay = TimeSpan.FromSeconds(2);
            options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
            options.CircuitBreaker.MinimumThroughput = 5;
            options.CircuitBreaker.FailureRatio = 0.8;
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(90);
        });

        // Register separate JWKS HttpClient without authentication
        services.AddHttpClient("JwksClient", (serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<DynamicXyzOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10); // Shorter timeout for JWKS
        })
        .AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.Delay = TimeSpan.FromSeconds(1);
            options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.MinimumThroughput = 3;
            options.CircuitBreaker.FailureRatio = 0.8;
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
        });

        return services;
    }
}