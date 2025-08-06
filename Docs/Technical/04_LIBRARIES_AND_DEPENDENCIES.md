# 📦 Axon Backend - Libraries & Dependencies Reference# 📦 Axon Backend - Libraries & Dependencies Reference

## Table of Contents
- [Overview](#overview)
- [Core Framework](#core-framework)
- [Web & API](#web--api)
- [Data Access](#data-access)
- [Messaging & Events](#messaging--events)
- [Caching](#caching)
- [Authentication & Security](#authentication--security)
- [Validation](#validation)
- [Mapping & Serialization](#mapping--serialization)
- [Testing](#testing)
- [Observability](#observability)
- [Resilience & Fault Tolerance](#resilience--fault-tolerance)
- [Development Tools](#development-tools)
- [Dependency Management](#dependency-management)
- [Version Strategy](#version-strategy)

## Overview

This document provides comprehensive information about all libraries and dependencies used in the Axon Backend system, including version numbers, usage patterns, and implementation guidelines.
### Technology Stack
- **.NET 10**: Latest LTS framework
- **C# 13**: Modern language features
- **PostgreSQL 16**: Primary database
- **Redis**: Distributed caching
- **RabbitMQ**: Message broker
- **EventStore**: Event sourcing
- **Docker**: Containerization
- **Kubernetes**: Orchestration

## Core Framework

### .NET 10 & ASP.NET Core
**Version**: 10.0.0
**Purpose**: Core runtime and web framework
**Key Packages**:
```xml
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.0" />
```**Usage Patterns**:
```csharp
// Program.cs configuration
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

## Web & API

### FastEndpoints
**Version**: Latest compatible with .NET 10
**Purpose**: High-performance minimal API framework
**Implementation**:
```csharp
// Endpoint configuration
app.UseFastEndpoints(config =>
{
    config.Errors.ResponseBuilder = (failures, ctx, statusCode) =>
    {
        return new ProblemDetails
        {
            Type = "https://httpstatuses.com/" + statusCode,
            Title = "Validation Error",
            Status = statusCode,
            Detail = string.Join(", ", failures.Select(f => f.ErrorMessage))
        };
    };
});
```### Swashbuckle (OpenAPI/Swagger)
**Version**: 7.1.0
**Purpose**: API documentation and testing
**Packages**:
```xml
<PackageReference Include="Swashbuckle.AspNetCore" Version="7.1.0" />
<PackageReference Include="Swashbuckle.AspNetCore.SwaggerGen" Version="7.1.0" />
<PackageReference Include="Swashbuckle.AspNetCore.SwaggerUI" Version="7.1.0" />
<PackageReference Include="Unchase.Swashbuckle.AspNetCore.Extensions" Version="2.7.1" />
```

**Configuration**:
```csharp
services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Axon Backend API",
        Version = "v1",
        Description = "AI-powered chat system with MCP integration"
    });
    
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please enter JWT token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    
    options.EnableAnnotations();
    options.CustomSchemaIds(type => type.FullName);
});
```### Scalar (API Documentation)
**Version**: 1.2.64
**Purpose**: Modern API documentation UI
**Package**:
```xml
<PackageReference Include="Scalar.AspNetCore" Version="1.2.64" />
```

**Usage**:
```csharp
app.UseScalar(options =>
{
    options.Title = "Axon API Reference";
    options.Theme = ScalarTheme.Modern;
    options.ShowSidebar = true;
});
```

### API Versioning
**Version**: 8.1.0
**Purpose**: RESTful API versioning
**Packages**:
```xml
<PackageReference Include="Asp.Versioning.Abstractions" Version="8.1.0" />
<PackageReference Include="Asp.Versioning.Http" Version="8.1.0" />
<PackageReference Include="Asp.Versioning.Mvc" Version="8.1.0" />
<PackageReference Include="Asp.Versioning.Mvc.ApiExplorer" Version="8.1.0" />
```

## Data Access

### Entity Framework Core
**Version**: 9.0.0
**Purpose**: Object-Relational Mapping (ORM)
**Packages**:
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Relational" Version="9.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="9.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="9.0.0" />
<PackageReference Include="EFCore.NamingConventions" Version="9.0.0" />
```**Implementation Patterns**:
```csharp
// DbContext configuration
public class ChatDbContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseNpgsql(connectionString, options =>
            {
                options.MigrationsHistoryTable("__EFMigrationsHistory", "chat");
                options.EnableRetryOnFailure(3);
            })
            .UseSnakeCaseNamingConvention()
            .EnableSensitiveDataLogging(isDevelopment)
            .EnableDetailedErrors(isDevelopment);
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("chat");
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        
        // Global query filters
        modelBuilder.Entity<Conversation>()
            .HasQueryFilter(c => !c.IsDeleted);
    }
}
```

### PostgreSQL Driver (Npgsql)
**Version**: 9.0.0
**Purpose**: PostgreSQL database driver
**Packages**:
```xml
<PackageReference Include="Npgsql" Version="9.0.0" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.0" />
```

**Advanced Features Used**:
```csharp
// JSONB support for complex data
modelBuilder.Entity<Message>()
    .Property(m => m.ToolExecutions)
    .HasColumnType("jsonb");

// Array types
modelBuilder.Entity<User>()
    .Property(u => u.Roles)
    .HasColumnType("text[]");

// Full-text search
modelBuilder.Entity<Conversation>()
    .HasIndex(c => c.Title)
    .HasMethod("gin")
    .HasOperators("gin_trgm_ops");
```### Sieve (Filtering, Sorting, Pagination)
**Version**: 2.5.5
**Purpose**: Dynamic filtering and sorting
**Package**:
```xml
<PackageReference Include="Sieve" Version="2.5.5" />
```

**Usage Pattern**:
```csharp
// Apply Sieve processing
public async Task<PagedResult<ConversationDto>> GetConversations(SieveModel sieveModel)
{
    var query = _context.Conversations.AsQueryable();
    
    // Apply filtering, sorting, and pagination
    query = _sieveProcessor.Apply(sieveModel, query);
    
    var total = await query.CountAsync();
    var items = await query.ToListAsync();
    
    return new PagedResult<ConversationDto>(items, total);
}
```

## Messaging & Events

### MediatR
**Version**: 13.0.0
**Purpose**: Mediator pattern implementation for CQRS
**Package**:
```xml
<PackageReference Include="MediatR" Version="13.0.0" />
```**Implementation**:
```csharp
// Registration
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddBehavior<IPipelineBehavior<,>, ValidationBehavior<,>>();
    cfg.AddBehavior<IPipelineBehavior<,>, LoggingBehavior<,>>();
    cfg.AddBehavior<IPipelineBehavior<,>, TransactionBehavior<,>>();
    cfg.AddBehavior<IPipelineBehavior<,>, MetricsBehavior<,>>();
});

// Command/Query handling
public class ProcessMessageHandler : IRequestHandler<ProcessMessageCommand, Result<ProcessMessageResponse>>
{
    public async Task<Result<ProcessMessageResponse>> Handle(
        ProcessMessageCommand request,
        CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

### MassTransit
**Version**: 8.3.6
**Purpose**: Distributed application framework with message broker support
**Packages**:
```xml
<PackageReference Include="MassTransit" Version="8.3.6" />
<PackageReference Include="MassTransit.RabbitMQ" Version="8.3.6" />
```**Configuration**:
```csharp
services.AddMassTransit(x =>
{
    x.AddConsumer<ConversationCreatedConsumer>();
    x.AddConsumer<MessageProcessedConsumer>();
    
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("rabbitmq://localhost", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });
        
        cfg.ConfigureEndpoints(context);
        
        // Retry policy
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
    });
});
```### EventStore Client
**Version**: 23.3.7
**Purpose**: Event sourcing and event streaming
**Package**:
```xml
<PackageReference Include="EventStore.Client.Grpc.Streams" Version="23.3.7" />
```

**Usage**:
```csharp
// Event store client setup
services.AddSingleton(provider =>
{
    var settings = EventStoreClientSettings.Create("esdb://localhost:2113?tls=false");
    return new EventStoreClient(settings);
});

// Append events
public async Task SaveEvents(Guid aggregateId, IEnumerable<object> events)
{
    var streamName = $"conversation-{aggregateId}";
    var eventData = events.Select(e => new EventData(
        Uuid.NewUuid(),
        e.GetType().Name,
        JsonSerializer.SerializeToUtf8Bytes(e)
    ));
    
    await _client.AppendToStreamAsync(
        streamName,
        StreamState.Any,
        eventData);
}
```## Caching

### EasyCaching
**Version**: 1.9.2
**Purpose**: Caching abstraction with multiple providers
**Packages**:
```xml
<PackageReference Include="EasyCaching.Core" Version="1.9.2" />
<PackageReference Include="EasyCaching.InMemory" Version="1.9.2" />
```

**Configuration**:
```csharp
services.AddEasyCaching(options =>
{
    // In-memory cache
    options.UseInMemory(config =>
    {
        config.DBConfig = new InMemoryCachingOptions
        {
            ExpirationScanFrequency = 60,
            SizeLimit = 100,
            EnableReadDeepClone = false,
            EnableWriteDeepClone = false
        };
        config.MaxRdSecond = 120;
        config.EnableLogging = true;
    });
    
    // Redis cache
    options.UseRedis(config =>
    {
        config.DBConfig.Endpoints.Add(new ServerEndPoint("localhost", 6379));
        config.DBConfig.Password = "";
        config.DBConfig.Database = 0;
        config.DBConfig.AllowAdmin = true;
    }, "redis");
    
    // Hybrid cache (L1 + L2)
    options.UseHybrid(config =>
    {
        config.TopicName = "cache-sync";
        config.EnableLogging = true;
        config.LocalCacheProviderName = "memory";
        config.DistributedCacheProviderName = "redis";
    });
});
```## Authentication & Security

### JWT Bearer Authentication
**Version**: 9.0.0
**Purpose**: JWT token-based authentication
**Package**:
```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="9.0.0" />
```

**Configuration**:
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
            ValidAudience = configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!))
        };
        
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // Support token from query string for SignalR
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });
```### Duende IdentityServer
**Version**: 7.0.8
**Purpose**: OpenID Connect and OAuth 2.0 framework
**Packages**:
```xml
<PackageReference Include="Duende.IdentityServer" Version="7.0.8" />
<PackageReference Include="Duende.IdentityServer.AspNetIdentity" Version="7.0.8" />
<PackageReference Include="Duende.IdentityServer.EntityFramework" Version="7.0.8" />
<PackageReference Include="Duende.IdentityServer.EntityFramework.Storage" Version="7.0.8" />
```

**Setup**:
```csharp
services.AddIdentityServer(options =>
    {
        options.Events.RaiseErrorEvents = true;
        options.Events.RaiseInformationEvents = true;
        options.Events.RaiseFailureEvents = true;
        options.Events.RaiseSuccessEvents = true;
    })
    .AddInMemoryIdentityResources(Config.IdentityResources)
    .AddInMemoryApiScopes(Config.ApiScopes)
    .AddInMemoryClients(Config.Clients)
    .AddAspNetIdentity<ApplicationUser>()
    .AddDeveloperSigningCredential(); // For development only
```

### ASP.NET Core Identity
**Version**: 9.0.0
**Purpose**: User management and authentication
**Package**:
```xml
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="9.0.0" />
```## Validation

### FluentValidation
**Version**: 11.11.0
**Purpose**: Fluent validation rules
**Packages**:
```xml
<PackageReference Include="FluentValidation" Version="11.11.0" />
<PackageReference Include="FluentValidation.AspNetCore" Version="11.3.0" />
```

**Usage Pattern**:
```csharp
public class ProcessMessageValidator : AbstractValidator<ProcessMessageCommand>
{
    public ProcessMessageValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message is required")
            .MaximumLength(4000).WithMessage("Message too long");
            
        RuleFor(x => x.ConversationId)
            .NotEqual(Guid.Empty).When(x => x.ConversationId.HasValue)
            .WithMessage("Invalid conversation ID");
            
        RuleFor(x => x.UserId)
            .MustAsync(BeActiveUser)
            .WithMessage("User must be active");
    }
    
    private async Task<bool> BeActiveUser(long? userId, CancellationToken ct)
    {
        if (!userId.HasValue) return false;
        return await _userService.IsActiveAsync(userId.Value, ct);
    }
}
```### Ardalis.GuardClauses
**Version**: 5.0.0
**Purpose**: Guard clause extensions for validation
**Package**:
```xml
<PackageReference Include="Ardalis.GuardClauses" Version="5.0.0" />
```

**Usage**:
```csharp
public class ConversationService
{
    public async Task<Conversation> GetConversationAsync(Guid id)
    {
        Guard.Against.Default(id, nameof(id));
        
        var conversation = await _repository.GetByIdAsync(id);
        Guard.Against.NotFound(id, conversation, nameof(conversation));
        
        return conversation;
    }
    
    public void UpdateTitle(string title)
    {
        Guard.Against.NullOrWhiteSpace(title, nameof(title));
        Guard.Against.OutOfRange(title.Length, nameof(title), 1, 200);
        
        Title = title;
    }
}
```## Mapping & Serialization

### Mapster
**Version**: 7.4.0
**Purpose**: High-performance object mapping
**Packages**:
```xml
<PackageReference Include="Mapster" Version="7.4.0" />
<PackageReference Include="Mapster.DependencyInjection" Version="1.0.1" />
```

**Configuration**:
```csharp
// Global configuration
TypeAdapterConfig.GlobalSettings.Scan(Assembly.GetExecutingAssembly());

// Custom mappings
TypeAdapterConfig<Conversation, ConversationDto>
    .NewConfig()
    .Map(dest => dest.MessageCount, src => src.Messages.Count)
    .Map(dest => dest.LastActivity, src => src.Messages.Max(m => m.CreatedAt))
    .AfterMapping((src, dest) =>
    {
        dest.Status = src.State.ToString();
    });

// Usage
var dto = conversation.Adapt<ConversationDto>();
var dtos = conversations.Adapt<List<ConversationDto>>();
```### Newtonsoft.Json
**Version**: 13.0.3
**Purpose**: JSON serialization (legacy support)
**Package**:
```xml
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
```

**Note**: Used for legacy compatibility. New code should use System.Text.Json.

### System.Text.Json
**Version**: Built-in with .NET 10
**Purpose**: High-performance JSON serialization
**Usage**:
```csharp
// Configure options
var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters =
    {
        new JsonStringEnumConverter(),
        new StrongIdJsonConverterFactory()
    }
};

// Serialization
var json = JsonSerializer.Serialize(obj, jsonOptions);
var obj = JsonSerializer.Deserialize<T>(json, jsonOptions);
```## Testing

### xUnit
**Version**: 2.9.2
**Purpose**: Unit testing framework
**Packages**:
```xml
<PackageReference Include="xunit.abstractions" Version="2.0.3" />
<PackageReference Include="xunit.extensibility.core" Version="2.9.2" />
<PackageReference Include="Xunit.Extensions.Logging" Version="1.1.0" />
```

### NSubstitute
**Version**: 5.3.0
**Purpose**: Mocking framework
**Package**:
```xml
<PackageReference Include="NSubstitute" Version="5.3.0" />
```

**Usage**:
```csharp
// Create mock
var repository = Substitute.For<IConversationRepository>();
repository.GetByIdAsync(Arg.Any<Guid>())
    .Returns(Task.FromResult(new Conversation()));

// Verify calls
await repository.Received(1).GetByIdAsync(conversationId);
```### FluentAssertions
**Version**: 7.0.0
**Purpose**: Fluent assertion library
**Package**:
```xml
<PackageReference Include="FluentAssertions" Version="7.0.0" />
```

**Usage**:
```csharp
// Assertions
result.Should().BeSuccess();
result.Value.Should().NotBeNull();
result.Value.ConversationId.Should().Be(expectedId);

conversation.Messages.Should()
    .HaveCount(2)
    .And.ContainSingle(m => m.Role == MessageRole.User)
    .And.ContainSingle(m => m.Role == MessageRole.Assistant);
```

### Bogus & AutoBogus
**Version**: 35.6.1 / 2.13.1
**Purpose**: Test data generation
**Packages**:
```xml
<PackageReference Include="Bogus" Version="35.6.1" />
<PackageReference Include="AutoBogus" Version="2.13.1" />
```**Usage**:
```csharp
// Generate test data
var faker = new Faker<ConversationDto>()
    .RuleFor(c => c.Id, f => f.Random.Guid())
    .RuleFor(c => c.Title, f => f.Lorem.Sentence())
    .RuleFor(c => c.UserId, f => f.Random.Long(1, 1000))
    .RuleFor(c => c.MessageCount, f => f.Random.Int(0, 100))
    .RuleFor(c => c.CreatedAt, f => f.Date.Past());

var testData = faker.Generate(10);
```

### Testcontainers
**Version**: 4.0.0
**Purpose**: Integration testing with containers
**Packages**:
```xml
<PackageReference Include="Testcontainers" Version="4.0.0" />
<PackageReference Include="Testcontainers.PostgreSql" Version="4.0.0" />
<PackageReference Include="Testcontainers.RabbitMq" Version="4.0.0" />
<PackageReference Include="Testcontainers.EventStoreDb" Version="4.0.0" />
```**Usage**:
```csharp
public class IntegrationTestBase : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("axon_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();
    
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        
        // Apply migrations
        var connectionString = _postgres.GetConnectionString();
        // Setup test database
    }
    
    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}
```

### Respawn
**Version**: 6.2.1
**Purpose**: Database cleanup for integration tests
**Package**:
```xml
<PackageReference Include="Respawn" Version="6.2.1" />
```**Usage**:
```csharp
private static Respawner _respawner;

// Initialize
_respawner = await Respawner.CreateAsync(connectionString, new RespawnerOptions
{
    TablesToIgnore = new[] { "__EFMigrationsHistory" },
    SchemasToInclude = new[] { "chat", "identity" }
});

// Reset database between tests
await _respawner.ResetAsync(connectionString);
```

### ASP.NET Core Test Host
**Version**: 9.0.0
**Purpose**: Integration testing for web APIs
**Packages**:
```xml
<PackageReference Include="Microsoft.AspNetCore.TestHost" Version="9.0.0" />
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="9.0.0" />
<PackageReference Include="WebMotions.Fake.Authentication.JwtBearer" Version="8.0.1" />
```## Observability

### OpenTelemetry
**Version**: 1.11.1
**Purpose**: Distributed tracing and metrics
**Packages**:
```xml
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.11.1" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.11.1"/>
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.11.0"/>
<PackageReference Include="OpenTelemetry.Instrumentation.GrpcNetClient" Version="1.11.0-beta.1"/>
<PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.11.0"/>
<PackageReference Include="OpenTelemetry.Instrumentation.Process" Version="1.11.0-beta.1"/>
<PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.11.0"/>
<PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" Version="1.11.0-beta.1"/>
<PackageReference Include="OpenTelemetry.Exporter.Console" Version="1.11.1"/>
<PackageReference Include="OpenTelemetry.Exporter.Zipkin" Version="1.11.1"/>
```**Configuration**:
```csharp
services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("axon-backend")
        .AddAttributes(new Dictionary<string, object>
        {
            ["environment"] = environment,
            ["version"] = version
        }))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddSource("MassTransit")
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddProcessInstrumentation()
        .AddPrometheusExporter());
```

### Grafana OpenTelemetry
**Version**: 1.2.0
**Purpose**: Grafana integration
**Package**:
```xml
<PackageReference Include="Grafana.OpenTelemetry" Version="1.2.0"/>
```### Health Checks
**Version**: 9.0.0
**Purpose**: Application health monitoring
**Packages**:
```xml
<PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks" Version="9.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.UI" Version="9.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.UI.Client" Version="9.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.UI.InMemory.Storage" Version="9.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.Npgsql" Version="9.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.Rabbitmq" Version="9.0.0" />
<PackageReference Include="AspNetCore.HealthChecks.EventStore" Version="9.0.0" />
```**Configuration**:
```csharp
services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgres")
    .AddRabbitMQ(rabbitConnection, name: "rabbitmq")
    .AddRedis(redisConnection, name: "redis")
    .AddCheck<AiServiceHealthCheck>("ai-service")
    .AddCheck<DatabaseMigrationHealthCheck>("db-migrations");

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecksUI(options => options.UIPath = "/health-ui");
```

## Resilience & Fault Tolerance

### Polly
**Version**: 8.5.0
**Purpose**: Resilience and transient-fault handling
**Packages**:
```xml
<PackageReference Include="Polly" Version="8.5.0" />
<PackageReference Include="Microsoft.Extensions.Http.Polly" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="9.0.0" />
```**Usage Patterns**:
```csharp
// HTTP client resilience
services.AddHttpClient<IAiClient, OpenAiClient>()
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy())
    .AddPolicyHandler(GetTimeoutPolicy());

private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(
            3,
            retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                var logger = context.Values["logger"] as ILogger;
                logger?.LogWarning("Retry {Count} after {Delay}ms", 
                    retryCount, timespan.TotalMilliseconds);
            });
}

private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
}
```## Development Tools

### Scrutor
**Version**: 5.0.2
**Purpose**: Assembly scanning and decoration for DI
**Package**:
```xml
<PackageReference Include="Scrutor" Version="5.0.2" />
```

**Usage**:
```csharp
// Auto-register services
services.Scan(scan => scan
    .FromAssemblyOf<Program>()
    .AddClasses(classes => classes.AssignableTo<IRepository>())
        .AsImplementedInterfaces()
        .WithScopedLifetime()
    .AddClasses(classes => classes.AssignableTo<IService>())
        .AsImplementedInterfaces()
        .WithTransientLifetime());

// Decorator pattern
services.Decorate<IConversationRepository, CachedConversationRepository>();
```### Humanizer
**Version**: 2.14.1
**Purpose**: String manipulation and formatting
**Package**:
```xml
<PackageReference Include="Humanizer.Core" Version="2.14.1" />
```

**Usage**:
```csharp
// String formatting
"ConversationCreated".Humanize(); // "Conversation created"
"conversation_id".Pascalize(); // "ConversationId"
DateTime.UtcNow.Humanize(); // "2 hours ago"
123456.ToWords(); // "one hundred and twenty-three thousand four hundred and fifty-six"
```

### IdGen
**Version**: 3.0.7
**Purpose**: Distributed ID generation (Snowflake IDs)
**Package**:
```xml
<PackageReference Include="IdGen" Version="3.0.7" />
```**Usage**:
```csharp
// Configure ID generator
var generator = new IdGenerator(0); // Machine ID = 0

// Generate IDs
var id = generator.CreateId();
```

### Figgle
**Version**: 0.5.1
**Purpose**: ASCII art generation for console output
**Package**:
```xml
<PackageReference Include="Figgle" Version="0.5.1" />
```

**Usage**:
```csharp
// Startup banner
Console.WriteLine(FiggleFonts.Standard.Render("Axon Backend"));
```

### YARP (Yet Another Reverse Proxy)
**Version**: 2.2.0
**Purpose**: Reverse proxy functionality
**Package**:
```xml
<PackageReference Include="Yarp.ReverseProxy" Version="2.2.0" />
```## Dependency Management

### Directory.Build.props
Common properties for all projects:
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>13</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
  </PropertyGroup>
</Project>
```

### Central Package Management
Directory.Packages.props for version management:
```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  
  <ItemGroup>
    <!-- Define all package versions centrally -->
    <PackageVersion Include="MediatR" Version="13.0.0" />
    <PackageVersion Include="FluentValidation" Version="11.11.0" />
    <!-- ... other packages ... -->
  </ItemGroup>
</Project>
```## Version Strategy

### Versioning Policy

1. **Framework & Runtime**
   - .NET: Use latest LTS version
   - Update within 3 months of new LTS release

2. **Major Dependencies**
   - Entity Framework Core: Match .NET version
   - ASP.NET Core packages: Match .NET version
   - MediatR: Latest stable
   - FluentValidation: Latest stable

3. **Security Updates**
   - Apply immediately for critical vulnerabilities
   - Monthly review for non-critical updates

4. **Testing Dependencies**
   - Can use preview/beta versions
   - Update frequently for better features

### Update Process

```bash
# Check for outdated packages
dotnet list package --outdated

# Update specific package
dotnet add package PackageName --version X.Y.Z

# Update all packages in solution
dotnet restore --force-evaluate
```### Dependency Audit

Regular audit checklist:

- [ ] Check for security vulnerabilities
- [ ] Review deprecated packages
- [ ] Identify unused dependencies
- [ ] Verify license compatibility
- [ ] Assess package maintenance status

```bash
# Security audit
dotnet list package --vulnerable

# License check
dotnet-license-checker

# Remove unused packages
dotnet remove package UnusedPackage
```

## Migration Notes

### From .NET 8 to .NET 10
- Update target framework in all projects
- Update all Microsoft packages to 10.0.0
- Review breaking changes in migration guide
- Update Docker base images
- Test all integration points

### Deprecated Packages
- **Automapper**: Replaced with Mapster for performance
- **Serilog**: Using built-in logging with OpenTelemetry
- **Dapper**: Fully using EF Core for consistency

---

*Last Updated: August 2025*
*Version: 1.0.0*