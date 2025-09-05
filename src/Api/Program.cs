using System.Text.Json;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Axon.Api.Configuration;
using BuildingBlocks.Web.OpenApi;
using BuildingBlocks.Web.Configuration;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.Extensions.Logging.Console;
using BuildingBlocks.Primitives.Ids;
using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using BuildingBlocks.Web.Middleware;

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
    options.SerializerOptions.Converters.Add(new UserId.UserIdSystemTextJsonConverter());
    
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

var app = builder.Build();

// CRITICAL: Validate DI container configuration on startup
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var validationErrors = Axon.Api.Configuration.DIValidationService.ValidateServiceRegistrations(scope.ServiceProvider);
    
    if (validationErrors.Count > 0)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError("DI VALIDATION FAILED:");
        foreach (var error in validationErrors)
        {
            logger.LogError("  ❌ {Error}", error);
        }
        
        // Don't fail startup in development, but log prominently
        logger.LogWarning("🚨 DI validation detected issues - please fix before production deployment");
    }
    else
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("✅ DI validation passed - all critical services registered correctly");
    }
}


// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Local")
{
    app.UseAspnetOpenApi(); // Built-in OpenAPI for Scalar
    app.UseSwaggerGen(); // FastEndpoints Swagger generation
}

app.UseHttpsRedirection();

// Global exception handling middleware (first in pipeline)
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

// CRITICAL FIX: Enable CORS before authentication/authorization
var corsOptions = app.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>();
app.UseCors(corsOptions?.PolicyName ?? "DefaultPolicy");

// Configure routing
app.UseRouting();

// Authentication enabled with Dynamic.xyz JWT validation
// Endpoints can now use proper authentication instead of AllowAnonymous()
app.UseAuthentication();
app.UseAuthorization();

// Configure FastEndpoints (before MVC controllers)
app.UseFastEndpoints();

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
