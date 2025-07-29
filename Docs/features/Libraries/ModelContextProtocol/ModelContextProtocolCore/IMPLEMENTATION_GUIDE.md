# ModelContextProtocol.Core v0.3.0-preview.3 Implementation Guide

## Overview

ModelContextProtocol.Core is a lightweight, minimal-dependency package designed for developers who need only client or low-level server APIs for the Model Context Protocol (MCP) without the full hosting and dependency injection infrastructure provided by the main ModelContextProtocol package.

**Package Purpose**: Core package provides foundational client and server abstractions for MCP integration with minimal dependencies and overhead.

**Status**: Preview release (0.3.0-preview.3) - breaking changes may be introduced without prior notice.

## Package Differentiation

### ModelContextProtocol.Core vs Main Package

| Aspect | ModelContextProtocol.Core | ModelContextProtocol (Main) |
|--------|---------------------------|------------------------------|
| **Target Audience** | Developers needing minimal dependencies | Most projects requiring full features |
| **Dependencies** | Minimal dependency footprint | Includes hosting + DI extensions |
| **Features** | Core client/server APIs only | Full hosting, DI, AI integration |
| **Performance** | Smaller size, faster startup | More features, higher overhead |
| **Use Cases** | Basic client functionality, low-level server APIs | Complete server infrastructure, LLM integration |

## .NET 10 Compatibility

### Framework Targeting

**Current Support**: The package currently targets .NET 8.0 and .NET Standard 2.0, making it compatible with .NET 8.0+ frameworks.

**NET 10 Status**: While .NET 10's `net10.0` TFM is under active development, specific ModelContextProtocol.Core compatibility with .NET 10 requires verification of:
- Package's target framework updates
- Compatibility with .NET 10 preview builds
- Infrastructure completion for `net10.0` TFM

**Project Integration**: For Axon Backend (targeting net10.0), the package should work via .NET Standard 2.0 compatibility until explicit net10.0 support is available.

```csharp
// Project file configuration for .NET 10
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="ModelContextProtocol.Core" Version="0.3.0-preview.3" />
  </ItemGroup>
</Project>
```

## Core Abstractions and Interfaces

### Primary Interfaces

#### IMcpClient
Core client interface for MCP protocol interactions.

```csharp
public interface IMcpClient
{
    // Tool enumeration and invocation
    Task<IList<McpClientTool>> ListToolsAsync(CancellationToken cancellationToken = default);
    Task<object> CallToolAsync(string toolName, object arguments, CancellationToken cancellationToken = default);
    
    // Resource management
    Task<IList<Resource>> ListResourcesAsync(CancellationToken cancellationToken = default);
    Task<ResourceContent> ReadResourceAsync(string uri, CancellationToken cancellationToken = default);
    
    // Protocol lifecycle
    Task InitializeAsync(CancellationToken cancellationToken = default);
    ValueTask DisposeAsync();
}
```

#### IMcpEndpoint
Protocol endpoint abstraction for message handling.

```csharp
public interface IMcpEndpoint
{
    Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default);
    ChannelReader<JsonRpcMessage> MessageReader { get; }
}
```

### Core Classes

#### McpClientFactory
Factory for creating MCP client instances.

```csharp
public static class McpClientFactory
{
    public static async Task<IMcpClient> CreateAsync(
        IClientTransport transport, 
        CancellationToken cancellationToken = default)
    {
        // Creates and initializes MCP client
    }
}
```

#### McpServerTool
Tool registration abstraction for servers.

```csharp
public static class McpServerTool
{
    public static McpServerTool Create<T>(
        Func<T, object> handler,
        object metadata)
    {
        // Creates server tool with typed handler
    }
}
```

## Transport Abstractions

### Client Transports

#### StdioClientTransport
Standard I/O transport for subprocess communication.

```csharp
public class StdioClientTransport : IClientTransport
{
    public StdioClientTransport(StdioClientTransportOptions options)
    {
        // Initialize with command, arguments, working directory
    }
}

public class StdioClientTransportOptions
{
    public string Name { get; set; }
    public string Command { get; set; }
    public string[] Arguments { get; set; }
    public string WorkingDirectory { get; set; }
}
```

#### SseClientTransport
Server-Sent Events transport for HTTP-based communication.

```csharp
public class SseClientTransport : IClientTransport
{
    public SseClientTransport(SseClientTransportOptions options)
    {
        // Initialize with endpoint URI
    }
}

public class SseClientTransportOptions
{
    public Uri Endpoint { get; set; }
    public TimeSpan Timeout { get; set; }
}
```

### Server Transports

#### StdioServerTransport
Standard I/O transport for server implementations.

```csharp
public class StdioServerTransport : IServerTransport
{
    // Handles stdio-based server communication
    public async Task RunAsync(IMcpServer server, CancellationToken cancellationToken)
    {
        // Server message loop implementation
    }
}
```

## Method Inventory

### Client Methods

```csharp
// McpClientFactory
public static async Task<IMcpClient> CreateAsync(IClientTransport transport, CancellationToken ct = default)

// IMcpClient Core Methods
public async Task<IList<McpClientTool>> ListToolsAsync(CancellationToken ct = default)
public async Task<object> CallToolAsync(string toolName, object arguments, CancellationToken ct = default)
public async Task<IList<Resource>> ListResourcesAsync(CancellationToken ct = default)
public async Task<ResourceContent> ReadResourceAsync(string uri, CancellationToken ct = default)
public async Task InitializeAsync(CancellationToken ct = default)
public async ValueTask DisposeAsync()

// McpClientTool (inherits from AIFunction)
public string Name { get; }
public string Description { get; }
public AIFunctionMetadata Metadata { get; }
public async Task<object> InvokeAsync(object arguments, CancellationToken ct = default)
```

### Server Methods

```csharp
// McpServer
public McpServer(McpServerOptions options)
public async Task RunAsync(IServerTransport transport, CancellationToken ct = default)

// McpServerTool Factory
public static McpServerTool Create<T>(Func<T, object> handler, object metadata)
public static McpServerTool Create(Func<object> handler, ToolMetadata metadata)

// McpServerOptions
public class McpServerOptions
{
    public ServerCapabilities Capabilities { get; set; }
    public IList<McpServerTool> Tools { get; set; }
}
```

## Client Implementation Patterns

### Basic Client Setup

```csharp
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

// Create transport (stdio example)
var clientTransport = new StdioClientTransport(new StdioClientTransportOptions 
{
    Name = "MyMcpClient",
    Command = "npx",
    Arguments = ["-y", "@modelcontextprotocol/server-everything"],
    WorkingDirectory = Environment.CurrentDirectory
});

// Create and initialize client
var client = await McpClientFactory.CreateAsync(clientTransport);

// List available tools
var tools = await client.ListToolsAsync();
Console.WriteLine($"Available tools: {string.Join(", ", tools.Select(t => t.Name))}");

// Invoke a tool
var result = await client.CallToolAsync("echo", new { message = "Hello MCP!" });
Console.WriteLine($"Tool result: {result}");

// Cleanup
await client.DisposeAsync();
```

### HTTP/SSE Client Pattern

```csharp
// Create SSE transport
var sseTransport = new SseClientTransport(new SseClientTransportOptions
{
    Endpoint = new Uri("https://localhost:7289/sse"),
    Timeout = TimeSpan.FromSeconds(30)
});

// Create client with HTTP transport
var httpClient = await McpClientFactory.CreateAsync(sseTransport);

try
{
    // Use client for MCP operations
    var resources = await httpClient.ListResourcesAsync();
    
    foreach (var resource in resources)
    {
        var content = await httpClient.ReadResourceAsync(resource.Uri);
        // Process resource content
    }
}
finally
{
    await httpClient.DisposeAsync();
}
```

### Integration with AI Chat Clients

```csharp
// ModelContextProtocol.Core tools integrate with Microsoft.Extensions.AI
using Microsoft.Extensions.AI;

var client = await McpClientFactory.CreateAsync(transport);
var mcpTools = await client.ListToolsAsync();

// McpClientTool inherits from AIFunction - direct integration
IChatClient chatClient = GetChatClient(); // Your AI chat client
var response = await chatClient.GetResponseAsync(
    "Analyze the data using available tools",
    new ChatOptions
    {
        Tools = mcpTools.Cast<AIFunction>().ToList()
    });
```

## Server Implementation Patterns

### Basic Server Setup

```csharp
using ModelContextProtocol.Server;

// Define server capabilities and tools
var serverOptions = new McpServerOptions();
serverOptions.Capabilities.Tools.ListChanged = true;
serverOptions.Capabilities.Tools.ToolCollection = new List<McpServerTool>
{
    McpServerTool.Create((string message) => $"Echo: {message}", 
        new ToolMetadata 
        { 
            Name = "echo", 
            Description = "Echoes the input message" 
        }),
    
    McpServerTool.Create((int x, int y) => x + y,
        new ToolMetadata
        {
            Name = "add",
            Description = "Adds two integers"
        })
};

// Create and run server
var server = new McpServer(serverOptions);
var stdioTransport = new StdioServerTransport();

await server.RunAsync(stdioTransport, CancellationToken.None);
```

### Advanced Server with Resource Support

```csharp
public class DataAnalysisServer
{
    private readonly McpServerOptions _options;
    
    public DataAnalysisServer()
    {
        _options = new McpServerOptions();
        ConfigureCapabilities();
        ConfigureTools();
    }
    
    private void ConfigureCapabilities()
    {
        _options.Capabilities.Resources.ListChanged = true;
        _options.Capabilities.Tools.ListChanged = true;
    }
    
    private void ConfigureTools()
    {
        _options.Capabilities.Tools.ToolCollection = new List<McpServerTool>
        {
            McpServerTool.Create<AnalysisRequest>(AnalyzeData, new ToolMetadata
            {
                Name = "analyze_data",
                Description = "Analyzes data from specified source"
            }),
            
            McpServerTool.Create<string>(GetDataSummary, new ToolMetadata
            {
                Name = "get_summary", 
                Description = "Gets summary of available data"
            })
        };
    }
    
    private async Task<object> AnalyzeData(AnalysisRequest request)
    {
        // Implementation for data analysis
        return new { Status = "Complete", Results = "Analysis results..." };
    }
    
    private async Task<string> GetDataSummary(string dataSource)
    {
        return $"Summary for {dataSource}: ...";
    }
    
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var server = new McpServer(_options);
        var transport = new StdioServerTransport();
        
        await server.RunAsync(transport, cancellationToken);
    }
}

public record AnalysisRequest(string DataSource, string[] Metrics);
```

## Protocol Serialization

### JSON-RPC Message Handling

MCP uses JSON-RPC 2.0 as its wire format. The Core package handles serialization automatically:

```csharp
// JSON-RPC message structure
public class JsonRpcMessage
{
    public string JsonRpc { get; set; } = "2.0";
    public object Id { get; set; }
    public string Method { get; set; }
    public object Params { get; set; }
    public object Result { get; set; }
    public JsonRpcError Error { get; set; }
}

// Error handling
public class JsonRpcError
{
    public int Code { get; set; }
    public string Message { get; set; }
    public object Data { get; set; }
}
```

### Custom Serialization

```csharp
using System.Text.Json;

// Configure JSON serialization options
var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,
    PropertyNameCaseInsensitive = true
};

// Manual serialization for complex objects
public static string SerializeToolResult(object result)
{
    return JsonSerializer.Serialize(result, jsonOptions);
}

public static T DeserializeToolParams<T>(object parameters)
{
    var json = JsonSerializer.Serialize(parameters);
    return JsonSerializer.Deserialize<T>(json, jsonOptions);
}
```

## Performance Considerations

### Core vs Full Package Trade-offs

**ModelContextProtocol.Core Advantages:**
- **Smaller Package Size**: Minimal dependencies reduce deployment size
- **Faster Startup**: No hosting infrastructure initialization overhead
- **Lower Memory Footprint**: Fewer loaded assemblies and services
- **Reduced Attack Surface**: Fewer dependencies mean fewer potential vulnerabilities
- **Better for Containers**: Smaller images and faster cold starts

**Core Package Limitations:**
- **Manual Configuration**: No built-in dependency injection or hosting
- **More Boilerplate**: Manual transport and client lifecycle management
- **Limited Integration**: No automatic integration with ASP.NET Core or hosting extensions
- **Reduced Convenience**: Missing helper methods and extensions

### Performance Optimization Tips

```csharp
// 1. Reuse client instances
private static readonly AsyncLazy<IMcpClient> _clientInstance = 
    new AsyncLazy<IMcpClient>(async () =>
    {
        var transport = new StdioClientTransport(options);
        return await McpClientFactory.CreateAsync(transport);
    });

// 2. Use connection pooling for HTTP transports
private static readonly HttpClient _httpClient = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(30)
};

// 3. Implement caching for frequently accessed resources
private readonly MemoryCache _resourceCache = new MemoryCache(new MemoryCacheOptions
{
    SizeLimit = 100
});

public async Task<ResourceContent> GetCachedResourceAsync(string uri)
{
    return await _resourceCache.GetOrCreateAsync(uri, async entry =>
    {
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
        return await _client.ReadResourceAsync(uri);
    });
}

// 4. Batch tool calls when possible
public async Task<object[]> BatchToolCallsAsync(IEnumerable<(string name, object args)> calls)
{
    var tasks = calls.Select(call => _client.CallToolAsync(call.name, call.args));
    return await Task.WhenAll(tasks);
}
```

## Axon Backend Integration Patterns

### Clean Architecture Integration

```csharp
// Domain layer - MCP abstractions
namespace Axon.Modules.Chat.Domain.Services
{
    public interface IMcpClientService
    {
        Task<IEnumerable<ToolInfo>> GetAvailableToolsAsync(CancellationToken ct = default);
        Task<TResult> ExecuteToolAsync<TResult>(string toolName, object parameters, CancellationToken ct = default);
    }
}

// Infrastructure layer - MCP implementation
namespace Axon.Modules.Chat.Infrastructure.Services
{
    public sealed class McpClientService : IMcpClientService, IAsyncDisposable
    {
        private readonly IMcpClient _client;
        private readonly ILogger<McpClientService> _logger;
        
        public McpClientService(IMcpClient client, ILogger<McpClientService> logger)
        {
            _client = client;
            _logger = logger;
        }
        
        public async Task<IEnumerable<ToolInfo>> GetAvailableToolsAsync(CancellationToken ct = default)
        {
            var tools = await _client.ListToolsAsync(ct);
            return tools.Select(t => new ToolInfo(t.Name, t.Description));
        }
        
        public async Task<TResult> ExecuteToolAsync<TResult>(string toolName, object parameters, CancellationToken ct = default)
        {
            try
            {
                var result = await _client.CallToolAsync(toolName, parameters, ct);
                return JsonSerializer.Deserialize<TResult>(JsonSerializer.Serialize(result));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute MCP tool {ToolName}", toolName);
                throw;
            }
        }
        
        public async ValueTask DisposeAsync()
        {
            await _client.DisposeAsync();
        }
    }
}
```

### CQRS Integration

```csharp
// Application layer - Command/Query handlers
namespace Axon.Modules.Chat.Application.Commands
{
    public record ExecuteMcpToolCommand(string ToolName, object Parameters) : IRequest<Result<object>>;
    
    public sealed class ExecuteMcpToolHandler : IRequestHandler<ExecuteMcpToolCommand, Result<object>>
    {
        private readonly IMcpClientService _mcpClient;
        
        public ExecuteMcpToolHandler(IMcpClientService mcpClient)
        {
            _mcpClient = mcpClient;
        }
        
        public async Task<Result<object>> Handle(ExecuteMcpToolCommand request, CancellationToken ct)
        {
            try
            {
                var result = await _mcpClient.ExecuteToolAsync<object>(
                    request.ToolName, 
                    request.Parameters, 
                    ct);
                    
                return Result.Success(result);
            }
            catch (Exception ex)
            {
                return Result.Failure($"MCP tool execution failed: {ex.Message}");
            }
        }
    }
}
```

### Dependency Registration

```csharp
// Infrastructure layer - Service registration
namespace Axon.Modules.Chat.Infrastructure
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddMcpServices(
            this IServiceCollection services, 
            IConfiguration configuration)
        {
            // Register MCP client as singleton for reuse
            services.AddSingleton<IMcpClient>(serviceProvider =>
            {
                var options = configuration.GetSection("Mcp").Get<McpClientOptions>();
                var transport = new StdioClientTransport(new StdioClientTransportOptions
                {
                    Name = options.ServerName,
                    Command = options.Command,
                    Arguments = options.Arguments
                });
                
                return McpClientFactory.CreateAsync(transport).GetAwaiter().GetResult();
            });
            
            services.AddScoped<IMcpClientService, McpClientService>();
            
            return services;
        }
    }
    
    public class McpClientOptions
    {
        public string ServerName { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string[] Arguments { get; set; } = Array.Empty<string>();
    }
}
```

## Code Examples for .NET 10

### Modern C# Features Integration

```csharp
// File-scoped namespaces (C# 10+)
namespace Axon.Modules.Chat.Infrastructure.Mcp;

// Primary constructors (C# 12)
public sealed class McpToolExecutor(
    IMcpClient client,
    ILogger<McpToolExecutor> logger) : IMcpToolExecutor
{
    // Target-typed new expressions
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    // Pattern matching with switch expressions
    public async Task<Result<T>> ExecuteAsync<T>(McpToolRequest request, CancellationToken ct = default)
    {
        return request.ToolType switch
        {
            ToolType.Analysis => await ExecuteAnalysisToolAsync<T>(request, ct),
            ToolType.DataRetrieval => await ExecuteDataRetrievalToolAsync<T>(request, ct),
            ToolType.Transformation => await ExecuteTransformationToolAsync<T>(request, ct),
            _ => Result.Failure<T>("Unknown tool type")
        };
    }

    // Records for clean DTOs
    public record McpToolRequest(
        string ToolName,
        ToolType ToolType,
        object Parameters,
        TimeSpan? Timeout = null);

    // Required members pattern
    public class McpExecutionContext
    {
        public required string UserId { get; init; }
        public required string SessionId { get; init; }
        public Dictionary<string, object> Metadata { get; init; } = new();
    }

    // Async enumerable for streaming results
    public async IAsyncEnumerable<McpToolResult> ExecuteStreamingToolAsync(
        string toolName,
        object parameters,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var tools = await client.ListToolsAsync(ct);
        
        foreach (var tool in tools.Where(t => t.Name.Contains(toolName)))
        {
            var result = await client.CallToolAsync(tool.Name, parameters, ct);
            yield return new McpToolResult(tool.Name, result, DateTime.UtcNow);
        }
    }
}

// Global using statements (typically in GlobalUsings.cs)
global using ModelContextProtocol;
global using ModelContextProtocol.Client;
global using ModelContextProtocol.Protocol;
global using Microsoft.Extensions.Logging;
global using System.Text.Json;
```

### Error Handling with Modern Patterns

```csharp
// Result pattern with nullable reference types
public sealed class McpClientWrapper : IMcpClientWrapper
{
    private readonly IMcpClient? _client;
    private readonly ILogger<McpClientWrapper> _logger;

    public McpClientWrapper(IMcpClient? client, ILogger<McpClientWrapper> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<Result<TResponse>> ExecuteToolSafelyAsync<TResponse>(
        string toolName, 
        object parameters,
        CancellationToken ct = default) where TResponse : class
    {
        if (_client is null)
        {
            return Result.Failure<TResponse>("MCP client not initialized");
        }

        try
        {
            var result = await _client.CallToolAsync(toolName, parameters, ct);
            
            return result switch
            {
                null => Result.Failure<TResponse>("Tool returned null result"),
                TResponse response => Result.Success(response),
                _ => ConvertResult<TResponse>(result)
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogWarning("MCP tool execution cancelled for {ToolName}", toolName);
            return Result.Failure<TResponse>("Operation was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP tool execution failed for {ToolName}", toolName);
            return Result.Failure<TResponse>($"Tool execution failed: {ex.Message}");
        }
    }

    private Result<TResponse> ConvertResult<TResponse>(object result) where TResponse : class
    {
        try
        {
            var json = JsonSerializer.Serialize(result);
            var converted = JsonSerializer.Deserialize<TResponse>(json);
            
            return converted is not null 
                ? Result.Success(converted)
                : Result.Failure<TResponse>("Failed to convert result to expected type");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to convert MCP result to {Type}", typeof(TResponse).Name);
            return Result.Failure<TResponse>("Result conversion failed");
        }
    }
}
```

## Summary and Recommendations

### When to Use ModelContextProtocol.Core

**Recommended for Axon Backend scenarios:**
- ✅ Microservice implementations requiring minimal dependencies
- ✅ High-performance client implementations
- ✅ Container-based deployments where size matters
- ✅ Custom transport implementations
- ✅ Integration with existing DI containers (manual registration)

**Consider Main Package instead when:**
- ❌ Need full ASP.NET Core integration
- ❌ Want built-in hosting and lifecycle management
- ❌ Require extensive middleware support
- ❌ Need automatic service discovery and configuration

### Integration Strategy for Axon Backend

1. **Use Core package** for lightweight MCP client implementations
2. **Manual DI registration** in Infrastructure layer
3. **Abstract behind domain interfaces** for clean architecture compliance
4. **Implement proper disposal patterns** for resource management
5. **Add caching and pooling** for performance optimization
6. **Use modern C# patterns** for clean, maintainable code

The ModelContextProtocol.Core package provides an excellent foundation for building efficient MCP integrations in the Axon Backend's modular monolith architecture while maintaining clean boundaries and minimal dependencies.