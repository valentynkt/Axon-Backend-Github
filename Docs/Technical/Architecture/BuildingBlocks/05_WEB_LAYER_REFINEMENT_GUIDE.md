# 🌐 BuildingBlocks/Web Layer - Complete Refinement Implementation Guide

**Version:** 1.0 - Brutal Refactoring Edition  
**Scope:** Complete replacement approach for BuildingBlocks/Web folder  
**Alignment:** Architecture Refinement PRD v3.1  

---

## Executive Summary

This document provides a comprehensive implementation guide for brutally refactoring the BuildingBlocks/Web layer to align with the MVP architecture goals. The Web layer serves as the HTTP interface boundary, implementing FastEndpoints, middleware, and cross-cutting web concerns with functional programming patterns.

## Table of Contents

1. [Current State Analysis](#current-state-analysis)
2. [Target Architecture](#target-architecture)
3. [Implementation Phases](#implementation-phases)
4. [Detailed Component Specifications](#detailed-component-specifications)
5. [Migration Strategy](#migration-strategy)
6. [Verification Gates](#verification-gates)

---

## Current State Analysis

### Existing Components Inventory

```
src/BuildingBlocks/Web/
├── BaseController.cs              # MVC Controller base (TO BE REPLACED)
├── IMinimalEndpoint.cs            # Basic endpoint interface (ENHANCE)
├── MinimalApiExtensions.cs        # Minimal API registration (REFACTOR)
├── CurrentUserProvider.cs         # User context provider (ENHANCE)
├── CorrelationExtensions.cs       # Correlation tracking (ENHANCE)
├── Extensions/                    # Extension methods (REORGANIZE)
│   ├── HealthCheckExtensions.cs   # Health checks (ENHANCE)
│   ├── JwtExtensions.cs          # JWT configuration (SECURE)
│   ├── MapsterExtensions.cs      # Mapping configuration (REPLACE)
│   ├── MassTransitExtensions.cs  # Message bus setup (ENHANCE)
│   ├── OpenTelemetryExtensions.cs # Observability (ENHANCE)
│   └── ProblemDetailsExtensions.cs # Error responses (REFACTOR)
└── OpenApi/                       # OpenAPI/Swagger (ENHANCE)
```

### Key Issues to Address

1. **Mixed Paradigms**: Both MVC Controllers and Minimal APIs
2. **Weak Error Handling**: No Result<T> pattern integration
3. **Missing Functional Patterns**: No railway-oriented pipeline
4. **Limited Validation**: Basic validation without functional composition
5. **Incomplete Observability**: Basic OpenTelemetry without comprehensive tracing

---

## Target Architecture

### Core Principles

- **FastEndpoints Only**: Complete removal of MVC Controllers
- **Functional Pipeline**: All requests flow through Result<T> railway
- **Immutable Request/Response**: All DTOs as immutable records
- **Comprehensive Telemetry**: Full request/response tracing
- **Security by Default**: JWT validation on all endpoints

### Component Structure

```
src/BuildingBlocks/Web/
├── Endpoints/                      # FastEndpoints base classes
│   ├── BaseEndpoint.cs           # Functional base endpoint
│   ├── BaseQueryEndpoint.cs      # Query-specific endpoint
│   ├── BaseCommandEndpoint.cs    # Command-specific endpoint
│   ├── EndpointResult.cs         # Result<T> to HTTP mapping
│   └── ValidationFilter.cs       # Functional validation
├── Middleware/                     # Custom middleware
│   ├── CorrelationMiddleware.cs  # Request correlation
│   ├── TelemetryMiddleware.cs    # OpenTelemetry integration
│   ├── RateLimitMiddleware.cs    # Rate limiting (NEW)
│   ├── RequestLoggingMiddleware.cs # Structured logging
│   └── ExceptionMiddleware.cs    # Global exception handling
├── Security/                       # Security components (NEW)
│   ├── JwtConfiguration.cs       # JWT setup
│   ├── AuthorizationPolicies.cs  # Policy definitions
│   ├── ClaimsTransformation.cs   # Claims enrichment
│   └── ApiKeyAuthentication.cs   # API key support
├── Validation/                     # Validation infrastructure (NEW)
│   ├── FunctionalValidator.cs    # Result<T> based validation
│   ├── ValidationPipeline.cs     # Validation composition
│   └── CommonValidations.cs      # Reusable validations
├── Mapping/                        # Object mapping (ENHANCED)
│   ├── MappingProfile.cs         # AutoMapper profiles
│   ├── FunctionalMapper.cs       # Result<T> aware mapping
│   └── DtoExtensions.cs          # DTO conversion helpers
├── OpenApi/                        # API documentation
│   ├── OpenApiConfiguration.cs   # Swagger setup
│   ├── SchemaFilters.cs          # Schema customization
│   ├── OperationFilters.cs       # Operation metadata
│   └── ExampleProviders.cs       # Request/response examples
├── Extensions/                     # Extension methods
│   ├── ServiceCollectionExtensions.cs # DI extensions
│   ├── ApplicationBuilderExtensions.cs # Pipeline configuration
│   ├── ResultExtensions.cs       # Result<T> web extensions
│   └── HttpContextExtensions.cs  # Context helpers
└── Configuration/                  # Configuration management (NEW)
    ├── WebOptions.cs              # Web layer options
    ├── CorsConfiguration.cs      # CORS setup
    ├── CompressionConfiguration.cs # Response compression
    └── CacheConfiguration.cs     # HTTP caching
```

---

## Implementation Phases

### Phase 1: Foundation Setup (Days 1-2)

#### 1.1 Create Functional Base Endpoints

```csharp
// BuildingBlocks/Web/Endpoints/BaseEndpoint.cs
using FastEndpoints;
using BuildingBlocks.Core.Functional.Results;

namespace BuildingBlocks.Web.Endpoints;

/// <summary>
/// Base endpoint with Result<T> pattern integration
/// </summary>
public abstract class BaseEndpoint<TRequest, TResponse> : Endpoint<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : notnull
{
    protected async Task<IResult> HandleResult<T>(Result<T> result, 
        Func<T, TResponse> mapper,
        CancellationToken ct = default)
    {
        return result.Match(
            success => Results.Ok(mapper(success)),
            failure => MapErrorToHttpResult(failure)
        );
    }
    
    private IResult MapErrorToHttpResult(Error error) => error.Type switch
    {
        ErrorType.Validation => Results.ValidationProblem(error.ToDictionary()),
        ErrorType.NotFound => Results.NotFound(error.ToProblemDetails()),
        ErrorType.Unauthorized => Results.Unauthorized(),
        ErrorType.Forbidden => Results.Forbid(),
        ErrorType.Conflict => Results.Conflict(error.ToProblemDetails()),
        ErrorType.External => Results.StatusCode(502),
        _ => Results.Problem(error.ToProblemDetails())
    };
    
    protected void AddTelemetryTags(params KeyValuePair<string, object?>[] tags)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            foreach (var tag in tags)
            {
                activity.SetTag(tag.Key, tag.Value);
            }
        }
    }
}

// BuildingBlocks/Web/Endpoints/BaseQueryEndpoint.cs
public abstract class BaseQueryEndpoint<TRequest, TResponse> : BaseEndpoint<TRequest, TResponse>
    where TRequest : IQuery<TResponse>, new()
    where TResponse : notnull
{
    private IMediator? _mediator;
    protected IMediator Mediator => _mediator ??= Resolve<IMediator>();
    
    public override void Configure()
    {
        // Queries are idempotent and cacheable
        Options(x => 
        {
            x.WithTags("Query");
            x.Produces<TResponse>(200);
            x.ProducesProblemDetails(400);
            x.ProducesProblemDetails(404);
            x.ProducesProblemDetails(500);
        });
        
        // Enable response caching by default for queries
        ResponseCache(60); // 60 seconds default
    }
    
    public override async Task HandleAsync(TRequest req, CancellationToken ct)
    {
        using var activity = Activity.StartActivity($"Query.{GetType().Name}");
        AddTelemetryTags(
            new("query.type", req.GetType().Name),
            new("query.timestamp", DateTimeOffset.UtcNow)
        );
        
        var result = await Mediator.Send(req, ct);
        
        await SendResultAsync(result, ct);
    }
    
    protected virtual async Task SendResultAsync(Result<TResponse> result, CancellationToken ct)
    {
        await result.Match(
            async success => await SendOkAsync(success, ct),
            async failure => await SendErrorAsync(failure, ct)
        );
    }
    
    private async Task SendErrorAsync(Error error, CancellationToken ct)
    {
        await (error.Type switch
        {
            ErrorType.NotFound => SendNotFoundAsync(ct),
            ErrorType.Validation => SendValidationProblemAsync(error, ct),
            _ => SendProblemAsync(error, ct)
        });
    }
}

// BuildingBlocks/Web/Endpoints/BaseCommandEndpoint.cs
public abstract class BaseCommandEndpoint<TRequest, TResponse> : BaseEndpoint<TRequest, TResponse>
    where TRequest : ICommand<TResponse>, new()
    where TResponse : notnull
{
    private IMediator? _mediator;
    protected IMediator Mediator => _mediator ??= Resolve<IMediator>();
    
    public override void Configure()
    {
        // Commands modify state and are not cacheable
        Options(x => 
        {
            x.WithTags("Command");
            x.Produces<TResponse>(200);
            x.Produces(201);
            x.ProducesProblemDetails(400);
            x.ProducesProblemDetails(409);
            x.ProducesProblemDetails(500);
        });
    }
    
    public override async Task HandleAsync(TRequest req, CancellationToken ct)
    {
        using var activity = Activity.StartActivity($"Command.{GetType().Name}");
        AddTelemetryTags(
            new("command.type", req.GetType().Name),
            new("command.timestamp", DateTimeOffset.UtcNow),
            new("command.user", User.Identity?.Name ?? "anonymous")
        );
        
        // Functional validation pipeline
        var validationResult = await ValidateAsync(req, ct);
        if (validationResult.IsFailure)
        {
            await SendValidationProblemAsync(validationResult.Error, ct);
            return;
        }
        
        var result = await Mediator.Send(req, ct);
        
        await SendResultAsync(result, ct);
    }
    
    protected virtual async Task<Result<Unit>> ValidateAsync(TRequest request, CancellationToken ct)
    {
        // Override for custom validation
        return Result<Unit>.Success(Unit.Value);
    }
}
```

#### 1.2 Implement Functional Validation

```csharp
// BuildingBlocks/Web/Validation/FunctionalValidator.cs
namespace BuildingBlocks.Web.Validation;

public interface IFunctionalValidator<T>
{
    Result<T> Validate(T value);
    Task<Result<T>> ValidateAsync(T value, CancellationToken ct = default);
}

public class FunctionalValidator<T> : IFunctionalValidator<T>
{
    private readonly List<Func<T, Result<Unit>>> _rules = new();
    private readonly List<Func<T, Task<Result<Unit>>>> _asyncRules = new();
    
    public FunctionalValidator<T> AddRule(Func<T, Result<Unit>> rule)
    {
        _rules.Add(rule);
        return this;
    }
    
    public FunctionalValidator<T> AddAsyncRule(Func<T, Task<Result<Unit>>> rule)
    {
        _asyncRules.Add(rule);
        return this;
    }
    
    public FunctionalValidator<T> NotNull(string errorMessage = "Value cannot be null")
    {
        return AddRule(value => 
            value != null 
                ? Result<Unit>.Success(Unit.Value)
                : Result<Unit>.Failure(Error.Validation(errorMessage)));
    }
    
    public FunctionalValidator<T> Must(Func<T, bool> predicate, string errorMessage)
    {
        return AddRule(value => 
            predicate(value)
                ? Result<Unit>.Success(Unit.Value)
                : Result<Unit>.Failure(Error.Validation(errorMessage)));
    }
    
    public Result<T> Validate(T value)
    {
        foreach (var rule in _rules)
        {
            var result = rule(value);
            if (result.IsFailure)
                return Result<T>.Failure(result.Error);
        }
        
        return Result<T>.Success(value);
    }
    
    public async Task<Result<T>> ValidateAsync(T value, CancellationToken ct = default)
    {
        // Run sync rules first
        var syncResult = Validate(value);
        if (syncResult.IsFailure)
            return syncResult;
        
        // Then run async rules
        foreach (var rule in _asyncRules)
        {
            var result = await rule(value);
            if (result.IsFailure)
                return Result<T>.Failure(result.Error);
        }
        
        return Result<T>.Success(value);
    }
}

// BuildingBlocks/Web/Validation/ValidationPipeline.cs
public static class ValidationPipeline
{
    public static Result<T> ValidateWith<T>(
        this T value,
        params Func<T, Result<Unit>>[] validators)
    {
        foreach (var validator in validators)
        {
            var result = validator(value);
            if (result.IsFailure)
                return Result<T>.Failure(result.Error);
        }
        return Result<T>.Success(value);
    }
    
    public static async Task<Result<T>> ValidateWithAsync<T>(
        this T value,
        params Func<T, Task<Result<Unit>>>[] validators)
    {
        foreach (var validator in validators)
        {
            var result = await validator(value);
            if (result.IsFailure)
                return Result<T>.Failure(result.Error);
        }
        return Result<T>.Success(value);
    }
    
    // Compose multiple validators
    public static FunctionalValidator<T> Compose<T>(
        params IFunctionalValidator<T>[] validators)
    {
        var composite = new FunctionalValidator<T>();
        
        foreach (var validator in validators)
        {
            composite.AddAsyncRule(async (value, ct) =>
            {
                var result = await validator.ValidateAsync(value, ct);
                return result.IsSuccess 
                    ? Result<Unit>.Success(Unit.Value)
                    : Result<Unit>.Failure(result.Error);
            });
        }
        
        return composite;
    }
}
```

### Phase 2: Middleware Implementation (Days 3-4)

#### 2.1 Correlation and Telemetry Middleware

```csharp
// BuildingBlocks/Web/Middleware/CorrelationMiddleware.cs
namespace BuildingBlocks.Web.Middleware;

public class CorrelationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationMiddleware> _logger;
    private const string CorrelationIdHeader = "X-Correlation-Id";
    
    public CorrelationMiddleware(RequestDelegate next, ILogger<CorrelationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }
    
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        
        // Add to response headers
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.TryAdd(CorrelationIdHeader, correlationId);
            return Task.CompletedTask;
        });
        
        // Add to logging scope
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["RequestPath"] = context.Request.Path.Value ?? "",
            ["RequestMethod"] = context.Request.Method
        }))
        {
            // Add to Activity for distributed tracing
            Activity.Current?.SetTag("correlation.id", correlationId);
            
            await _next(context);
        }
    }
    
    private string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var correlationId))
        {
            return correlationId.ToString();
        }
        
        var newCorrelationId = Guid.NewGuid().ToString("N");
        context.Items["CorrelationId"] = newCorrelationId;
        return newCorrelationId;
    }
}

// BuildingBlocks/Web/Middleware/TelemetryMiddleware.cs
public class TelemetryMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TelemetryMiddleware> _logger;
    private readonly IMetrics _metrics;
    
    public async Task InvokeAsync(HttpContext context)
    {
        using var activity = Activity.StartActivity($"HTTP {context.Request.Method} {context.Request.Path}");
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            activity?.SetTag("http.method", context.Request.Method);
            activity?.SetTag("http.url", context.Request.GetDisplayUrl());
            activity?.SetTag("http.scheme", context.Request.Scheme);
            activity?.SetTag("http.host", context.Request.Host.ToString());
            activity?.SetTag("http.path", context.Request.Path.Value);
            activity?.SetTag("http.user_agent", context.Request.Headers.UserAgent.ToString());
            
            await _next(context);
            
            activity?.SetTag("http.status_code", context.Response.StatusCode);
            activity?.SetStatus(GetActivityStatus(context.Response.StatusCode));
            
            RecordMetrics(context, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            throw;
        }
    }
    
    private void RecordMetrics(HttpContext context, long elapsedMs)
    {
        var tags = new[]
        {
            new KeyValuePair<string, object?>("method", context.Request.Method),
            new KeyValuePair<string, object?>("endpoint", context.Request.Path.Value),
            new KeyValuePair<string, object?>("status", context.Response.StatusCode),
            new KeyValuePair<string, object?>("status_category", GetStatusCategory(context.Response.StatusCode))
        };
        
        _metrics.RecordHttpRequestDuration(elapsedMs, tags);
        _metrics.IncrementHttpRequestCount(tags);
        
        if (context.Response.StatusCode >= 500)
        {
            _metrics.IncrementHttpErrorCount(tags);
        }
    }
    
    private static ActivityStatusCode GetActivityStatus(int statusCode) => statusCode switch
    {
        >= 200 and < 400 => ActivityStatusCode.Ok,
        >= 400 and < 500 => ActivityStatusCode.Error,
        >= 500 => ActivityStatusCode.Error,
        _ => ActivityStatusCode.Unset
    };
    
    private static string GetStatusCategory(int statusCode) => statusCode switch
    {
        >= 200 and < 300 => "2xx",
        >= 300 and < 400 => "3xx",
        >= 400 and < 500 => "4xx",
        >= 500 => "5xx",
        _ => "unknown"
    };
}
```

#### 2.2 Exception Handling Middleware

```csharp
// BuildingBlocks/Web/Middleware/ExceptionMiddleware.cs
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;
    
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }
    
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "An unhandled exception occurred");
        
        var problemDetails = CreateProblemDetails(context, exception);
        
        context.Response.StatusCode = problemDetails.Status ?? 500;
        context.Response.ContentType = "application/problem+json";
        
        await context.Response.WriteAsJsonAsync(problemDetails);
    }
    
    private ProblemDetails CreateProblemDetails(HttpContext context, Exception exception)
    {
        var statusCode = exception switch
        {
            ValidationException => StatusCodes.Status400BadRequest,
            NotFoundException => StatusCodes.Status404NotFound,
            UnauthorizedException => StatusCodes.Status401Unauthorized,
            ForbiddenException => StatusCodes.Status403Forbidden,
            ConflictException => StatusCodes.Status409Conflict,
            ExternalServiceException => StatusCodes.Status502BadGateway,
            _ => StatusCodes.Status500InternalServerError
        };
        
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Type = $"https://httpstatuses.com/{statusCode}",
            Title = GetTitle(statusCode),
            Detail = _environment.IsDevelopment() ? exception.Message : GetGenericMessage(statusCode),
            Instance = context.Request.Path,
            Extensions =
            {
                ["correlationId"] = context.Items["CorrelationId"] ?? Guid.NewGuid().ToString(),
                ["timestamp"] = DateTimeOffset.UtcNow
            }
        };
        
        if (_environment.IsDevelopment())
        {
            problemDetails.Extensions["exception"] = new
            {
                message = exception.Message,
                type = exception.GetType().Name,
                stackTrace = exception.StackTrace
            };
        }
        
        return problemDetails;
    }
    
    private static string GetTitle(int statusCode) => statusCode switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        502 => "Bad Gateway",
        _ => "Internal Server Error"
    };
    
    private static string GetGenericMessage(int statusCode) => statusCode switch
    {
        400 => "The request was invalid",
        401 => "Authentication is required",
        403 => "You don't have permission to access this resource",
        404 => "The requested resource was not found",
        409 => "The request conflicts with the current state",
        502 => "An external service is unavailable",
        _ => "An error occurred while processing your request"
    };
}
```

### Phase 3: Security & Rate Limiting (Days 5-6)

#### 3.1 Enhanced JWT Configuration

```csharp
// BuildingBlocks/Web/Security/JwtConfiguration.cs
namespace BuildingBlocks.Web.Security;

public static class JwtConfiguration
{
    public static IServiceCollection AddCustomJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection("Jwt").Get<JwtOptions>() 
            ?? throw new InvalidOperationException("JWT configuration is missing");
        
        services.AddSingleton(jwtOptions);
        
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = jwtOptions.Authority;
                options.Audience = jwtOptions.Audience;
                options.RequireHttpsMetadata = !configuration.IsDevelopment();
                
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                    ClockSkew = TimeSpan.Zero
                };
                
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILogger<JwtBearerEvents>>();
                        
                        logger.LogWarning(context.Exception, 
                            "JWT authentication failed");
                        
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILogger<JwtBearerEvents>>();
                        
                        logger.LogDebug("JWT token validated for user {UserId}", 
                            context.Principal?.Identity?.Name);
                        
                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        // Customize 401 response
                        context.Response.Headers.Append("WWW-Authenticate", 
                            "Bearer error=\"invalid_token\"");
                        
                        return Task.CompletedTask;
                    }
                };
            });
        
        services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
                
            // Add custom policies
            options.AddPolicy("RequireAdmin", policy =>
                policy.RequireClaim("role", "admin"));
                
            options.AddPolicy("RequireUser", policy =>
                policy.RequireClaim("role", "user", "admin"));
        });
        
        return services;
    }
}

// BuildingBlocks/Web/Security/ClaimsTransformation.cs
public class CustomClaimsTransformation : IClaimsTransformation
{
    private readonly IUserContextService _userContext;
    
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
            return principal;
        
        var claimsIdentity = (ClaimsIdentity)principal.Identity;
        
        // Add custom claims from user context
        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            var userContext = await _userContext.GetUserContextAsync(userId);
            
            claimsIdentity.AddClaim(new Claim("tenant", userContext.TenantId));
            claimsIdentity.AddClaim(new Claim("subscription", userContext.SubscriptionLevel));
            
            foreach (var permission in userContext.Permissions)
            {
                claimsIdentity.AddClaim(new Claim("permission", permission));
            }
        }
        
        return principal;
    }
}
```

#### 3.2 Rate Limiting Implementation

```csharp
// BuildingBlocks/Web/Middleware/RateLimitMiddleware.cs
public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    private readonly RateLimitOptions _options;
    private readonly ILogger<RateLimitMiddleware> _logger;
    
    public async Task InvokeAsync(HttpContext context)
    {
        if (!ShouldApplyRateLimit(context))
        {
            await _next(context);
            return;
        }
        
        var key = GenerateClientKey(context);
        var limit = GetRateLimit(context);
        
        var requestCount = await _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _options.Window;
            return 0;
        });
        
        if (requestCount >= limit.RequestsPerWindow)
        {
            await HandleRateLimitExceeded(context, limit);
            return;
        }
        
        await _cache.SetAsync(key, requestCount + 1, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _options.Window
        });
        
        // Add rate limit headers
        context.Response.Headers.Append("X-RateLimit-Limit", limit.RequestsPerWindow.ToString());
        context.Response.Headers.Append("X-RateLimit-Remaining", 
            (limit.RequestsPerWindow - requestCount - 1).ToString());
        context.Response.Headers.Append("X-RateLimit-Reset", 
            DateTimeOffset.UtcNow.Add(_options.Window).ToUnixTimeSeconds().ToString());
        
        await _next(context);
    }
    
    private bool ShouldApplyRateLimit(HttpContext context)
    {
        // Skip for health checks and metrics
        if (context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/metrics"))
            return false;
        
        // Skip for authenticated admin users
        if (context.User.IsInRole("admin"))
            return false;
        
        return true;
    }
    
    private string GenerateClientKey(HttpContext context)
    {
        // Use user ID for authenticated users
        if (context.User.Identity?.IsAuthenticated == true)
        {
            return $"user_{context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value}";
        }
        
        // Use IP address for anonymous users
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"ip_{ipAddress}";
    }
    
    private RateLimit GetRateLimit(HttpContext context)
    {
        // Different limits for authenticated vs anonymous
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var subscriptionLevel = context.User.FindFirst("subscription")?.Value;
            return subscriptionLevel switch
            {
                "premium" => new RateLimit { RequestsPerWindow = 1000, Window = TimeSpan.FromMinutes(1) },
                "standard" => new RateLimit { RequestsPerWindow = 100, Window = TimeSpan.FromMinutes(1) },
                _ => new RateLimit { RequestsPerWindow = 60, Window = TimeSpan.FromMinutes(1) }
            };
        }
        
        return new RateLimit { RequestsPerWindow = 20, Window = TimeSpan.FromMinutes(1) };
    }
    
    private async Task HandleRateLimitExceeded(HttpContext context, RateLimit limit)
    {
        _logger.LogWarning("Rate limit exceeded for {ClientKey}", GenerateClientKey(context));
        
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.Append("Retry-After", _options.Window.TotalSeconds.ToString());
        
        var problemDetails = new ProblemDetails
        {
            Status = 429,
            Title = "Too Many Requests",
            Detail = $"Rate limit of {limit.RequestsPerWindow} requests per {limit.Window.TotalMinutes} minute(s) exceeded",
            Instance = context.Request.Path
        };
        
        await context.Response.WriteAsJsonAsync(problemDetails);
    }
}
```

### Phase 4: OpenAPI & Configuration (Days 7-8)

#### 4.1 Enhanced OpenAPI Configuration

```csharp
// BuildingBlocks/Web/OpenApi/OpenApiConfiguration.cs
namespace BuildingBlocks.Web.OpenApi;

public static class OpenApiConfiguration
{
    public static IServiceCollection AddCustomOpenApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<SecuritySchemeTransformer>();
            options.AddDocumentTransformer<ResultTypeTransformer>();
            options.AddOperationTransformer<CorrelationIdOperationTransformer>();
        });
        
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.SerializerOptions.Converters.Add(new ResultJsonConverter());
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });
        
        return services;
    }
    
    public static WebApplication UseCustomOpenApi(this WebApplication app)
    {
        app.MapOpenApi("/openapi/{documentName}.json")
            .RequireAuthorization("ApiDocumentation");
        
        app.MapGet("/", () => Results.Redirect("/swagger"))
            .ExcludeFromDescription();
        
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "Axon API V1");
            options.DisplayRequestDuration();
            options.EnableDeepLinking();
            options.EnableFilter();
            options.ShowExtensions();
            options.EnableValidator();
            options.EnableTryItOutByDefault();
            
            // Add JWT token support
            options.UseRequestInterceptor(@"
                (request) => {
                    const token = localStorage.getItem('jwt_token');
                    if (token) {
                        request.headers['Authorization'] = 'Bearer ' + token;
                    }
                    return request;
                }
            ");
        });
        
        return app;
    }
}

// BuildingBlocks/Web/OpenApi/ResultTypeTransformer.cs
public class ResultTypeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, 
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        // Transform Result<T> types in schemas
        foreach (var schema in document.Components.Schemas)
        {
            if (schema.Key.StartsWith("Result") && schema.Value.Properties != null)
            {
                // Simplify Result<T> schema to just show T or Error
                var successSchema = schema.Value.Properties.GetValueOrDefault("value");
                var errorSchema = schema.Value.Properties.GetValueOrDefault("error");
                
                if (successSchema != null && errorSchema != null)
                {
                    schema.Value.OneOf = new List<OpenApiSchema>
                    {
                        successSchema,
                        errorSchema
                    };
                    schema.Value.Properties = null;
                }
            }
        }
        
        return Task.CompletedTask;
    }
}
```

#### 4.2 Application Builder Extensions

```csharp
// BuildingBlocks/Web/Extensions/ApplicationBuilderExtensions.cs
namespace BuildingBlocks.Web.Extensions;

public static class ApplicationBuilderExtensions
{
    public static WebApplication ConfigureCustomPipeline(this WebApplication app)
    {
        // Development-specific middleware
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseCustomOpenApi();
        }
        else
        {
            // Production error handling
            app.UseExceptionHandler("/error");
            app.UseHsts();
        }
        
        // Security headers
        app.Use(async (context, next) =>
        {
            context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Append("X-Frame-Options", "DENY");
            context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
            context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
            
            await next();
        });
        
        // Core middleware pipeline
        app.UseHttpsRedirection();
        app.UseResponseCompression();
        app.UseResponseCaching();
        
        // Custom middleware
        app.UseMiddleware<CorrelationMiddleware>();
        app.UseMiddleware<TelemetryMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();
        
        // Authentication & Authorization
        app.UseAuthentication();
        app.UseAuthorization();
        
        // Rate limiting
        app.UseMiddleware<RateLimitMiddleware>();
        
        // Exception handling (catches any unhandled exceptions)
        app.UseMiddleware<ExceptionMiddleware>();
        
        // Health checks
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            }
        }).AllowAnonymous();
        
        // Metrics endpoint
        app.MapMetrics("/metrics")
            .RequireAuthorization("MetricsAccess");
        
        // Map endpoints
        app.MapEndpoints();
        
        return app;
    }
    
    public static WebApplication MapEndpoints(this WebApplication app)
    {
        var endpoints = app.Services.GetServices<IMinimalEndpoint>();
        
        foreach (var endpoint in endpoints)
        {
            endpoint.MapEndpoint(app);
        }
        
        return app;
    }
}

// BuildingBlocks/Web/Extensions/ServiceCollectionExtensions.cs
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWebBuildingBlocks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Core services
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserProvider, CurrentUserProvider>();
        
        // Validation
        services.AddValidatorsFromAssemblyContaining<IAssemblyMarker>();
        
        // Caching
        services.AddMemoryCache();
        services.AddDistributedMemoryCache(); // Replace with Redis in production
        
        // Response compression
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });
        
        // Response caching
        services.AddResponseCaching();
        
        // CORS
        services.AddCors(options =>
        {
            options.AddPolicy("Default", builder =>
            {
                builder
                    .WithOrigins(configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials()
                    .WithExposedHeaders("X-Correlation-Id", "X-RateLimit-Limit", 
                        "X-RateLimit-Remaining", "X-RateLimit-Reset");
            });
        });
        
        // API versioning
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = ApiVersionReader.Combine(
                new UrlSegmentApiVersionReader(),
                new HeaderApiVersionReader("X-Api-Version"),
                new MediaTypeApiVersionReader("version")
            );
        }).AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });
        
        // FastEndpoints
        services.AddFastEndpoints(options =>
        {
            options.SourceGeneratorDiscoveredTypes = DiscoveredTypes.All;
        });
        
        // Authentication & Authorization
        services.AddCustomJwtAuthentication(configuration);
        services.AddTransient<IClaimsTransformation, CustomClaimsTransformation>();
        
        // OpenAPI
        services.AddCustomOpenApi(configuration);
        
        // Health checks
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy())
            .AddDbContextCheck<ApplicationDbContext>()
            .AddRedis(configuration.GetConnectionString("Redis") ?? "")
            .AddRabbitMQ(rabbitConnectionString: configuration.GetConnectionString("RabbitMQ") ?? "");
        
        return services;
    }
}
```

---

## Migration Strategy

### Step 1: Parallel Development (Week 1)
- Create new components alongside existing ones
- Use feature flags to toggle between old/new implementations
- No breaking changes to existing code

### Step 2: Gradual Migration (Week 2)
- Migrate one endpoint at a time
- Update tests for migrated endpoints
- Monitor performance and errors

### Step 3: Deprecation (Week 3)
- Mark old components as obsolete
- Update documentation
- Notify dependent teams

### Step 4: Removal (Week 4)
- Remove deprecated components
- Clean up feature flags
- Final testing and validation

---

## Verification Gates

### Gate 1: Foundation Complete
✅ All base endpoint classes implemented  
✅ Functional validation working  
✅ Result<T> pattern integrated  
✅ Unit tests passing (>95% coverage)  

### Gate 2: Middleware Operational
✅ Correlation tracking active  
✅ Telemetry data flowing  
✅ Exception handling tested  
✅ Rate limiting enforced  
✅ Integration tests passing  

### Gate 3: Security Hardened
✅ JWT authentication working  
✅ Authorization policies enforced  
✅ Rate limiting by user/IP  
✅ Security headers present  
✅ Security scan passing  

### Gate 4: Production Ready
✅ OpenAPI documentation complete  
✅ Health checks responsive  
✅ Metrics exported  
✅ Performance benchmarks met  
✅ Load testing successful  

---

## Performance Targets

### Response Times
- P50: < 20ms
- P95: < 50ms
- P99: < 100ms

### Throughput
- Minimum: 10,000 RPS per instance
- Target: 25,000 RPS per instance

### Resource Usage
- Memory: < 256MB per instance
- CPU: < 50% at target load

---

## Security Checklist

- [ ] All endpoints require authentication (except health/metrics)
- [ ] Rate limiting configured per user/IP
- [ ] Security headers on all responses
- [ ] Input validation on all requests
- [ ] Output encoding for all responses
- [ ] Secrets stored in secure vault
- [ ] TLS 1.3 enforced
- [ ] CORS properly configured
- [ ] SQL injection protection
- [ ] XSS protection

---

## Testing Requirements

### Unit Tests
- Coverage: > 95%
- All happy paths tested
- All error scenarios tested
- Result<T> pattern validated

### Integration Tests
- All endpoints tested
- Authentication/authorization verified
- Rate limiting validated
- Error handling confirmed

### Performance Tests
- Load testing: 10,000 concurrent users
- Stress testing: Find breaking point
- Soak testing: 24-hour stability

### Security Tests
- OWASP Top 10 scan
- Penetration testing
- Dependency vulnerability scan

---

## Documentation Requirements

### Code Documentation
- XML comments on all public APIs
- README in each component folder
- Architecture decision records (ADRs)

### API Documentation
- OpenAPI specification complete
- Example requests/responses
- Error code reference
- Rate limit documentation

### Operations Documentation
- Deployment guide
- Configuration reference
- Monitoring setup
- Troubleshooting guide

---

## Conclusion

This comprehensive implementation guide provides a complete blueprint for brutally refactoring the BuildingBlocks/Web layer. The approach ensures:

1. **Clean Architecture**: Complete separation of concerns
2. **Functional Programming**: Result<T> pattern throughout
3. **Production Ready**: Security, monitoring, and performance
4. **Developer Experience**: Clear APIs and minimal boilerplate
5. **Maintainability**: Comprehensive testing and documentation

The phased approach with verification gates ensures safe migration while maintaining system stability. Each phase builds upon the previous, creating a robust and scalable web infrastructure layer.

**Total Effort**: 8 days of focused development  
**Risk Level**: Low (with parallel development and gradual migration)  
**Expected ROI**: 5x through improved reliability, performance, and developer productivity