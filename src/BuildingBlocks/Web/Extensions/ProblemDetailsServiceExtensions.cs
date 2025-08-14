using BuildingBlocks.Core.Diagnostics.Exceptions;
using BuildingBlocks.Web.ProblemDetails;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Web.Extensions;

/// <summary>
/// Service collection extensions for enhanced Problem Details integration.
/// </summary>
public static class ProblemDetailsServiceExtensions
{
    /// <summary>
    /// Adds enhanced Problem Details services with Error system integration.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configure">Optional configuration callback</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddEnhancedProblemDetails(
        this IServiceCollection services,
        Action<ProblemDetailsOptions>? configure = null)
    {
        services.AddProblemDetails(options =>
        {
            // Custom problem details configuration
            options.CustomizeProblemDetails = context =>
            {
                // Add correlation ID
                context.ProblemDetails.Extensions["correlationId"] = 
                    context.HttpContext.TraceIdentifier;
                    
                // Add timestamp
                context.ProblemDetails.Extensions["timestamp"] = 
                    DateTimeOffset.UtcNow.ToString("O");
                    
                // Add API version if present
                var apiVersion = context.HttpContext.GetRequestedApiVersion();
                if (apiVersion != null)
                {
                    context.ProblemDetails.Extensions["apiVersion"] = 
                        apiVersion.ToString();
                }
                
                // Development-only details
                #if DEBUG
                context.ProblemDetails.Extensions["machineName"] = 
                    Environment.MachineName;
                context.ProblemDetails.Extensions["environment"] = "Development";
                #endif
            };
            
            // Custom exception handling is moved to middleware
            // .NET 9.0 ProblemDetailsOptions doesn't support Map method
            // Exception mapping will be handled in the custom middleware
            
            // Apply custom configuration
            configure?.Invoke(options);
        });
        
        return services;
    }
    
    /// <summary>
    /// Adds the Problem Details middleware to the application pipeline.
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    public static IApplicationBuilder UseEnhancedProblemDetails(
        this IApplicationBuilder app)
    {
        app.UseMiddleware<ProblemDetailsMiddleware>();
        return app;
    }
}

/// <summary>
/// Extensions for getting API version information from HttpContext.
/// </summary>
internal static class HttpContextExtensions
{
    /// <summary>
    /// Gets the requested API version from the HttpContext.
    /// </summary>
    /// <param name="httpContext">The HTTP context</param>
    /// <returns>The API version if available</returns>
    public static object? GetRequestedApiVersion(this HttpContext httpContext)
    {
        // Try to get API version from various sources
        
        // From headers
        if (httpContext.Request.Headers.TryGetValue("Api-Version", out var headerVersion))
        {
            return headerVersion.FirstOrDefault();
        }
        
        // From query parameters
        if (httpContext.Request.Query.TryGetValue("api-version", out var queryVersion))
        {
            return queryVersion.FirstOrDefault();
        }
        
        // From route data
        if (httpContext.Request.RouteValues.TryGetValue("version", out var routeVersion))
        {
            return routeVersion;
        }
        
        return null;
    }
}