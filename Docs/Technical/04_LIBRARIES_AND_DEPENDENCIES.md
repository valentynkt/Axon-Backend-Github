# 📦 Axon Backend - Libraries & Dependencies Reference

## Table of Contents
- [Overview](#overview)
- [Technology Stack](#technology-stack)
- [Core Framework](#core-framework)
- [Web & API](#web--api)
- [Data Access](#data-access)
- [Messaging & Events](#messaging--events)
- [Caching](#caching)
- [Authentication & Security](#authentication--security)
- [Validation](#validation)
- [Mapping & Serialization](#mapping--serialization)
- [Testing](#testing)
- [Observability & Logging](#observability--logging)
- [Resilience & Fault Tolerance](#resilience--fault-tolerance)
- [Background Processing](#background-processing)
- [Development Tools](#development-tools)
- [Build & Deployment](#build--deployment)
- [Dependency Management](#dependency-management)
- [Version Strategy](#version-strategy)
- [Security Compliance](#security-compliance)
- [Performance Optimization](#performance-optimization)

## Overview

This document provides comprehensive information about all libraries and dependencies used in the Axon Backend system, including version numbers, usage patterns, implementation guidelines, and architectural decisions. It serves as the single source of truth for dependency management and library integration patterns.

### Document Purpose
- **Reference Guide**: Complete catalog of all dependencies
- **Integration Patterns**: Best practices for library usage
- **Version Control**: Centralized version management strategy
- **Security Tracking**: Vulnerability monitoring and patching
- **Performance Guidelines**: Optimization techniques per library
- **Migration Path**: Upgrade strategies and breaking changes

## Technology Stack

### Core Technologies
| Technology | Version | Purpose | Status |
|------------|---------|---------|--------|
| **.NET** | 10.0.0 | Core runtime framework | LTS |
| **C#** | 13.0 | Programming language | Current |
| **PostgreSQL** | 16.x | Primary database | Production |
| **Redis** | 7.x | Distributed caching | Production |
| **RabbitMQ** | 3.13.x | Message broker | Production |
| **EventStore** | 23.x | Event sourcing | Production |
| **Docker** | Latest | Containerization | Production |
| **Kubernetes** | 1.28+ | Container orchestration | Production |

### Architecture Patterns
- **Clean Architecture**: Domain-driven design with clear boundaries
- **CQRS**: Command Query Responsibility Segregation
- **Event Sourcing**: Audit trail and event replay capabilities
- **Microservices Ready**: Modular monolith with service boundaries
- **API-First**: RESTful APIs with OpenAPI documentation

## Core Framework

### .NET 10 & ASP.NET Core
**Version**: 10.0.0  
**License**: MIT  
**Purpose**: Core runtime and web framework  
**Update Policy**: Track LTS releases, update within 3 months

#### Key Packages
```xml
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Options" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Http" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.DependencyModel" Version="9.0.0" />
```

#### Configuration Patterns
```csharp
// Program.cs - Minimal API configuration
var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();
builder.Services.AddProblemDetails();

// Configure host
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

// Build application
var app = builder.Build();

// Configure middleware pipeline
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseExceptionHandler();
app.MapControllers();
app.MapHealthChecks("/health");
```

## Web & API

### FastEndpoints
**Version**: 7.0.1  
**License**: MIT  
**Purpose**: High-performance minimal API framework  
**Update Policy**: Minor version updates monthly

#### Packages
```xml
<PackageReference Include="FastEndpoints" Version="7.0.1" />
<PackageReference Include="FastEndpoints.Swagger" Version="7.0.0" />
```

#### Implementation Pattern
```csharp
// Endpoint configuration
app.UseFastEndpoints(config =>
{
    config.Endpoints.RoutePrefix = "api";
    config.Versioning.Prefix = "v";
    config.Serializer.Options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    
    config.Errors.ResponseBuilder = (failures, ctx, statusCode) =>
    {
        return new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{statusCode}",
            Title = "Validation Error",
            Status = statusCode,
            Detail = string.Join(", ", failures.Select(f => f.ErrorMessage)),
            Instance = ctx.Request.Path
        };
    };
    
    config.Throttle.HeaderName = "X-Rate-Limit";
    config.Throttle.Message = "Rate limit exceeded";
});
```

### Swashbuckle (OpenAPI/Swagger)
**Version**: 7.1.0 / 7.2.0  
**License**: MIT  
**Purpose**: API documentation and testing  
**Security Note**: Disable in production environments

#### Packages
```xml
<PackageReference Include="Swashbuckle.AspNetCore" Version="7.1.0" />
<PackageReference Include="Swashbuckle.AspNetCore.SwaggerGen" Version="7.1.0" />
<PackageReference Include="Swashbuckle.AspNetCore.SwaggerUI" Version="7.1.0" />
<PackageReference Include="Unchase.Swashbuckle.AspNetCore.Extensions" Version="2.7.1" />
```

#### Advanced Configuration
```csharp
services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Axon Backend API",
        Version = "v1",
        Description = "AI-powered chat system with MCP integration",
        Contact = new OpenApiContact
        {
            Name = "API Support",
            Email = "api@axon.com"
        },
        License = new OpenApiLicense
        {
            Name = "Proprietary",
            Url = new Uri("https://axon.com/license")
        }
    });
    
    // Security definitions
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "JWT Bearer token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
    
    // Enable annotations
    options.EnableAnnotations();
    
    // Custom schema IDs
    options.CustomSchemaIds(type => type.FullName?.Replace("+", "."));
    
    // XML documentation
    var xmlFiles = Directory.GetFiles(AppContext.BaseDirectory, "*.xml");
    foreach (var xmlFile in xmlFiles)
    {
        options.IncludeXmlComments(xmlFile);
    }
});
```

### Scalar (Modern API Documentation)
**Version**: 1.2.64  
**License**: MIT  
**Purpose**: Modern, interactive API documentation UI  
**Features**: Dark mode, request builder, code generation

```xml
<PackageReference Include="Scalar.AspNetCore" Version="1.2.64" />
```

### API Versioning
**Version**: 8.1.0  
**License**: MIT  
**Purpose**: RESTful API versioning support  
**Strategy**: URL path versioning (v1, v2)

#### Packages
```xml
<PackageReference Include="Asp.Versioning.Abstractions" Version="8.1.0" />
<PackageReference Include="Asp.Versioning.Http" Version="8.1.0" />
<PackageReference Include="Asp.Versioning.Mvc" Version="8.1.0" />
<PackageReference Include="Asp.Versioning.Mvc.ApiExplorer" Version="8.1.0" />
```

## Data Access

### Entity Framework Core
**Version**: 9.0.0  
**License**: MIT  
**Purpose**: Object-Relational Mapping (ORM)  
**Performance**: Query compilation caching, connection pooling

#### Packages
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Relational" Version="9.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="9.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.0.0" />
<PackageReference Include="EFCore.NamingConventions" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore" Version="9.0.0" />
```

#### Advanced Configuration
```csharp
public class ChatDbContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "chat");
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
                npgsqlOptions.CommandTimeout(30);
                npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            })
            .UseSnakeCaseNamingConvention()
            .EnableSensitiveDataLogging(isDevelopment)
            .EnableDetailedErrors(isDevelopment)
            .UseLoggerFactory(loggerFactory)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Schema configuration
        modelBuilder.HasDefaultSchema("chat");
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        
        // Global query filters
        modelBuilder.Entity<Conversation>()
            .HasQueryFilter(c => !c.IsDeleted);
        
        // Value conversions
        modelBuilder.Entity<Message>()
            .Property(m => m.Status)
            .HasConversion<string>();
        
        // Indexes
        modelBuilder.Entity<Conversation>()
            .HasIndex(c => c.UserId)
            .HasDatabaseName("ix_conversations_user_id")
            .IncludeProperties(c => new { c.Title, c.CreatedAt });
    }
}
```

### PostgreSQL Driver (Npgsql)
**Version**: 9.0.0  
**License**: PostgreSQL License  
**Purpose**: PostgreSQL database driver  
**Features**: JSONB, arrays, full-text search, COPY operations

#### Packages
```xml
<PackageReference Include="Npgsql" Version="9.0.0" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.0" />
```

#### Advanced PostgreSQL Features
```csharp
// JSONB column for flexible schema
modelBuilder.Entity<Message>()
    .Property(m => m.ToolExecutions)
    .HasColumnType("jsonb")
    .HasConversion(
        v => JsonSerializer.Serialize(v, jsonOptions),
        v => JsonSerializer.Deserialize<List<ToolExecution>>(v, jsonOptions));

// Array column types
modelBuilder.Entity<User>()
    .Property(u => u.Roles)
    .HasColumnType("text[]");

// Full-text search configuration
modelBuilder.Entity<Conversation>()
    .HasIndex(c => c.Title)
    .HasMethod("gin")
    .HasOperators("gin_trgm_ops");

// Generated columns
modelBuilder.Entity<Message>()
    .Property(m => m.SearchVector)
    .HasComputedColumnSql(
        "to_tsvector('english', content)", 
        stored: true);
```

### Sieve (Dynamic Filtering & Sorting)
**Version**: 2.5.5  
**License**: Apache 2.0  
**Purpose**: Dynamic filtering, sorting, and pagination  
**Security**: Input validation and SQL injection prevention

```xml
<PackageReference Include="Sieve" Version="2.5.5" />
```

#### Configuration
```csharp
// Sieve configuration
public class ApplicationSieveProcessor : SieveProcessor
{
    protected override SievePropertyMapper MapProperties(SievePropertyMapper mapper)
    {
        mapper.Property<Conversation>(c => c.Title)
            .CanFilter()
            .CanSort();
            
        mapper.Property<Conversation>(c => c.CreatedAt)
            .CanFilter()
            .CanSort()
            .HasName("created");
            
        return mapper;
    }
}

// Usage in repository
public async Task<PagedResult<T>> GetPagedAsync(SieveModel sieveModel)
{
    var query = _context.Set<T>().AsQueryable();
    
    // Apply Sieve processing
    query = _sieveProcessor.Apply(sieveModel, query);
    
    var total = await query.CountAsync();
    var items = await query.ToListAsync();
    
    return new PagedResult<T>
    {
        Items = items,
        TotalCount = total,
        PageNumber = sieveModel.Page ?? 1,
        PageSize = sieveModel.PageSize ?? 10
    };
}
```

## Messaging & Events

### MediatR
**Version**: 12.4.1 / 13.0.0  
**License**: Apache 2.0  
**Purpose**: Mediator pattern for CQRS  
**Pattern**: In-process message bus

```xml
<PackageReference Include="MediatR" Version="13.0.0" />
```

#### Pipeline Configuration
```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    
    // Pipeline behaviors (order matters!)
    cfg.AddBehavior<IPipelineBehavior<,>, LoggingBehavior<,>>();
    cfg.AddBehavior<IPipelineBehavior<,>, ValidationBehavior<,>>();
    cfg.AddBehavior<IPipelineBehavior<,>, CachingBehavior<,>>();
    cfg.AddBehavior<IPipelineBehavior<,>, TransactionBehavior<,>>();
    cfg.AddBehavior<IPipelineBehavior<,>, MetricsBehavior<,>>();
    cfg.AddBehavior<IPipelineBehavior<,>, RetryBehavior<,>>();
    
    // Notification behaviors
    cfg.NotificationPublisher = new TaskWhenAllPublisher();
    cfg.NotificationPublisherType = typeof(TaskWhenAllPublisher);
});
```

### MassTransit
**Version**: 8.3.6  
**License**: Apache 2.0  
**Purpose**: Distributed application framework  
**Transports**: RabbitMQ, Azure Service Bus, Amazon SQS

#### Packages
```xml
<PackageReference Include="MassTransit" Version="8.3.6" />
<PackageReference Include="MassTransit.RabbitMQ" Version="8.3.6" />
```

#### Advanced Configuration
```csharp
services.AddMassTransit(x =>
{
    // Consumer registration
    x.AddConsumer<ConversationCreatedConsumer>()
        .Endpoint(e => e.Name = "conversation-created");
    x.AddConsumer<MessageProcessedConsumer>();
    
    // Saga registration
    x.AddSagaStateMachine<ConversationStateMachine, ConversationState>()
        .InMemoryRepository();
    
    // Transport configuration
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("rabbitmq://localhost", h =>
        {
            h.Username("guest");
            h.Password("guest");
            h.PublisherConfirmation = true;
        });
        
        // Retry policies
        cfg.UseMessageRetry(r => r.Exponential(5, 
            TimeSpan.FromSeconds(1), 
            TimeSpan.FromSeconds(30), 
            TimeSpan.FromSeconds(3)));
        
        // Circuit breaker
        cfg.UseCircuitBreaker(cb =>
        {
            cb.TrackingPeriod = TimeSpan.FromMinutes(1);
            cb.TripThreshold = 15;
            cb.ActiveThreshold = 10;
            cb.ResetInterval = TimeSpan.FromMinutes(5);
        });
        
        // Rate limiting
        cfg.UseRateLimit(100, TimeSpan.FromMinutes(1));
        
        // Outbox pattern
        cfg.UseInMemoryOutbox();
        
        cfg.ConfigureEndpoints(context);
    });
});
```

### EventStore Client
**Version**: 23.3.7  
**License**: Apache 2.0  
**Purpose**: Event sourcing and event streaming  
**Features**: Event streams, projections, subscriptions

```xml
<PackageReference Include="EventStore.Client.Grpc.Streams" Version="23.3.7" />
```

#### Event Store Integration
```csharp
// Client configuration
services.AddSingleton(provider =>
{
    var settings = EventStoreClientSettings.Create("esdb://localhost:2113?tls=false");
    settings.ConnectionName = "axon-backend";
    settings.DefaultCredentials = new UserCredentials("admin", "changeit");
    return new EventStoreClient(settings);
});

// Event sourcing implementation
public class EventStoreRepository : IEventStore
{
    public async Task SaveEventsAsync(
        Guid aggregateId, 
        IEnumerable<object> events,
        int expectedVersion)
    {
        var streamName = $"conversation-{aggregateId}";
        var eventData = events.Select(e => new EventData(
            Uuid.NewUuid(),
            e.GetType().Name,
            JsonSerializer.SerializeToUtf8Bytes(e),
            metadata: CreateMetadata(e)
        ));
        
        await _client.AppendToStreamAsync(
            streamName,
            expectedVersion == -1 ? StreamState.Any : StreamRevision.FromInt64(expectedVersion),
            eventData);
    }
    
    public async Task<List<object>> GetEventsAsync(Guid aggregateId)
    {
        var streamName = $"conversation-{aggregateId}";
        var events = new List<object>();
        
        await foreach (var @event in _client.ReadStreamAsync(
            Direction.Forwards,
            streamName,
            StreamPosition.Start))
        {
            var eventType = Type.GetType(@event.Event.EventType);
            var eventData = JsonSerializer.Deserialize(
                @event.Event.Data.Span, 
                eventType!);
            events.Add(eventData!);
        }
        
        return events;
    }
}
```

## Caching

### EasyCaching
**Version**: 1.9.2  
**License**: MIT  
**Purpose**: Caching abstraction with multiple providers  
**Providers**: Memory, Redis, SQLite, Memcached

#### Packages
```xml
<PackageReference Include="EasyCaching.Core" Version="1.9.2" />
<PackageReference Include="EasyCaching.InMemory" Version="1.9.2" />
```

#### Hybrid Caching Configuration
```csharp
services.AddEasyCaching(options =>
{
    // L1 Cache - In-memory
    options.UseInMemory(config =>
    {
        config.DBConfig = new InMemoryCachingOptions
        {
            ExpirationScanFrequency = 60,
            SizeLimit = 1000,
            EnableReadDeepClone = true,
            EnableWriteDeepClone = false
        };
        config.MaxRdSecond = 120;
        config.EnableLogging = true;
    }, "l1cache");
    
    // L2 Cache - Redis
    options.UseRedis(config =>
    {
        config.DBConfig.Endpoints.Add(new ServerEndPoint("localhost", 6379));
        config.DBConfig.Password = "";
        config.DBConfig.Database = 0;
        config.DBConfig.AllowAdmin = true;
        config.DBConfig.ConnectionTimeout = 5000;
        config.DBConfig.SyncTimeout = 5000;
        config.SerializerName = "msgpack";
    }, "l2cache");
    
    // Hybrid cache combining L1 and L2
    options.UseHybrid(config =>
    {
        config.TopicName = "cache-sync";
        config.EnableLogging = true;
        config.LocalCacheProviderName = "l1cache";
        config.DistributedCacheProviderName = "l2cache";
    });
    
    // Message pack serialization
    options.WithMessagePack();
});
```

## Authentication & Security

### JWT Bearer Authentication
**Version**: 9.0.0  
**License**: MIT  
**Purpose**: JWT token-based authentication  
**Algorithm**: RS256 (recommended) or HS256

```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="9.0.0" />
```

#### JWT Configuration
```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = configuration["Jwt:Issuer"],
            ValidAudiences = configuration.GetSection("Jwt:Audiences").Get<string[]>(),
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!)),
            ClockSkew = TimeSpan.Zero,
            RequireExpirationTime = true,
            RequireSignedTokens = true
        };
        
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                if (context.Exception is SecurityTokenExpiredException)
                {
                    context.Response.Headers.Append("Token-Expired", "true");
                }
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var telemetry = context.HttpContext.RequestServices
                    .GetRequiredService<ITelemetryService>();
                telemetry.RecordAuthentication(context.Principal);
                return Task.CompletedTask;
            },
            OnMessageReceived = context =>
            {
                // Support token from query string for SignalR/SSE
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                
                if (!string.IsNullOrEmpty(accessToken) && 
                    path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
        
        options.SaveToken = true;
        options.RequireHttpsMetadata = !isDevelopment;
    });
```

### Duende IdentityServer
**Version**: 7.0.8  
**License**: Reciprocal Public License (RPL)  
**Purpose**: OpenID Connect and OAuth 2.0 server  
**Note**: Commercial license required for production

#### Packages
```xml
<PackageReference Include="Duende.IdentityServer" Version="7.0.8" />
<PackageReference Include="Duende.IdentityServer.AspNetIdentity" Version="7.0.8" />
<PackageReference Include="Duende.IdentityServer.EntityFramework" Version="7.0.8" />
<PackageReference Include="Duende.IdentityServer.EntityFramework.Storage" Version="7.0.8" />
```

### ASP.NET Core Identity
**Version**: 9.0.0  
**License**: MIT  
**Purpose**: User management and authentication  
**Features**: Password hashing, 2FA, account lockout

```xml
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="9.0.0" />
```

## Validation

### FluentValidation
**Version**: 11.11.0  
**License**: Apache 2.0  
**Purpose**: Fluent validation rules  
**Integration**: MediatR pipeline, ASP.NET Core

#### Packages
```xml
<PackageReference Include="FluentValidation" Version="11.11.0" />
<PackageReference Include="FluentValidation.AspNetCore" Version="11.3.0" />
```

#### Advanced Validation Patterns
```csharp
public class ProcessMessageValidator : AbstractValidator<ProcessMessageCommand>
{
    public ProcessMessageValidator(IUserService userService)
    {
        // Basic validations
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message is required")
            .MaximumLength(4000).WithMessage("Message exceeds maximum length")
            .Must(NotContainMaliciousContent).WithMessage("Invalid content detected");
        
        // Conditional validation
        RuleFor(x => x.ConversationId)
            .NotEqual(Guid.Empty)
            .When(x => x.ConversationId.HasValue)
            .WithMessage("Invalid conversation ID");
        
        // Async validation with dependency
        RuleFor(x => x.UserId)
            .MustAsync(async (userId, ct) => await userService.IsActiveAsync(userId, ct))
            .WithMessage("User must be active")
            .WithErrorCode("USER_INACTIVE");
        
        // Complex nested validation
        RuleForEach(x => x.Attachments)
            .SetValidator(new AttachmentValidator());
        
        // Custom severity levels
        RuleFor(x => x.Priority)
            .InclusiveBetween(1, 5)
            .WithSeverity(Severity.Warning);
    }
    
    private bool NotContainMaliciousContent(string content)
    {
        // Security validation logic
        return !content.Contains("<script>", StringComparison.OrdinalIgnoreCase);
    }
}
```

### Ardalis.GuardClauses
**Version**: 5.0.0  
**License**: MIT  
**Purpose**: Guard clause extensions  
**Pattern**: Fail-fast validation

```xml
<PackageReference Include="Ardalis.GuardClauses" Version="5.0.0" />
```

## Mapping & Serialization

### Mapster
**Version**: 7.4.0  
**License**: MIT  
**Purpose**: High-performance object mapping  
**Performance**: 4x faster than AutoMapper

#### Packages
```xml
<PackageReference Include="Mapster" Version="7.4.0" />
<PackageReference Include="Mapster.DependencyInjection" Version="1.0.1" />
```

#### Configuration
```csharp
// Global configuration
TypeAdapterConfig.GlobalSettings.Scan(Assembly.GetExecutingAssembly());
TypeAdapterConfig.GlobalSettings.RequireExplicitMapping = true;
TypeAdapterConfig.GlobalSettings.RequireDestinationMemberSource = true;

// Custom type mappings
TypeAdapterConfig<Conversation, ConversationDto>
    .NewConfig()
    .Map(dest => dest.MessageCount, src => src.Messages.Count)
    .Map(dest => dest.LastActivity, src => src.Messages.Max(m => m.CreatedAt))
    .Map(dest => dest.Tags, src => src.Tags.Select(t => t.Name).ToList())
    .PreserveReference(true)
    .AfterMapping((src, dest) =>
    {
        dest.Status = src.State.ToString();
        dest.IsActive = src.State == ConversationState.Active;
    });
```

### System.Text.Json
**Version**: Built-in (.NET 10)  
**License**: MIT  
**Purpose**: High-performance JSON serialization  
**Performance**: 2x faster than Newtonsoft.Json

#### Configuration
```csharp
var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    ReferenceHandler = ReferenceHandler.IgnoreCycles,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    Converters =
    {
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
        new DateOnlyJsonConverter(),
        new TimeOnlyJsonConverter(),
        new StrongIdJsonConverterFactory()
    },
    TypeInfoResolver = new DefaultJsonTypeInfoResolver()
};
```

### Newtonsoft.Json
**Version**: 13.0.3  
**License**: MIT  
**Purpose**: JSON serialization (legacy support)  
**Note**: Use only for backward compatibility

```xml
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
```

## Testing

### Testing Frameworks

#### NUnit
**Version**: 4.x  
**License**: MIT  
**Purpose**: Primary unit testing framework  
**Features**: Parallel execution, data-driven tests

```xml
<PackageReference Include="NUnit" Version="4.0.1" />
<PackageReference Include="NUnit3TestAdapter" Version="4.5.0" />
<PackageReference Include="NUnit.Analyzers" Version="3.9.0" />
```

#### xUnit (Components)
**Version**: 2.9.2  
**License**: Apache 2.0  
**Purpose**: Testing abstractions and logging

```xml
<PackageReference Include="xunit.abstractions" Version="2.0.3" />
<PackageReference Include="xunit.extensibility.core" Version="2.9.2" />
<PackageReference Include="Xunit.Extensions.Logging" Version="1.1.0" />
```

### Mocking & Assertions

#### NSubstitute
**Version**: 5.3.0  
**License**: BSD  
**Purpose**: Mocking framework  
**Syntax**: Fluent, readable

```xml
<PackageReference Include="NSubstitute" Version="5.3.0" />
```

#### Moq
**Version**: Latest  
**License**: BSD  
**Purpose**: Alternative mocking framework  
**Usage**: Legacy test support

```xml
<PackageReference Include="Moq" />
```

#### Shouldly
**Version**: Latest  
**License**: BSD  
**Purpose**: Assertion framework  
**Syntax**: Natural language assertions

```xml
<PackageReference Include="Shouldly" />
```

#### FluentAssertions
**Version**: 7.0.0  
**License**: Apache 2.0  
**Purpose**: Fluent assertion library  
**Features**: Deep object comparison

```xml
<PackageReference Include="FluentAssertions" Version="7.0.0" />
```

### Test Data Generation

#### Bogus
**Version**: 35.6.1  
**License**: MIT  
**Purpose**: Fake data generation  
**Locales**: 50+ language support

```xml
<PackageReference Include="Bogus" Version="35.6.1" />
```

#### AutoBogus
**Version**: 2.13.1  
**License**: MIT  
**Purpose**: Automatic fake data generation  
**Integration**: Works with Bogus

```xml
<PackageReference Include="AutoBogus" Version="2.13.1" />
```

### Integration Testing

#### Testcontainers
**Version**: 4.0.0  
**License**: MIT  
**Purpose**: Integration testing with containers  
**Containers**: PostgreSQL, Redis, RabbitMQ, EventStore

```xml
<PackageReference Include="Testcontainers" Version="4.0.0" />
<PackageReference Include="Testcontainers.PostgreSql" Version="4.0.0" />
<PackageReference Include="Testcontainers.RabbitMq" Version="4.0.0" />
<PackageReference Include="Testcontainers.EventStoreDb" Version="4.0.0" />
```

#### Respawn
**Version**: 6.2.1  
**License**: Apache 2.0  
**Purpose**: Database cleanup for integration tests  
**Performance**: Fast database reset

```xml
<PackageReference Include="Respawn" Version="6.2.1" />
```

#### ASP.NET Core Test Host
**Version**: 9.0.0  
**License**: MIT  
**Purpose**: In-memory API testing

```xml
<PackageReference Include="Microsoft.AspNetCore.TestHost" Version="9.0.0" />
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.0" />
<PackageReference Include="WebMotions.Fake.Authentication.JwtBearer" Version="8.0.1" />
```

### Performance Testing

#### BenchmarkDotNet
**Version**: Latest  
**License**: MIT  
**Purpose**: Micro-benchmarking framework  
**Features**: Statistical analysis, memory diagnostics

```xml
<PackageReference Include="BenchmarkDotNet" />
```

## Observability & Logging

### OpenTelemetry
**Version**: 1.11.1  
**License**: Apache 2.0  
**Purpose**: Distributed tracing and metrics  
**Backends**: Jaeger, Zipkin, OTLP, Prometheus

#### Comprehensive Package List
```xml
<!-- Core -->
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.11.1" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.11.1"/>

<!-- Instrumentation -->
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.11.0"/>
<PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.11.0"/>
<PackageReference Include="OpenTelemetry.Instrumentation.GrpcNetClient" Version="1.11.0-beta.1"/>
<PackageReference Include="OpenTelemetry.Instrumentation.Process" Version="1.11.0-beta.1"/>
<PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.11.0"/>

<!-- Exporters -->
<PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" Version="1.11.0-beta.1"/>
<PackageReference Include="OpenTelemetry.Exporter.Console" Version="1.11.1"/>
<PackageReference Include="OpenTelemetry.Exporter.Zipkin" Version="1.11.1"/>

<!-- Grafana Integration -->
<PackageReference Include="Grafana.OpenTelemetry" Version="1.2.0"/>
```

#### Configuration
```csharp
services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "axon-backend", serviceVersion: version)
        .AddAttributes(new Dictionary<string, object>
        {
            ["environment"] = environment,
            ["deployment.environment"] = environment,
            ["service.namespace"] = "axon",
            ["service.instance.id"] = Environment.MachineName
        }))
    .WithTracing(tracing => tracing
        .SetSampler(new TraceIdRatioBasedSampler(0.1)) // 10% sampling
        .AddAspNetCoreInstrumentation(options =>
        {
            options.RecordException = true;
            options.Filter = (httpContext) => 
                !httpContext.Request.Path.StartsWithSegments("/health");
        })
        .AddHttpClientInstrumentation(options =>
        {
            options.RecordException = true;
            options.FilterHttpRequestMessage = (httpRequestMessage) =>
                !httpRequestMessage.RequestUri?.Host.Contains("localhost") ?? true;
        })
        .AddEntityFrameworkCoreInstrumentation(options =>
        {
            options.SetDbStatementForText = true;
            options.SetDbStatementForStoredProcedure = true;
        })
        .AddSource("MassTransit")
        .AddSource("Axon.*")
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("http://localhost:4317");
            options.Protocol = OtlpExportProtocol.Grpc;
        }))
    .WithMetrics(metrics => metrics
        .AddMeter("Axon.*")
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddProcessInstrumentation()
        .AddPrometheusExporter(options =>
        {
            options.StartHttpListener = true;
            options.HttpListenerPrefixes = new[] { "http://localhost:9090/" };
        }));
```

### Logging (Microsoft.Extensions.Logging)
**Version**: 9.0.0  
**License**: MIT  
**Purpose**: Structured logging abstraction  
**Providers**: Console, Debug, EventSource, EventLog

```xml
<PackageReference Include="Microsoft.Extensions.Logging" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Configuration" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Console" Version="9.0.0" />
```

#### Structured Logging Pattern
```csharp
// High-performance logging with LoggerMessage
public static class LoggerExtensions
{
    private static readonly Action<ILogger, string, double, Exception?> _requestProcessed =
        LoggerMessage.Define<string, double>(
            LogLevel.Information,
            new EventId(1000, "RequestProcessed"),
            "Request {RequestId} processed in {Duration}ms");
    
    public static void LogRequestProcessed(this ILogger logger, string requestId, double duration)
        => _requestProcessed(logger, requestId, duration, null);
}
```

### Health Checks
**Version**: 9.0.0  
**License**: MIT  
**Purpose**: Application health monitoring  
**Integrations**: Database, Redis, RabbitMQ, HTTP endpoints

#### Packages
```xml
<PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks" Version="9.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.UI" Version="9.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.UI.Client" Version="9.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.UI.InMemory.Storage" Version="9.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.Npgsql" Version="9.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.Rabbitmq" Version="9.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.EventStore" Version="9.0.0" />
```

## Resilience & Fault Tolerance

### Polly
**Version**: 8.5.0  
**License**: BSD  
**Purpose**: Resilience and transient-fault handling  
**Patterns**: Retry, Circuit Breaker, Timeout, Bulkhead, Cache

#### Packages
```xml
<PackageReference Include="Polly" Version="8.5.0" />
<PackageReference Include="Polly.Extensions.Http" Version="3.0.0" />
<PackageReference Include="Microsoft.Extensions.Http.Polly" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="9.0.0" />
```

#### Resilience Pipeline
```csharp
services.AddHttpClient<IAiClient, OpenAiClient>()
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromSeconds(1);
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;
        
        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.MinimumThroughput = 10;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
        
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
    });
```

### Microsoft.Extensions.ServiceDiscovery
**Version**: 9.3.1  
**License**: MIT  
**Purpose**: Service discovery and load balancing  
**Providers**: DNS, Configuration, Kubernetes

```xml
<PackageReference Include="Microsoft.Extensions.ServiceDiscovery" Version="9.3.1" />
```

## Background Processing

### Custom Background Services
**Purpose**: Long-running background tasks  
**Pattern**: IHostedService, BackgroundService  
**Usage**: Message processing, cleanup tasks

```csharp
public class PersistMessageBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly PersistMessageOptions _options;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var processor = scope.ServiceProvider
                    .GetRequiredService<IPersistMessageProcessor>();
                    
                await processor.ProcessPendingMessagesAsync(stoppingToken);
                await Task.Delay(_options.PollingInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in background message processing");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
```

## Development Tools

### Scrutor
**Version**: 5.0.2  
**License**: MIT  
**Purpose**: Assembly scanning and decoration  
**Features**: Auto-registration, decorators

```xml
<PackageReference Include="Scrutor" Version="5.0.2" />
```

### Humanizer
**Version**: 2.14.1  
**License**: MIT  
**Purpose**: String manipulation and formatting  
**Features**: Pluralization, date humanizing

```xml
<PackageReference Include="Humanizer.Core" Version="2.14.1" />
```

### IdGen
**Version**: 3.0.7  
**License**: MIT  
**Purpose**: Distributed ID generation  
**Algorithm**: Twitter Snowflake

```xml
<PackageReference Include="IdGen" Version="3.0.7" />
```

### Figgle
**Version**: 0.5.1  
**License**: Apache 2.0  
**Purpose**: ASCII art generation  
**Usage**: Startup banners

```xml
<PackageReference Include="Figgle" Version="0.5.1" />
```

### YARP (Yet Another Reverse Proxy)
**Version**: 2.2.0  
**License**: MIT  
**Purpose**: Reverse proxy functionality  
**Features**: Load balancing, health checks

```xml
<PackageReference Include="Yarp.ReverseProxy" Version="2.2.0" />
```

### System.Linq.Async
**Version**: 6.0.1  
**License**: MIT  
**Purpose**: Async LINQ operations  
**Features**: IAsyncEnumerable support

```xml
<PackageReference Include="System.Linq.Async" Version="6.0.1" />
<PackageReference Include="System.Linq.Async.Queryable" Version="6.0.1" />
```

## Build & Deployment

### gRPC Support
**Purpose**: High-performance RPC framework  
**Protocol**: HTTP/2, Protocol Buffers

```xml
<PackageReference Include="Google.Protobuf" Version="3.29.1" />
<PackageReference Include="Grpc.Core.Testing" Version="2.46.6" />
<PackageReference Include="Grpc.Net.ClientFactory" Version="2.67.0" />
```

### EasyNetQ Management
**Version**: 3.0.0  
**Purpose**: RabbitMQ management API client  
**Features**: Queue management, monitoring

```xml
<PackageReference Include="EasyNetQ.Management.Client" Version="3.0.0" />
```

## Dependency Management

### Directory.Build.props
Central configuration for all projects:
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>13</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
  </PropertyGroup>
  
  <ItemGroup>
    <Using Include="System.Text.Json" />
    <Using Include="Microsoft.Extensions.Logging" />
    <Using Include="MediatR" />
  </ItemGroup>
</Project>
```

### Central Package Management
Directory.Packages.props:
```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>
  
  <ItemGroup>
    <!-- Framework packages -->
    <PackageVersion Include="Microsoft.AspNetCore.*" Version="9.0.0" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.*" Version="9.0.0" />
    <PackageVersion Include="Microsoft.Extensions.*" Version="9.0.0" />
    
    <!-- Third-party packages -->
    <PackageVersion Include="MediatR" Version="13.0.0" />
    <PackageVersion Include="FluentValidation" Version="11.11.0" />
    <PackageVersion Include="Polly" Version="8.5.0" />
    <!-- ... other packages ... -->
  </ItemGroup>
</Project>
```

## Version Strategy

### Versioning Policy

#### Framework & Runtime
- **.NET**: Track LTS versions, update within 3 months
- **C#**: Use latest stable language version
- **Preview Features**: Test in development, avoid in production

#### Dependency Categories

1. **Critical Dependencies** (Immediate updates)
   - Security libraries
   - Authentication packages
   - Cryptography libraries

2. **Core Dependencies** (Quarterly review)
   - Entity Framework Core
   - MediatR
   - FluentValidation
   - Polly

3. **Utility Dependencies** (Bi-annual review)
   - Humanizer
   - Figgle
   - IdGen

4. **Development Dependencies** (Flexible)
   - Testing frameworks
   - Mocking libraries
   - Code analyzers

### Update Process

```bash
# Check outdated packages
dotnet list package --outdated --include-transitive

# Check vulnerable packages
dotnet list package --vulnerable --include-transitive

# Check deprecated packages
dotnet list package --deprecated

# Update specific package
dotnet add package PackageName --version X.Y.Z

# Update all packages (use with caution)
dotnet restore --force-evaluate
```

### Dependency Audit Checklist

- [ ] Weekly: Security vulnerability scan
- [ ] Monthly: Deprecated package review
- [ ] Quarterly: Performance impact analysis
- [ ] Bi-annually: License compliance check
- [ ] Annually: Major version migration planning

## Security Compliance

### Security Scanning Tools
```bash
# .NET Security Scanner
dotnet list package --vulnerable

# OWASP Dependency Check
dependency-check --project "Axon Backend" --scan .

# Snyk CLI
snyk test
```

### License Compliance
- **Approved**: MIT, Apache 2.0, BSD
- **Review Required**: LGPL, MPL
- **Restricted**: GPL, AGPL
- **Commercial**: Duende IdentityServer (RPL)

### Security Headers
```csharp
app.UseSecurityHeaders(policies =>
{
    policies.AddContentSecurityPolicy(builder =>
    {
        builder.AddDefaultSrc().Self();
        builder.AddScriptSrc().Self().UnsafeInline();
        builder.AddStyleSrc().Self().UnsafeInline();
    });
    policies.AddStrictTransportSecurity(maxAge: 31536000, includeSubDomains: true);
    policies.AddXContentTypeOptions();
    policies.AddXFrameOptions(XFrameOptionsDirective.Deny);
    policies.AddReferrerPolicy(ReferrerPolicyDirective.StrictOriginWhenCrossOrigin);
});
```

## Performance Optimization

### Package-Specific Optimizations

#### Entity Framework Core
- Use compiled queries for hot paths
- Enable query splitting for includes
- Implement connection pooling
- Use AsNoTracking for read-only queries

#### MediatR
- Avoid heavy operations in behaviors
- Use streaming for large result sets
- Implement caching behavior for queries

#### JSON Serialization
- Use source generators for AOT
- Implement custom converters for complex types
- Enable reference handling for circular dependencies

#### HTTP Clients
- Implement connection pooling
- Use IHttpClientFactory
- Configure appropriate timeouts
- Enable HTTP/2 where supported

### Memory Management
```csharp
// Use ArrayPool for temporary buffers
var pool = ArrayPool<byte>.Shared;
var buffer = pool.Rent(4096);
try
{
    // Use buffer
}
finally
{
    pool.Return(buffer, clearArray: true);
}

// Use Memory<T> and Span<T> for zero-allocation
ReadOnlySpan<char> span = text.AsSpan();
```

### Compilation Optimizations
```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <PublishTrimmed>true</PublishTrimmed>
  <PublishSingleFile>true</PublishSingleFile>
  <SelfContained>true</SelfContained>
  <DebugType>none</DebugType>
  <DebugSymbols>false</DebugSymbols>
</PropertyGroup>
```

---

**Document Version**: 2.0.0  
**Last Updated**: 2025-08-06  
**Next Review**: 2025-09-06  
**Maintained By**: Architecture Team

**References**:
- [.NET Documentation](https://docs.microsoft.com/dotnet)
- [NuGet Package Explorer](https://nuget.info)
- [Package Security Advisories](https://github.com/advisories)