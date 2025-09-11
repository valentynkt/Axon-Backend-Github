using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
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

// Add rate limiting services
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        
        // Apply rate limiting only to /auth/exchange endpoint
        if (context.Request.Path.StartsWithSegments("/api/v1/auth/exchange"))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                ipAddress,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
        }
        
        return RateLimitPartition.GetNoLimiter(ipAddress);
    });
});

var app = builder.Build();

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

app.UseHttpsRedirection();

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

// Configure rate limiting middleware with observability
app.UseRateLimiter();
app.UseMiddleware<RateLimitObservabilityMiddleware>();

// Configure routing
app.UseRouting();

// Authentication and authorization
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
