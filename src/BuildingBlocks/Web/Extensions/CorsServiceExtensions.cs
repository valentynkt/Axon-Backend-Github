using BuildingBlocks.Web.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Web.Extensions;

public static class CorsServiceExtensions
{
    public static IServiceCollection AddCorsConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Configure options from configuration
        services.Configure<CorsOptions>(configuration.GetSection(CorsOptions.SectionName));
        
        // Add CORS service with configuration
        services.AddCors(corsOptions =>
        {
            var serviceProvider = services.BuildServiceProvider();
            var corsConfig = serviceProvider.GetRequiredService<IOptions<CorsOptions>>().Value;
            var logger = serviceProvider.GetRequiredService<ILogger<CorsOptions>>();
            
            // Validate configuration
            if (!corsConfig.IsValid)
            {
                throw new InvalidOperationException(
                    "CORS configuration is invalid. AllowedOrigins must contain at least one origin.");
            }
            
            corsOptions.AddPolicy(corsConfig.PolicyName, policy =>
            {
                // Always log CORS configuration for transparency
                logger.LogInformation("🌐 Configuring CORS policy '{PolicyName}' for environment '{Environment}'", 
                    corsConfig.PolicyName, environment.EnvironmentName);
                
                logger.LogInformation("   📍 Allowed origins: {Origins}", string.Join(", ", corsConfig.AllowedOrigins));
                logger.LogInformation("   🔧 Allow credentials: {AllowCredentials}", corsConfig.AllowCredentials);
                
                // Configure origins
                policy.WithOrigins(corsConfig.AllowedOrigins);
                
                // Configure methods
                if (corsConfig.AllowedMethods.Contains("*"))
                {
                    policy.AllowAnyMethod();
                }
                else
                {
                    policy.WithMethods(corsConfig.AllowedMethods);
                }
                
                // Configure headers
                if (corsConfig.AllowedHeaders.Contains("*"))
                {
                    policy.AllowAnyHeader();
                }
                else
                {
                    policy.WithHeaders(corsConfig.AllowedHeaders);
                }
                
                // Configure credentials
                if (corsConfig.AllowCredentials)
                {
                    policy.AllowCredentials();
                }
                else
                {
                    policy.DisallowCredentials();
                }
                
                // Set preflight cache age
                policy.SetPreflightMaxAge(TimeSpan.FromSeconds(corsConfig.PreflightMaxAge));
                
                // Environment-specific configurations
                if (environment.IsDevelopment() || environment.EnvironmentName == "Local")
                {
                    logger.LogWarning("⚠️  Development CORS enabled - ensure production configuration is properly secured");
                }
            });
        });
        
        return services;
    }
}