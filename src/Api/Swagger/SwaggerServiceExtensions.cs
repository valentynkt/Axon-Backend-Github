using System.Reflection;
using Axon.Api.Contracts.Common;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Axon.Api.Swagger;

/// <summary>
/// Swagger/OpenAPI configuration extensions for comprehensive API documentation.
/// Configures JWT bearer authentication, error schemas, and documentation generation.
/// </summary>
public static class SwaggerServiceExtensions
{
    /// <summary>
    /// Configures Swagger/OpenAPI documentation with JWT authentication and comprehensive schemas.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            // Basic API information
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Axon Identity Service API",
                Version = "v1",
                Description = """
                    Axon Identity Service provides secure authentication and wallet management for the Solana ecosystem.
                    
                    ## Authentication
                    All endpoints require a valid JWT Bearer token from Dynamic.xyz in the Authorization header.
                    
                    ## Rate Limiting
                    The `/auth/exchange` endpoint is rate limited to 10 requests per minute per IP address.
                    
                    ## Error Handling
                    All errors return structured ApiError responses with consistent format and privacy protection.
                    """,
                Contact = new OpenApiContact
                {
                    Name = "Axon Team",
                    Url = new Uri("https://axon.dev")
                }
            });

            // Configure JWT Bearer authentication
            options.AddSecurityDefinition("bearerAuth", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Provider-issued JWT (e.g., Dynamic.xyz). Enter your JWT token without 'Bearer ' prefix."
            });

            // Apply bearer auth globally
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "bearerAuth"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            // Add XML documentation
            var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }

            // Configure error response schemas
            ConfigureErrorSchemas(options);

            // Configure example responses
            ConfigureExampleResponses(options);

            // Configure schema naming
            options.CustomSchemaIds(type => type.FullName?.Replace("+", ".", StringComparison.Ordinal));
        });

        return services;
    }

    /// <summary>
    /// Configures error response schemas for all HTTP status codes.
    /// </summary>
    private static void ConfigureErrorSchemas(SwaggerGenOptions options)
    {
        // Map ApiError schema
        options.MapType<ApiError>(() => new OpenApiSchema
        {
            Type = "object",
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["code"] = new() { Type = "string", Description = "Machine-readable error code", Example = new Microsoft.OpenApi.Any.OpenApiString("VALIDATION_ERROR") },
                ["message"] = new() { Type = "string", Description = "Human-readable error message", Example = new Microsoft.OpenApi.Any.OpenApiString("Email is required") },
                ["details"] = new() { Type = "object", Description = "Optional structured details", Nullable = true }
            },
            Required = new HashSet<string> { "code", "message" },
            Example = new Microsoft.OpenApi.Any.OpenApiObject
            {
                ["code"] = new Microsoft.OpenApi.Any.OpenApiString("VALIDATION_ERROR"),
                ["message"] = new Microsoft.OpenApi.Any.OpenApiString("Email is required"),
                ["details"] = new Microsoft.OpenApi.Any.OpenApiObject
                {
                    ["field"] = new Microsoft.OpenApi.Any.OpenApiString("email")
                }
            }
        });
    }

    /// <summary>
    /// Configures example responses for common scenarios.
    /// </summary>
    private static void ConfigureExampleResponses(SwaggerGenOptions options)
    {
        // This will be called by the OpenAPI document generator
        options.DocumentFilter<ErrorResponseDocumentFilter>();
    }
}

/// <summary>
/// Document filter to add error response examples to all endpoints.
/// </summary>
public class ErrorResponseDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        foreach (var path in swaggerDoc.Paths.Values)
        {
            foreach (var operation in path.Operations.Values)
            {
                // Add common error responses if not already present
                AddErrorResponse(operation, "400", "Bad Request", CreateValidationErrorExample());
                AddErrorResponse(operation, "401", "Unauthorized", CreateUnauthorizedErrorExample());
                AddErrorResponse(operation, "409", "Conflict", CreateConflictErrorExample());
                AddErrorResponse(operation, "422", "Unprocessable Entity", CreateBusinessRuleErrorExample());
                AddErrorResponse(operation, "429", "Too Many Requests", CreateRateLimitErrorExample());
                AddErrorResponse(operation, "500", "Internal Server Error", CreateInternalErrorExample());
            }
        }
    }

    private static void AddErrorResponse(OpenApiOperation operation, string statusCode, string description, object _)
    {
        if (!operation.Responses.ContainsKey(statusCode))
        {
            operation.Responses.Add(statusCode, new OpenApiResponse
            {
                Description = description,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new()
                    {
                        Schema = new OpenApiSchema { Reference = new OpenApiReference { Id = "ApiError", Type = ReferenceType.Schema } },
                        Example = new Microsoft.OpenApi.Any.OpenApiObject()
                    }
                }
            });
        }
    }

    private static object CreateValidationErrorExample() => new
    {
        code = "VALIDATION_ERROR",
        message = "Invalid request parameters",
        details = new { field = "email", error = "Email is required" }
    };

    private static object CreateUnauthorizedErrorExample() => new
    {
        code = "UNAUTHORIZED",
        message = "Authentication required"
    };

    private static object CreateConflictErrorExample() => new
    {
        code = "WALLET_OWNERSHIP_CONFLICT",
        message = "wallet already owned",
        details = new { chainId = "solana", address = "9WzDXwBbmkg8ZTbNMqUxvQRAyrZzDsGYdLVL9zYtAWWM" }
    };

    private static object CreateBusinessRuleErrorExample() => new
    {
        code = "BUSINESS_RULE_VIOLATION",
        message = "Domain constraint violation"
    };

    private static object CreateRateLimitErrorExample() => new
    {
        code = "RATE_LIMIT_EXCEEDED",
        message = "Rate limit exceeded",
        details = new { retryAfter = 60 }
    };

    private static object CreateInternalErrorExample() => new
    {
        code = "INTERNAL_ERROR",
        message = "An internal error occurred"
    };
}