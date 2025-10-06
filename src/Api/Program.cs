using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Axon.Api.Configuration;
using BuildingBlocks.Web.OpenApi;
using BuildingBlocks.Web.Configuration;
using Microsoft.AspNetCore.HttpOverrides;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.Extensions.Logging.Console;
using BuildingBlocks.Primitives.Ids;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using BuildingBlocks.Web.Middleware;
using BuildingBlocks.Infrastructure.Persistence;
using Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Identity.Infrastructure.Persistence.DbContexts;
using Axon.Api;
using Axon.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Configure structured logging with correlation IDs
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.FormatterName = "simple";
});

// Configure logging to include structured data
builder.Services.Configure<ConsoleFormatterOptions>(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "HH:mm:ss.fff ";
});

// Add services to the container
builder.Services.AddApplicationServices(builder.Configuration, builder.Environment);

// Configure Kestrel server options with timeout settings
builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
{
    // Increase request timeout to 2 minutes for long-running operations
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(1);
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    
    // Configure request body limits
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
    options.Limits.MaxRequestLineSize = 8192; // 8KB for JWT tokens
    options.Limits.MaxRequestHeadersTotalSize = 32768; // 32KB for headers
});

// Configure JSON serialization options
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    
    // StronglyTypedId converters - one per ID type
    options.SerializerOptions.Converters.Add(new AiResponseId.AiResponseIdSystemTextJsonConverter());
    options.SerializerOptions.Converters.Add(new MessageId.MessageIdSystemTextJsonConverter());
    options.SerializerOptions.Converters.Add(new AxonUserId.AxonUserIdSystemTextJsonConverter());
    
    // Vogen VO converters  
    options.SerializerOptions.Converters.Add(new MessageContent.MessageContentSystemTextJsonConverter());
});

// Add API versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'V";
    options.SubstituteApiVersionInUrl = true;
});

// Add rate limiting services
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.OnRejected = async (context, cancellationToken) =>
    {
        // Add rate limit headers when request is rejected
        context.HttpContext.Response.Headers["X-RateLimit-Limit"] = "10";
        context.HttpContext.Response.Headers["X-RateLimit-Remaining"] = "0";
        context.HttpContext.Response.Headers["X-RateLimit-Reset"] = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds().ToString();
        context.HttpContext.Response.Headers.RetryAfter = "60";

        await Task.CompletedTask;
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        // Get IP address, checking X-Forwarded-For header first for test compatibility
        var ipAddress = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim()
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        // Apply rate limiting only to /api/v1/auth/exchange endpoint
        if (context.Request.Path.StartsWithSegments("/api/v1/auth/exchange"))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                $"rate_limited_{ipAddress}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
        }

        return RateLimitPartition.GetNoLimiter($"no_limit_{ipAddress}");
    });
});

var app = builder.Build();

// Apply database migrations if enabled
await app.ApplyMigrationsAsync();

// Simplified startup validation for development
if (app.Environment.IsDevelopment())
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("✅ Axon API started successfully in development mode");
}


// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Local")
{
    app.UseAspnetOpenApi(); // Built-in OpenAPI for Scalar
    app.UseSwaggerGen(); // FastEndpoints Swagger generation
}

// Skip HTTPS redirection in test environment to avoid redirect port configuration issues
if (!app.Environment.EnvironmentName.Equals("Testing", StringComparison.OrdinalIgnoreCase))
{
    app.UseHttpsRedirection();
}

// Global exception handling middleware (first in pipeline)
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

// Add security headers
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers.XXSSProtection = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    
    // Add correlation ID for request tracing
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() 
        ?? Guid.NewGuid().ToString("N")[..12];
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    context.Items["CorrelationId"] = correlationId;
    
    await next();
});

// HSTS (HTTP Strict Transport Security)
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// CRITICAL FIX: Enable CORS before authentication/authorization
var corsOptions = app.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>();
app.UseCors(corsOptions?.PolicyName ?? "DefaultPolicy");

// Configure forwarded headers for test environment rate limiting
if (app.Environment.EnvironmentName == "Test")
{
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    });
}

// Configure rate limiting middleware with observability
app.UseRateLimiter();
app.UseMiddleware<RateLimitObservabilityMiddleware>();

// Configure routing
app.UseRouting();

// Authentication and authorization (must be in main pipeline for FastEndpoints)
app.UseAuthentication();
app.UseAuthorization();

// Format 401 responses as JSON (skip for exchange endpoint which handles its own responses)
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api/v1/auth/exchange"),
    appBuilder =>
    {
        appBuilder.UseAuthenticationErrorFormatting();
    });

// Configure FastEndpoints with global processors (before MVC controllers)
app.UseFastEndpoints(c =>
{
    c.Endpoints.Configurator = ep =>
    {
        // Pre-processors execute in order: Logging
        ep.PreProcessor<BuildingBlocks.Web.Endpoints.Processors.LoggingPreProcessor>(FastEndpoints.Order.Before);

        // Post-processors execute in order: Logging
        ep.PostProcessor<BuildingBlocks.Web.Endpoints.Processors.LoggingPostProcessor>(FastEndpoints.Order.After);
    };
});

// Configure MVC controllers
app.MapControllers();

// PRODUCTION-READY: Configure comprehensive health check endpoints
app.MapGet("/", () => "Axon API v1.0 - Ready");

// Liveness probe - basic responsiveness check (for Kubernetes/Docker)
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    AllowCachingResponses = false
});

// Readiness probe - detailed health information (for load balancers)
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    AllowCachingResponses = false,
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(x => new
            {
                name = x.Key,
                status = x.Value.Status.ToString(),
                description = x.Value.Description,
                duration = x.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        });
        await context.Response.WriteAsync(response);
    }
});

// Legacy health endpoint for backward compatibility
app.MapHealthChecks("/health");

// Dynamic.xyz specific health check endpoint
app.MapHealthChecks("/health/dynamic-auth", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Name == "dynamic-auth",
    AllowCachingResponses = false,
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var entry = report.Entries.FirstOrDefault();
        var response = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = entry.Value.Status.ToString(),
            description = entry.Value.Description,
            duration = entry.Value.Duration.TotalMilliseconds,
            data = entry.Value.Data
        });
        await context.Response.WriteAsync(response);
    }
});

app.Run();

namespace Axon.Api
{
    /// <summary>
    /// Extension methods for applying EF Core migrations at startup
    /// </summary>
    public static class MigrationExtensions
    {
        /// <summary>
        /// Apply pending migrations for all DbContexts if enabled in configuration
        /// </summary>
        public static async Task ApplyMigrationsAsync(this WebApplication app)
        {
            ArgumentNullException.ThrowIfNull(app);

            var migrationLogger = app.Services.GetRequiredService<ILogger<Program>>();

            // Check configuration flag
            var config = app.Configuration.GetSection("DatabaseOptions");
            if (!config.GetValue<bool>("EnableAutomaticMigrations", false))
            {
                migrationLogger.LogDebug("Automatic migrations disabled in configuration");
                return;
            }

            // Only apply in development/local environments for safety
            if (!app.Environment.IsDevelopment() && app.Environment.EnvironmentName != "Local")
            {
                migrationLogger.LogWarning("Automatic migrations skipped in {Environment} environment", app.Environment.EnvironmentName);
                return;
            }

            migrationLogger.LogInformation("Applying automatic migrations in {Environment} environment", app.Environment.EnvironmentName);

            // Apply migrations for both contexts using existing UseMigration extension
            // This leverages the existing infrastructure in BuildingBlocks
            app.UseMigration<ChatDbContext>();
            app.UseMigration<IdentityDbContext>();

            // Adding await to satisfy async method requirements
            await Task.CompletedTask;

            migrationLogger.LogInformation("✅ Automatic migrations completed successfully");
        }
    }
}
