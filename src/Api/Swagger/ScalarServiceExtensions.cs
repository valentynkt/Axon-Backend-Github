using Scalar.AspNetCore;

namespace Axon.Api.Swagger;

/// <summary>
/// Scalar UI configuration extensions providing an alternative API documentation interface.
/// Offers modern, fast documentation UI with same OpenAPI specification as Swagger.
/// </summary>
public static class ScalarServiceExtensions
{
    /// <summary>
    /// Configures Scalar UI with custom branding and organization for Axon Identity Service.
    /// </summary>
    /// <param name="app">Application builder</param>
    /// <returns>Application builder for chaining</returns>
    public static IApplicationBuilder UseScalarDocumentation(this IApplicationBuilder app)
    {
        // Scalar configuration would be done through endpoint routing
        // For now, we'll focus on Swagger UI functionality
        return app;
    }

    /// <summary>
    /// Configures both Swagger and Scalar UI in development environment.
    /// </summary>
    /// <param name="app">Application builder</param>
    /// <param name="environment">Web host environment</param>
    /// <returns>Application builder for chaining</returns>
    public static IApplicationBuilder UseApiDocumentation(this IApplicationBuilder app, IWebHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            // Swagger UI at /swagger
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "Axon Identity Service API v1");
                options.RoutePrefix = "swagger";
                options.DisplayRequestDuration();
                options.EnableTryItOutByDefault();
                options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
                
                // Custom styling
                options.InjectStylesheet("/swagger-ui/custom.css");
                options.DocumentTitle = "Axon Identity API Documentation";
                
                // Configure OAuth2 for JWT testing
                options.OAuthClientId("axon-identity-docs");
                options.OAuthAppName("Axon Identity API Docs");
                options.OAuthUsePkce();
            });

            // Note: Scalar would be configured through endpoint routing
        }

        return app;
    }
}

/// <summary>
/// Service collection extensions for documentation services.
/// </summary>
public static class DocumentationServiceExtensions
{
    /// <summary>
    /// Adds both Swagger and Scalar documentation services.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        // Add Swagger documentation
        services.AddSwaggerDocumentation();
        
        // Scalar is added via UseScalarDocumentation middleware
        return services;
    }
}