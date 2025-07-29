# ModelContextProtocol.AspNetCore Implementation Guide

## Overview

**ModelContextProtocol.AspNetCore** is the ASP.NET Core extension library for the C# Model Context Protocol (MCP) SDK, specifically designed for HTTP-based MCP servers. This package enables .NET applications to host MCP servers within ASP.NET Core web applications, providing standardized context to Large Language Models (LLMs) through a secure, well-defined protocol.

### Package Information
- **Current Version**: 0.3.0-preview.2 (0.3.0-preview.3 may be available)
- **Target Framework**: .NET 8.0+ (compatible with .NET 10)
- **Status**: Preview (breaking changes possible without notice)
- **Maintained by**: Model Context Protocol team in collaboration with Microsoft

### Installation
```bash
dotnet add package ModelContextProtocol.AspNetCore --prerelease
```

## Core Concepts

### What is Model Context Protocol (MCP)?
MCP is an open protocol that standardizes how applications provide context to Large Language Models (LLMs). It enables secure integration between LLMs and various data sources and tools through a well-defined interface.

### Package Ecosystem Comparison
1. **ModelContextProtocol** - Main package with hosting and dependency injection extensions (recommended for most projects)
2. **ModelContextProtocol.AspNetCore** - HTTP-based MCP servers with ASP.NET Core integration
3. **ModelContextProtocol.Core** - Low-level client/server APIs with minimal dependencies

## API Reference

### Core Extension Methods

#### AddMcpServer()
Registers MCP server services in the dependency injection container.

```csharp
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();
```

**Return Type**: `IMcpServerBuilder`
**Purpose**: Entry point for MCP server configuration

#### WithHttpTransport()
Configures the MCP server to use HTTP transport with Server-Sent Events (SSE).

```csharp
builder.Services.AddMcpServer()
    .WithHttpTransport() // Enables SSE-based HTTP transport
```

**Transport Details**:
- Adds `/sse` endpoint for server-sent events
- Adds `/messages` endpoint for HTTP communication
- Supports streamable HTTP transport
- Requires CORS configuration for browser compatibility

#### WithToolsFromAssembly()
Automatically discovers and registers MCP tools from the current assembly.

```csharp
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly(); // Auto-discovers [McpServerToolType] classes
```

**Alternative**: Use `.WithTools<ToolType>()` for AOT compatibility or manual registration.

#### MapMcp()
Extension method to configure MCP server endpoints in the request pipeline.

```csharp
app.MapMcp("/api/mcp"); // Optional route prefix
// or
app.MapMcp(); // Maps to root path
```

**Endpoints Created**:
- `/sse` - Server-Sent Events endpoint
- `/messages` - HTTP messages endpoint

## Server Hosting Patterns

### Basic Server Configuration

```csharp
using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = WebApplication.CreateBuilder(args);

// Register MCP server with HTTP transport
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// Configure CORS for browser compatibility
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure MCP endpoints
app.MapMcp("/api/mcp");
app.UseCors();

app.Run("http://localhost:3001");
```

### Advanced Server Configuration with Custom Route

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<CustomMcpTool>()
    .WithTools<AnotherMcpTool>();

// Configure CORS with specific origins for production
builder.Services.AddCors(options =>
{
    options.AddPolicy("Production", policy =>
    {
        policy.WithOrigins("https://yourdomain.com")
              .WithMethods("GET", "POST")
              .WithHeaders("Content-Type", "Authorization")
              .AllowCredentials();
    });
});

var app = builder.Build();

app.MapMcp("/mcp");
app.UseCors("Production");
app.Run();
```

## Tool Development

### Tool Definition Attributes

#### [McpServerToolType]
Marks a class as containing MCP server tools.

```csharp
[McpServerToolType]
public class WeatherTools
{
    // Tool methods defined here
}
```

#### [McpServerTool]
Marks methods as callable MCP tools.

```csharp
[McpServerTool, Description("Gets current weather for a location")]
public async Task<string> GetWeatherAsync(
    [Description("The city name")] string city,
    [Description("Country code (optional)")] string? country = null)
{
    // Implementation
    return JsonSerializer.Serialize(weatherData);
}
```

#### [Description]
Provides human-readable descriptions for methods and parameters.

### Complete Tool Example

```csharp
[McpServerToolType]
public class TodosMcpTool
{
    private readonly ITodoService _todoService;

    public TodosMcpTool(ITodoService todoService)
    {
        _todoService = todoService;
    }

    [McpServerTool, Description("Creates a new todo item")]
    public async Task<string> CreateTodoAsync(
        [Description("Todo description")] string description,
        [Description("Due date")] DateTime? dueDate = null,
        [Description("Priority level")] string priority = "medium")
    {
        var todo = await _todoService.CreateAsync(description, dueDate, priority);
        return JsonSerializer.Serialize(todo);
    }

    [McpServerTool, Description("Gets all todo items")]
    public async Task<string> GetTodosAsync()
    {
        var todos = await _todoService.GetAllAsync();
        return JsonSerializer.Serialize(todos);
    }

    [McpServerTool, Description("Marks a todo as completed")]
    public async Task<string> CompleteTodoAsync(
        [Description("Todo ID to complete")] int todoId)
    {
        await _todoService.CompleteAsync(todoId);
        return JsonSerializer.Serialize(new { success = true, message = "Todo completed" });
    }
}
```

## Middleware Integration

### Request Pipeline Order

The correct middleware order for MCP servers:

```csharp
var app = builder.Build();

// 1. Exception handling
app.UseExceptionHandler();

// 2. HTTPS redirection
app.UseHttpsRedirection();

// 3. Routing
app.UseRouting();

// 4. CORS (after routing, before authorization)
app.UseCors();

// 5. Authentication
app.UseAuthentication();

// 6. Authorization  
app.UseAuthorization();

// 7. MCP endpoints
app.MapMcp("/api/mcp");

// 8. Other endpoints
app.MapControllers();

app.Run();
```

### Custom Middleware Integration

```csharp
// Custom logging middleware for MCP requests
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/mcp"))
    {
        Console.WriteLine($"MCP Request: {context.Request.Method} {context.Request.Path}");
        
        // Add custom headers
        context.Response.Headers.Add("X-MCP-Server", "Axon-Backend");
    }
    
    await next();
});

app.MapMcp("/api/mcp");
```

## Dependency Injection Integration

### Service Registration Patterns

```csharp
// Register MCP server
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// Register dependencies for MCP tools
builder.Services.AddScoped<IWeatherService, WeatherService>();
builder.Services.AddScoped<ITodoService, TodoService>();
builder.Services.AddHttpClient<IExternalApiClient, ExternalApiClient>();

// Register MCP tools explicitly (alternative to WithToolsFromAssembly)
builder.Services.AddScoped<WeatherMcpTool>();
builder.Services.AddScoped<TodoMcpTool>();
```

### Tool Dependency Injection

```csharp
[McpServerToolType]
public class DatabaseMcpTool
{
    private readonly IDbContext _dbContext;
    private readonly ILogger<DatabaseMcpTool> _logger;
    private readonly IConfiguration _configuration;

    public DatabaseMcpTool(
        IDbContext dbContext,
        ILogger<DatabaseMcpTool> logger,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _logger = logger;
        _configuration = configuration;
    }

    [McpServerTool, Description("Query database records")]
    public async Task<string> QueryRecordsAsync(
        [Description("SQL query to execute")] string query)
    {
        _logger.LogInformation("Executing query: {Query}", query);
        
        var results = await _dbContext.ExecuteQueryAsync(query);
        return JsonSerializer.Serialize(results);
    }
}
```

## Authentication and Authorization

### Basic Authentication Integration

```csharp
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("McpAccess", policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim("mcp_access", "true"));
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Apply authorization to MCP endpoints
app.MapMcp("/api/mcp").RequireAuthorization("McpAccess");
```

### Tool-Level Authorization

```csharp
[McpServerToolType]
public class AdminMcpTool
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AdminMcpTool(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    [McpServerTool, Description("Administrative operation")]
    public async Task<string> AdminOperationAsync(
        [Description("Operation to perform")] string operation)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        
        if (!user?.IsInRole("Admin") == true)
        {
            throw new UnauthorizedAccessException("Admin role required");
        }

        // Perform admin operation
        return JsonSerializer.Serialize(new { success = true });
    }
}
```

## CORS Configuration

### Development CORS Setup

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader() 
              .AllowAnyMethod();
    });
});

app.UseCors("Development");
```

### Production CORS Setup

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Production", policy =>
    {
        policy.WithOrigins(
                "https://yourdomain.com",
                "https://app.yourdomain.com"
              )
              .WithMethods("GET", "POST", "OPTIONS")
              .WithHeaders("Content-Type", "Authorization", "X-MCP-Client")
              .AllowCredentials()
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});

app.UseCors("Production");
```

### CORS with Authentication

```csharp
// Cannot use AllowAnyOrigin() with AllowCredentials()
builder.Services.AddCors(options =>
{
    options.AddPolicy("AuthenticatedCors", policy =>
    {
        policy.SetIsOriginAllowed(origin => 
                IsAllowedOrigin(origin)) // Custom validation
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

static bool IsAllowedOrigin(string origin)
{
    var allowedDomains = new[] { "yourdomain.com", "localhost" };
    return allowedDomains.Any(domain => 
        origin.Contains(domain, StringComparison.OrdinalIgnoreCase));
}
```

## Transport Mechanisms

### Current HTTP+SSE Transport

The current implementation uses HTTP with Server-Sent Events:

```csharp
builder.Services.AddMcpServer()
    .WithHttpTransport(); // Enables HTTP+SSE transport

// Creates endpoints:
// GET  /sse      - Server-Sent Events stream
// POST /messages - HTTP message endpoint
```

**Transport Characteristics**:
- **Pros**: Web-standard HTTP, firewall-friendly, CORS support
- **Cons**: Complex state management, SSE limitations, not truly bidirectional

### WebSocket Support Status

**Current Status**: WebSocket transport is not directly supported in the current preview version.

**Future Consideration**: There are proposals to replace HTTP+SSE with WebSocket transport for:
- Full-duplex communication
- Simplified state management
- Better performance for real-time scenarios
- Standards-compliant bidirectional messaging

**Workaround**: For WebSocket-like functionality, implement custom SignalR hubs:

```csharp
// Custom SignalR hub for MCP-like functionality
public class McpHub : Hub
{
    [HubMethodName("ProcessTool")]
    public async Task<string> ProcessToolAsync(string toolName, string parameters)
    {
        // Custom MCP tool processing
        return await ProcessMcpToolAsync(toolName, parameters);
    }
}

// Registration
builder.Services.AddSignalR();
app.MapHub<McpHub>("/mcpHub");
```

## .NET 10 Compatibility

### Framework Compatibility
- **Target Framework**: .NET 8.0+ 
- **.NET 10 Status**: ✅ Compatible (forward compatibility)
- **ASP.NET Core 10**: ✅ Supported through backward compatibility

### .NET 10 Specific Features

```csharp
// Leverage .NET 10 features in MCP tools
[McpServerToolType]
public class ModernMcpTool
{
    // Primary constructors (C# 12)
    public ModernMcpTool(ILogger<ModernMcpTool> logger, IServiceProvider services);

    [McpServerTool, Description("Uses .NET 10 features")]
    public async Task<string> ProcessWithModernFeaturesAsync(
        [Description("Input data")] string data)
    {
        // Collection expressions
        List<string> items = [data, "processed", "data"];
        
        // Pattern matching enhancements
        var result = items switch
        {
            [var first, ..] when first.Length > 0 => ProcessItems(items),
            [] => "Empty collection",
            _ => "Unknown pattern"
        };

        return JsonSerializer.Serialize(result);
    }
}
```

### Global Configuration for .NET 10

```xml
<!-- Directory.Build.props -->
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

## Axon Backend Integration Patterns

### Architecture Alignment

Based on Axon Backend's Clean Architecture + DDD + CQRS patterns:

```
src/
├── Api/                           # MCP server hosting
│   ├── Endpoints/Mcp/            # MCP endpoint configuration
│   └── Program.cs                # MCP server registration
├── Modules/
│   └── Chat/
│       ├── Application/
│       │   └── McpTools/         # MCP tool implementations
│       ├── Domain/               # Domain logic (no MCP dependencies)
│       └── Infrastructure/
│           └── Mcp/              # MCP-specific adapters
└── Shared/
    └── Mcp/                      # MCP abstractions and common types
```

### Dependency Rule Compliance

**✅ ALLOWED integrations:**
```csharp
// Api layer - MCP server hosting
// Api → Modules.*.Application (MCP tools)
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<Modules.Chat.Application.McpTools.ChatMcpTool>();

// Application layer - MCP tool implementations  
// Modules.Chat.Application → Modules.Chat.Domain + Shared.*
[McpServerToolType]
public class ChatMcpTool 
{
    private readonly IMediator _mediator; // From Application layer
    // Uses domain services, not direct domain access
}
```

**❌ FORBIDDEN integrations:**
```csharp
// NEVER: Api → Domain
// NEVER: MCP tools directly accessing domain entities
// NEVER: Cross-module MCP tool dependencies
```

### MCP Server Registration in Program.cs

```csharp
// src/Api/Program.cs
using Modules.Chat.Infrastructure;
using Modules.Portfolio.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Register modules (existing pattern)
builder.Services.AddChatModule(builder.Configuration);
builder.Services.AddPortfolioModule(builder.Configuration);

// Register MCP server
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<Modules.Chat.Application.McpTools.ChatMcpTool>()
    .WithTools<Modules.Portfolio.Application.McpTools.PortfolioMcpTool>();

var app = builder.Build();

// MCP endpoint with feature-based routing
app.MapMcp("/api/mcp");
```

### Module-Specific MCP Tools

```csharp
// src/Modules/Chat/Application/McpTools/ChatMcpTool.cs
namespace Modules.Chat.Application.McpTools;

[McpServerToolType]
public class ChatMcpTool
{
    private readonly IMediator _mediator;
    private readonly ILogger<ChatMcpTool> _logger;

    public ChatMcpTool(IMediator mediator, ILogger<ChatMcpTool> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [McpServerTool, Description("Process a chat message")]
    public async Task<string> ProcessMessageAsync(
        [Description("Message content")] string message,
        [Description("Conversation ID")] Guid conversationId)
    {
        var command = new ProcessMessageCommand(message, conversationId);
        var result = await _mediator.Send(command);
        
        return result.IsSuccess 
            ? JsonSerializer.Serialize(result.Value)
            : JsonSerializer.Serialize(new { error = result.Error.Message });
    }
}

// src/Modules/Chat/Application/Commands/ProcessMessageCommand.cs
public record ProcessMessageCommand(
    string Message, 
    Guid ConversationId
) : IRequest<Result<MessageDto>>;
```

### Shared MCP Abstractions

```csharp
// src/Shared/Mcp/IMcpTool.cs
namespace Shared.Mcp;

public interface IMcpTool
{
    string ToolName { get; }
    string Description { get; }
}

// src/Shared/Mcp/McpResult.cs
public static class McpResult
{
    public static string Success<T>(T data) => 
        JsonSerializer.Serialize(new { success = true, data });
        
    public static string Error(string message) => 
        JsonSerializer.Serialize(new { success = false, error = message });
}
```

### Integration with MediatR

```csharp
[McpServerToolType]
public class ChatMcpTool
{
    private readonly IMediator _mediator;

    public ChatMcpTool(IMediator mediator) => _mediator = mediator;

    [McpServerTool, Description("Send a chat message")]
    public async Task<string> SendMessageAsync(
        [Description("Message text")] string message,
        [Description("User ID")] Guid userId)
    {
        var command = new SendMessageCommand(message, userId);
        var result = await _mediator.Send(command);
        
        return result.Match(
            success: data => McpResult.Success(data),
            error: err => McpResult.Error(err.Message)
        );
    }

    [McpServerTool, Description("Get conversation history")]
    public async Task<string> GetConversationHistoryAsync(
        [Description("Conversation ID")] Guid conversationId,
        [Description("Number of messages")] int limit = 20)
    {
        var query = new GetConversationHistoryQuery(conversationId, limit);
        var result = await _mediator.Send(query);
        
        return result.Match(
            success: data => McpResult.Success(data),
            error: err => McpResult.Error(err.Message)
        );
    }
}
```

## Error Handling and Logging

### Global Error Handling

```csharp
// Global exception handler for MCP tools
[McpServerToolType]
public class SafeMcpTool
{
    private readonly ILogger<SafeMcpTool> _logger;

    public SafeMcpTool(ILogger<SafeMcpTool> logger) => _logger = logger;

    [McpServerTool, Description("Safe operation with error handling")]
    public async Task<string> SafeOperationAsync(
        [Description("Operation parameters")] string parameters)
    {
        try
        {
            var result = await PerformOperationAsync(parameters);
            return McpResult.Success(result);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Validation error in MCP tool: {Error}", ex.Message);
            return McpResult.Error($"Validation error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in MCP tool");
            return McpResult.Error("An unexpected error occurred");
        }
    }
}
```

### Structured Logging

```csharp
[McpServerToolType]  
public class LoggingMcpTool
{
    private readonly ILogger<LoggingMcpTool> _logger;

    public LoggingMcpTool(ILogger<LoggingMcpTool> logger) => _logger = logger;

    [McpServerTool, Description("Operation with structured logging")]
    public async Task<string> TrackedOperationAsync(
        [Description("Operation ID")] string operationId,
        [Description("User ID")] Guid userId)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["OperationId"] = operationId,
            ["UserId"] = userId,
            ["ToolName"] = nameof(TrackedOperationAsync)
        });

        _logger.LogInformation("Starting MCP tool operation");

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await PerformTrackedOperationAsync(operationId, userId);
            
            _logger.LogInformation("MCP tool operation completed in {Duration}ms", 
                stopwatch.ElapsedMilliseconds);
                
            return McpResult.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP tool operation failed after {Duration}ms", 
                stopwatch.ElapsedMilliseconds);
            return McpResult.Error("Operation failed");
        }
    }
}
```

## Performance Considerations

### Async/Await Best Practices

```csharp
[McpServerToolType]
public class PerformantMcpTool
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;

    public PerformantMcpTool(HttpClient httpClient, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _cache = cache;
    }

    [McpServerTool, Description("Cached HTTP operation")]
    public async Task<string> CachedHttpOperationAsync(
        [Description("API endpoint")] string endpoint)
    {
        var cacheKey = $"mcp_http_{endpoint}";
        
        if (_cache.TryGetValue(cacheKey, out string? cachedResult))
        {
            return cachedResult;
        }

        using var response = await _httpClient.GetAsync(endpoint);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        var result = McpResult.Success(content);

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
        return result;
    }

    [McpServerTool, Description("Parallel processing operation")]
    public async Task<string> ParallelOperationAsync(
        [Description("List of items to process")] string[] items)
    {
        var tasks = items.Select(ProcessItemAsync).ToArray();
        var results = await Task.WhenAll(tasks);
        
        return McpResult.Success(results);
    }

    private async Task<string> ProcessItemAsync(string item)
    {
        // Simulate async processing
        await Task.Delay(100);
        return $"Processed: {item}";
    }
}
```

### Memory Management

```csharp
[McpServerToolType]
public class EfficientMcpTool : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(10); // Limit concurrent operations
    private bool _disposed;

    [McpServerTool, Description("Resource-efficient operation")]
    public async Task<string> EfficientOperationAsync(
        [Description("Large data input")] string largeData)
    {
        await _semaphore.WaitAsync();
        try
        {
            // Process data in chunks to avoid memory pressure
            const int chunkSize = 1024;
            var results = new List<string>();

            for (int i = 0; i < largeData.Length; i += chunkSize)
            {
                var chunk = largeData.Substring(i, Math.Min(chunkSize, largeData.Length - i));
                var processedChunk = await ProcessChunkAsync(chunk);
                results.Add(processedChunk);
            }

            return McpResult.Success(results);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<string> ProcessChunkAsync(string chunk)
    {
        await Task.Delay(10); // Simulate processing
        return chunk.ToUpperInvariant();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _semaphore?.Dispose();
            _disposed = true;
        }
    }
}
```

## Testing Strategies

### Unit Testing MCP Tools

```csharp
// Test class for MCP tools
public class ChatMcpToolTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<ChatMcpTool>> _loggerMock;
    private readonly ChatMcpTool _tool;

    public ChatMcpToolTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<ChatMcpTool>>();
        _tool = new ChatMcpTool(_mediatorMock.Object, _loggerMock.Object);
    }

    [Test]
    public async Task ProcessMessageAsync_ValidInput_ReturnsSuccess()
    {
        // Arrange
        var message = "Hello, world!";
        var conversationId = Guid.NewGuid();
        var expectedResult = Result.Success(new MessageDto { Id = Guid.NewGuid(), Content = message });
        
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<ProcessMessageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _tool.ProcessMessageAsync(message, conversationId);

        // Assert
        result.Should().Contain("success");
        result.Should().Contain(message);
        
        _mediatorMock.Verify(m => m.Send(
            It.Is<ProcessMessageCommand>(cmd => 
                cmd.Message == message && cmd.ConversationId == conversationId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ProcessMessageAsync_MediatorFailure_ReturnsError()
    {
        // Arrange
        var message = "Hello, world!";
        var conversationId = Guid.NewGuid();
        var error = Error.Validation("Invalid message");
        var failureResult = Result.Failure<MessageDto>(error);
        
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<ProcessMessageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(failureResult);

        // Act
        var result = await _tool.ProcessMessageAsync(message, conversationId);

        // Assert
        result.Should().Contain("error");
        result.Should().Contain(error.Message);
    }
}
```

### Integration Testing

```csharp
public class McpServerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public McpServerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Test]
    public async Task McpEndpoints_ShouldBeAvailable()
    {
        // Test SSE endpoint
        var sseResponse = await _client.GetAsync("/api/mcp/sse");
        sseResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        sseResponse.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream");

        // Test messages endpoint availability
        var messagesResponse = await _client.PostAsync("/api/mcp/messages", 
            new StringContent("{}", Encoding.UTF8, "application/json"));
        messagesResponse.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
    }
}
```

## Security Best Practices

### Input Validation

```csharp
[McpServerToolType]
public class SecureMcpTool
{
    [McpServerTool, Description("Secure operation with input validation")]
    public async Task<string> SecureOperationAsync(
        [Description("User input to validate")] string userInput,
        [Description("Numeric parameter")] int numericParam)
    {
        // Input validation
        if (string.IsNullOrWhiteSpace(userInput))
        {
            return McpResult.Error("Input cannot be empty");
        }

        if (userInput.Length > 1000)
        {
            return McpResult.Error("Input too long");
        }

        if (numericParam < 0 || numericParam > 100)
        {
            return McpResult.Error("Numeric parameter out of range");
        }

        // Sanitize input
        var sanitizedInput = Regex.Replace(userInput, @"[<>""']", "");
        
        var result = await ProcessSecurelyAsync(sanitizedInput, numericParam);
        return McpResult.Success(result);
    }

    private async Task<object> ProcessSecurelyAsync(string input, int param)
    {
        // Secure processing logic
        await Task.Delay(100);
        return new { processed = input, parameter = param };
    }
}
```

### Rate Limiting

```csharp
// Add rate limiting service
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("McpPolicy", policyOptions =>
    {
        policyOptions.PermitLimit = 100;
        policyOptions.Window = TimeSpan.FromMinutes(1);
        policyOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        policyOptions.QueueLimit = 10;
    });
});

var app = builder.Build();

app.UseRateLimiter();

// Apply rate limiting to MCP endpoints
app.MapMcp("/api/mcp").RequireRateLimiting("McpPolicy");
```

## Production Deployment

### Health Checks

```csharp
builder.Services.AddHealthChecks()
    .AddCheck("mcp-server", () =>
    {
        // Add MCP server health check logic
        return HealthCheckResult.Healthy("MCP server is running");
    });

app.MapHealthChecks("/health");
```

### Configuration Management

```csharp
// appsettings.json
{
  "McpServer": {
    "EnableHttpTransport": true,
    "MaxConcurrentConnections": 100,
    "RequestTimeout": "00:00:30",
    "EnableDetailedErrors": false
  },
  "Cors": {
    "AllowedOrigins": ["https://yourdomain.com"],
    "AllowCredentials": true
  }
}

// Configuration binding
builder.Services.Configure<McpServerOptions>(
    builder.Configuration.GetSection("McpServer"));
builder.Services.Configure<CorsOptions>(
    builder.Configuration.GetSection("Cors"));
```

### Docker Configuration

```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["YourApp.csproj", "./"]
RUN dotnet restore "YourApp.csproj"
COPY . .
RUN dotnet build "YourApp.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "YourApp.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "YourApp.dll"]
```

## Summary

ModelContextProtocol.AspNetCore provides a robust foundation for hosting MCP servers within ASP.NET Core applications. Key takeaways:

### When to Use
- ✅ Need HTTP-based MCP server hosting
- ✅ Integration with existing ASP.NET Core applications  
- ✅ Web-standard transport requirements
- ✅ CORS and authentication needs

### When to Consider Alternatives
- ❌ Simple stdio-based MCP servers (use `ModelContextProtocol`)
- ❌ Minimal dependencies required (use `ModelContextProtocol.Core`)
- ❌ Real-time bidirectional communication critical (WebSocket alternatives)

### Axon Backend Fit
The package aligns well with Axon Backend's architecture:
- Respects Clean Architecture boundaries
- Integrates with MediatR/CQRS patterns
- Follows dependency injection principles
- Supports modular tool organization

### Next Steps
1. Install preview package and experiment with basic setup
2. Implement module-specific MCP tools following Axon patterns
3. Configure authentication and CORS for your use case
4. Set up comprehensive testing strategy
5. Plan production deployment with health checks and monitoring

The preview status requires careful consideration for production use, but the package provides a solid foundation for MCP server integration in modern .NET applications.