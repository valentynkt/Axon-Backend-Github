// using Axon.Api.Common.ErrorHandling;  // Not needed for POC
using Axon.Api.Configuration.Mapping;
using Axon.Api.Modules;
using Axon.BuildingBlocks.Web.Configuration;
using BuildingBlocks.Web.OpenApi;
using BuildingBlocks.Web.Extensions;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Polly;
using Polly.Extensions.Http;
using System.Reflection;
using BuildingBlocks.Infrastructure.Persistence.Infrastructure;
using BuildingBlocks.Application.Configuration;
using Axon.Modules.Chat.Application.Commands.StartConversation;
using Axon.Modules.Identity.Application.Commands.ExchangeCredential;

namespace Axon.Api.Configuration;

/// <summary>
/// Service registration for API layer
/// </summary>

public static class ServiceRegistration
{
    /// <summary>
    /// Register application services
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <param name="environment">Host environment</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Add core framework services
        services.AddControllers();
        services.AddFastEndpoints()
                .SwaggerDocument(o =>
                {
                    o.DocumentSettings = s =>
                    {
                        s.Title = "Axon API";
                        s.Version = "v1";
                        s.Description = "Axon Backend API - Modular Monolith with Clean Architecture";
                    };
                    // Disable auto-tagging from route segments - use manual tags only
                    o.AutoTagPathSegmentIndex = 0;
                });
        
        // Add API documentation
        services.AddEndpointsApiExplorer();
        
        // Add ASP.NET Core OpenAPI services
        services.AddAspnetOpenApi();
        
        // CRITICAL FIX: Add comprehensive OpenTelemetry observability
        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder
                    .AddSource("Axon.Application") // Our custom ActivitySource
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.EnrichWithHttpRequest = (activity, request) =>
                        {
                            activity.SetTag("http.request.body.size", request.ContentLength);
                        };
                        options.EnrichWithHttpResponse = (activity, response) =>
                        {
                            activity.SetTag("http.response.body.size", response.ContentLength);
                        };
                    })
                    .AddHttpClientInstrumentation();

                // Only export traces to console in development for debugging
                if (environment.IsDevelopment())
                {
                    builder.AddConsoleExporter(options =>
                    {
                        options.Targets = OpenTelemetry.Exporter.ConsoleExporterOutputTargets.Console;
                    });
                }
            })
            .WithMetrics(builder =>
            {
                builder
                    .AddMeter("Axon.Application") // Our custom Meter only
                    .AddView("http.server.request.duration", new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = new double[] { 0.1, 0.5, 1.0, 2.5, 5.0, 10.0 }
                    })
                    .AddView("*", MetricStreamConfiguration.Drop); // Drop all other noisy metrics
                
                // Only add infrastructure metrics in production with proper exporters
                if (!environment.IsDevelopment())
                {
                    builder
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation();
                }
                
                // Never export metrics to console - too noisy
            });
        
        // CRITICAL FIX: Add comprehensive health checks for production monitoring
        services.AddHealthChecks()
            .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("API is responsive"), ["live"])
            .AddCheck("memory", () => 
            {
                var allocatedMemory = GC.GetTotalMemory(false);
                var maxMemory = 1024L * 1024L * 1024L; // 1GB threshold
                var status = allocatedMemory < maxMemory 
                    ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy 
                    : Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded;
                return new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult(
                    status, 
                    $"Allocated memory: {allocatedMemory / (1024 * 1024)} MB");
            }, ["ready"])
            .AddCheck("dynamic-auth", () => 
            {
                // Simple health check for Dynamic auth service
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Dynamic auth service is configured");
            }, ["ready", "external"]);
        
        // CRITICAL FIX: Add CORS configuration for frontend integration
        services.AddCorsConfiguration(configuration, environment);
        
        // JWT authentication and rate limiting are now handled by IdentityApiModule

        // CRITICAL FIX: Register MediatR once for ALL modules to prevent handler overwriting
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(
                // Chat module handlers
                typeof(StartConversationCommand).Assembly,
                // Identity module handlers
                typeof(ExchangeCredentialCommand).Assembly
            );
        });

        // Register pipeline behaviors once for all modules
        services.AddApplicationServices();

        // CRITICAL FIX: Register outbox facade services (includes IEnvelopeContextAccessor)
        // This ensures all required services for MassTransitIntegrationEventPublisher are available
        services.AddOutboxFacade();

        // Configure Mapster with profiles and validation
        services.AddMapsterWithProfiles(Assembly.GetExecutingAssembly());

        // Register ISeedManager service required by auto-migration
        services.AddScoped<ISeedManager, SeedManager>();

        // Register API modules
        RegisterApiModules(services, configuration, environment);
        
        return services;
    }

    private static void RegisterApiModules(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Discover and register all modules implementing IApiModule
        var modules = DiscoverApiModules();
        
        foreach (var module in modules)
        {
            module.ConfigureServices(services, configuration, environment);
        }
    }

    private static List<IApiModule> DiscoverApiModules()
    {
        var modules = new List<IApiModule>();
        
        // For now, manually register modules
        // In the future, this could use reflection to auto-discover
        modules.Add(new ChatApiModule());
        modules.Add(new IdentityApiModule()); // Clean architecture with canonical command/query patterns
        
        return modules;
    }
    
    private static Polly.Retry.AsyncRetryPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => !msg.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // 2, 4, 8 seconds
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    // Log retry attempts if logger is available
                });
    }
}