using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scalar.AspNetCore;
using System;
using System.Linq;

namespace BuildingBlocks.Web.OpenApi
{
    public static class OpenApiExtensions
    {
        // ref: https://github.com/dotnet/eShop/blob/main/src/eShop.ServiceDefaults/OpenApi.Extensions.cs
        public static IServiceCollection AddAspnetOpenApi(this IServiceCollection services)
        {
            string[] versions = ["v1"];

            foreach (var description in versions)
            {
                services.AddOpenApi(
                    description,
                    options =>
                    {
                        // TODO: Add security document transformer when needed
                    });
            }

            return services;
        }

        public static IApplicationBuilder UseAspnetOpenApi(this WebApplication app)
        {
            var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("OpenApi");
            
            app.MapOpenApi();

            app.UseSwaggerUI(
                options =>
                {
                    var descriptions = app.DescribeApiVersions();

                    // build a swagger endpoint for each discovered API version
                    foreach (var description in descriptions)
                    {
                        var openApiUrl = $"/openapi/{description.GroupName}.json";
                        var name = description.GroupName.ToUpperInvariant();
                        options.SwaggerEndpoint(openApiUrl, name);
                    }
                });

            // Add scalar ui - use FastEndpoints OpenAPI document
            app.MapScalarApiReference(
                options =>
                {
                    options
                        .WithTitle("Axon API")
                        .WithOpenApiRoutePattern("/swagger/{documentName}/swagger.json")
                        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
                });

            // Log API documentation URLs
            var urls = app.Urls.FirstOrDefault() ?? "https://localhost:7204";
            if (urls.Contains(';'))
            {
                // If multiple URLs, prefer HTTPS
                var urlList = urls.Split(';');
                urls = urlList.FirstOrDefault(u => u.StartsWith("https", StringComparison.OrdinalIgnoreCase)) ?? urlList.First();
            }
            
            logger.LogInformation("📚 API Documentation:");
            logger.LogInformation("  🔹 Swagger UI:    {Url}/swagger", urls);
            logger.LogInformation("  🔹 Scalar UI:     {Url}/scalar/v1", urls);
            logger.LogInformation("  🔹 FastEndpoints: {Url}/swagger/v1/swagger.json", urls);

            return app;
        }
    }
}