---
id: AXON-20250729-Libraries-ModelContextProtocol-IMPLEMENTATION_GUIDE
title: ModelContextProtocol: Implementation Guide
module: Libraries
feature: ModelContextProtocol
gate: G1
owner: <owner>
status: draft
relates_to: [docs/references/Direct_Mcp_OpenAI.md]
source_of_truth: doc
created: 2025-07-29
updated: 2025-07-29
version: 1
---

# ModelContextProtocol 0.3.0-preview.3 Implementation Guide

## Executive Summary

**ModelContextProtocol** is an open standard that enables secure, two-way connections between AI systems and external data sources, tools, and resources. This guide covers the **ModelContextProtocol 0.3.0-preview.3** package for .NET 10 compatibility in the Axon Backend project.

**Key Benefits:**
- **Standardized AI Integration**: Open protocol for connecting LLMs to external systems
- **Microsoft Collaboration**: Official C# SDK maintained in collaboration with Microsoft
- **Clean Architecture Compatible**: Fits well in Infrastructure layer for external service integration
- **Multiple Transport Support**: HTTP, stdio, and SSE transport mechanisms

**Status**: Preview package with potential breaking changes

---

## Package Overview

### Core Package Information

| Package | Version | Purpose | Stability |
|---------|---------|---------|-----------|
| `ModelContextProtocol` | 0.3.0-preview.3 | Main package with hosting/DI extensions | Preview |
| `ModelContextProtocol.AspNetCore` | 0.3.0-preview.2 | HTTP-based MCP servers | Preview |
| `ModelContextProtocol.Core` | 0.3.3-alpha | Minimal client/server APIs | Preview |

### Installation

```bash
# Primary package for most scenarios
dotnet add package ModelContextProtocol --prerelease

# For HTTP server capabilities
dotnet add package ModelContextProtocol.AspNetCore --prerelease

# Minimal dependencies version
dotnet add package ModelContextProtocol.Core --prerelease
```

### Project File Configuration

```xml
<PackageReference Include="ModelContextProtocol" Version="0.3.0-preview.3" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="9.0.0" />
```

---

## .NET 10 Compatibility Analysis

### Compatibility Status

**Current Framework Support:**
- **.NET 8.0**: ✅ Explicitly supported
- **.NET Standard 2.0**: ✅ Explicitly supported
- **.NET 10**: ⚠️ **Not explicitly tested** - Likely compatible via .NET Standard 2.0

### .NET 10 Compatibility Assessment

**High Likelihood of Compatibility:**
1. **Framework Design**: Targets .NET Standard 2.0 which is forward-compatible
2. **Microsoft Involvement**: Official collaboration with Microsoft suggests .NET 10 awareness
3. **Preview Status**: Active development aligns with .NET 10 preview timeline
4. **JSON-RPC Foundation**: Protocol uses standard JSON-RPC 2.0 over HTTP/stdio

**Risk Factors:**
- Preview status means potential breaking changes
- No explicit .NET 10 TFM testing documented
- MCP ecosystem is rapidly evolving

**Recommendation**: ✅ **APPLY WITH MONITORING** - Use in Axon Backend with version pinning and release monitoring

---

## Core MCP Concepts

### 1. Tools
**Model-controlled functions** that LLMs can invoke to perform actions.

```csharp
[McpServerToolType]
public static class WeatherTools
{
    [McpServerTool, Description("Get current weather for a location")]
    public static async Task<string> GetWeather(
        [Description("City name")] string city,
        [Description("Country code")] string country = "US")
    {
        // Implementation here
        return $"Weather in {city}, {country}: 22°C, Sunny";
    }
}
```

### 2. Resources  
**Application-controlled data sources** that provide contextual information without side effects.

```csharp
// Resources are typically provided by the server
// Client can discover and access them read-only
var resources = await client.ListResourcesAsync();
foreach (var resource in resources)
{
    var content = await client.ReadResourceAsync(resource.Uri);
}
```

### 3. Prompts
**User-controlled templates** that provide structured workflows for optimal tool/resource usage.

```csharp
// Prompts provide templated starting points
var prompts = await client.ListPromptsAsync();
var prompt = await client.GetPromptAsync("weather-analysis", new { location = "Boston" });
```

---

## MCP Client Implementation

### Basic Client Setup

```csharp
using ModelContextProtocol.Client;

// Create client with stdio transport
var clientTransport = new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "WeatherService",
    Command = "node",
    Arguments = ["weather-mcp-server.js"]
});

await using var client = await McpClientFactory.CreateAsync(clientTransport);
```

### HTTP Client Configuration

```csharp
using ModelContextProtocol.Client;

// Create HTTP client
var httpTransport = new HttpMcpClientTransportOptions
{
    Endpoint = new Uri("https://api.weather-service.com/mcp"),
    Headers = new Dictionary<string, string>
    {
        ["Authorization"] = "Bearer your-api-key"
    }
};

await using var client = await McpClientFactory.CreateAsync(httpTransport);
```

### Tool Discovery and Invocation

```csharp
// Discover available tools
var tools = await client.ListToolsAsync();
Console.WriteLine($"Available tools: {string.Join(", ", tools.Select(t => t.Name))}");

// Invoke a specific tool
var result = await client.CallToolAsync("get_weather", new
{
    city = "Boston",
    country = "US"
});

Console.WriteLine($"Tool result: {result.Content}");
```

### Resource Access

```csharp
// List available resources
var resources = await client.ListResourcesAsync();

// Read a specific resource
var resource = resources.FirstOrDefault(r => r.Name == "weather-data");
if (resource != null)
{
    var content = await client.ReadResourceAsync(resource.Uri);
    Console.WriteLine($"Resource content: {content}");
}
```

---

## MCP Server Implementation

### Basic Server Setup

```csharp
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly(); // Auto-discover tools

var host = builder.Build();
await host.RunAsync();
```

### Tool Registration

```csharp
[McpServerToolType]
public class CalculatorTools
{
    [McpServerTool, Description("Add two numbers")]
    public static double Add(
        [Description("First number")] double a,
        [Description("Second number")] double b)
    {
        return a + b;
    }

    [McpServerTool, Description("Multiply two numbers")]
    public static async Task<double> MultiplyAsync(double a, double b)
    {
        // Simulate async operation
        await Task.Delay(100);
        return a * b;
    }
}
```

### Dependency Injection in Tools  

```csharp
[McpServerToolType]
public class DatabaseTools
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<DatabaseTools> _logger;

    public DatabaseTools(IUserRepository userRepository, ILogger<DatabaseTools> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    [McpServerTool, Description("Get user by ID")]
    public async Task<UserDto> GetUser([Description("User ID")] int userId)
    {
        _logger.LogInformation("Fetching user {UserId}", userId);
        var user = await _userRepository.GetByIdAsync(userId);
        return user?.ToDto();
    }
}
```

---

## Available Methods and APIs

### IMcpClient Interface

```csharp
public interface IMcpClient : IAsyncDisposable
{
    // Tool Management
    Task<IReadOnlyList<Tool>> ListToolsAsync(CancellationToken cancellationToken = default);
    Task<ToolResult> CallToolAsync(string name, object arguments, CancellationToken cancellationToken = default);

    // Resource Management  
    Task<IReadOnlyList<Resource>> ListResourcesAsync(CancellationToken cancellationToken = default);
    Task<ResourceContent> ReadResourceAsync(string uri, CancellationToken cancellationToken = default);

    // Prompt Management
    Task<IReadOnlyList<Prompt>> ListPromptsAsync(CancellationToken cancellationToken = default);
    Task<PromptResult> GetPromptAsync(string name, object arguments = null, CancellationToken cancellationToken = default);

    // Server Information
    Task<ServerInfo> GetServerInfoAsync(CancellationToken cancellationToken = default);
}
```

### Key Data Types

```csharp
// Tool Definition
public class Tool
{
    public string Name { get; set; }
    public string Description { get; set; }
    public JsonSchema InputSchema { get; set; }
}

// Tool Result
public class ToolResult
{
    public IList<ContentItem> Content { get; set; }
    public bool IsError { get; set; }
}

// Resource Definition
public class Resource
{
    public string Uri { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string MimeType { get; set; }
}

// Prompt Definition
public class Prompt
{
    public string Name { get; set; }
    public string Description { get; set; }
    public JsonSchema ArgumentsSchema { get; set; }
}
```

---

## Transport Mechanisms

### 1. Standard I/O (stdio) Transport

**Use Case**: Local tools, command-line applications, development

```csharp
var stdioTransport = new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "LocalTool",
    Command = "python",
    Arguments = ["mcp_server.py"],
    WorkingDirectory = "/path/to/server"
});
```

### 2. HTTP Transport

**Use Case**: Remote services, production deployments, web APIs

```csharp
var httpTransport = new HttpMcpClientTransportOptions
{
    Endpoint = new Uri("https://api.example.com/mcp"),
    Headers = new Dictionary<string, string>
    {
        ["Authorization"] = "Bearer token",
        ["User-Agent"] = "Axon-Backend/1.0"
    },
    Timeout = TimeSpan.FromSeconds(30)
};
```

### 3. Server-Sent Events (SSE) Transport

**Use Case**: Real-time updates, streaming data

```csharp
// SSE transport for streaming scenarios
builder.Services.AddMcpServer()
    .WithServerSentEventsTransport(options =>
    {
        options.Path = "/mcp/sse";
        options.HeartbeatInterval = TimeSpan.FromSeconds(30);
    });
```

---

## Security Considerations

### Authentication Patterns

```csharp
// HTTP Bearer Token Authentication
var clientOptions = new HttpMcpClientTransportOptions
{
    Endpoint = new Uri("https://secure-mcp-server.com/api"),
    Headers = new Dictionary<string, string>
    {
        ["Authorization"] = $"Bearer {await GetAccessTokenAsync()}"
    }
};

// Custom authentication header
clientOptions.Headers["X-API-Key"] = configuration["McpServer:ApiKey"];
```

### Authorization and Trust

```csharp
// Implement tool approval workflow
public class SecureMcpClient : IMcpClient
{
    private readonly IMcpClient _innerClient;
    private readonly IToolApprovalService _approvalService;

    public async Task<ToolResult> CallToolAsync(string name, object arguments, CancellationToken ct)
    {
        // Require explicit approval for sensitive tools
        if (await _approvalService.RequiresApprovalAsync(name))
        {
            var approved = await _approvalService.RequestApprovalAsync(name, arguments);
            if (!approved)
                throw new UnauthorizedAccessException($"Tool {name} not approved");
        }

        return await _innerClient.CallToolAsync(name, arguments, ct);
    }
}
```

### Data Protection

```csharp
// Sanitize tool arguments before transmission
public class SanitizingMcpClient : IMcpClient
{
    public async Task<ToolResult> CallToolAsync(string name, object arguments, CancellationToken ct)
    {
        // Remove sensitive data from arguments
        var sanitizedArgs = _dataSanitizer.Sanitize(arguments);
        return await _innerClient.CallToolAsync(name, sanitizedArgs, ct);
    }
}
```

---

## Integration with OpenAI Direct MCP

### Complementary Usage Pattern

The **ModelContextProtocol** package complements the **Direct MCP** pattern documented in `docs/references/Direct_Mcp_OpenAI.md`:

```csharp
// Use ModelContextProtocol for tool discovery
await using var mcpClient = await McpClientFactory.CreateAsync(transportOptions);
var availableTools = await mcpClient.ListToolsAsync();

// Convert to OpenAI Direct MCP format
var openAiTools = availableTools.Select(tool => new
{
    type = "mcp",
    server_url = "https://your-mcp-server.com/api",
    server_label = tool.Name,
    allowed_tools = new[] { tool.Name }
}).ToArray();

// Use in OpenAI Responses API call
var requestPayload = new
{
    model = "gpt-4o",
    input = userInput,
    tools = openAiTools
};
```

### Combined Architecture Pattern

```csharp
public class HybridMcpService
{
    private readonly IMcpClient _mcpClient;
    private readonly HttpClient _openAiClient;

    // Discover tools via MCP client
    public async Task<Tool[]> DiscoverToolsAsync()
    {
        return (await _mcpClient.ListToolsAsync()).ToArray();
    }

    // Execute via OpenAI Direct MCP for performance
    public async Task<string> ExecuteWithDirectMcpAsync(string prompt, Tool[] tools)
    {
        var payload = new
        {
            model = "gpt-4o",
            input = prompt,
            tools = tools.Select(t => new { type = "mcp", server_url = _serverUrl, allowed_tools = new[] { t.Name } })
        };

        // Direct OpenAI call with MCP tools
        var response = await _openAiClient.PostAsJsonAsync("/v1/responses", payload);
        return await response.Content.ReadAsStringAsync();
    }
}
```

---

## Clean Architecture Integration

### Infrastructure Layer Implementation

```csharp
// Axon.Modules.Chat.Infrastructure/Clients/McpClientAdapter.cs
namespace Axon.Modules.Chat.Infrastructure.Clients;

public sealed class McpClientAdapter : IMcpClientPort
{
    private readonly IMcpClient _mcpClient;
    private readonly ILogger<McpClientAdapter> _logger;

    public McpClientAdapter(IMcpClient mcpClient, ILogger<McpClientAdapter> logger)
    {
        _mcpClient = mcpClient;
        _logger = logger;
    }

    public async Task<Result<ExternalTool[]>> GetAvailableToolsAsync(CancellationToken ct)
    {
        try
        {
            var tools = await _mcpClient.ListToolsAsync(ct);
            var domainTools = tools.Select(t => new ExternalTool(t.Name, t.Description)).ToArray();
            return Result.Success(domainTools);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve MCP tools");
            return Result.Failure<ExternalTool[]>(Error.External("Failed to retrieve external tools"));
        }
    }

    public async Task<Result<string>> InvokeToolAsync(string toolName, object arguments, CancellationToken ct)
    {
        try
        {
            var result = await _mcpClient.CallToolAsync(toolName, arguments, ct);
            return Result.Success(result.Content.FirstOrDefault()?.Text ?? "No content");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invoke MCP tool {ToolName}", toolName);
            return Result.Failure<string>(Error.External($"Tool invocation failed: {toolName}"));
        }
    }
}
```

### Application Layer Port Definition

```csharp
// Axon.Modules.Chat.Application/Ports/IMcpClientPort.cs
namespace Axon.Modules.Chat.Application.Ports;

public interface IMcpClientPort
{
    Task<Result<ExternalTool[]>> GetAvailableToolsAsync(CancellationToken ct = default);
    Task<Result<string>> InvokeToolAsync(string toolName, object arguments, CancellationToken ct = default);
}
```

### Dependency Registration

```csharp
// Axon.Modules.Chat.Infrastructure/DependencyInjection.cs
public static class DependencyInjection
{
    public static IServiceCollection AddChatInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // MCP Client configuration
        services.Configure<McpClientOptions>(configuration.GetSection("McpClient"));
        
        services.AddSingleton<IMcpClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<McpClientOptions>>().Value;
            var transport = new HttpMcpClientTransportOptions
            {
                Endpoint = new Uri(options.ServerUrl),
                Headers = options.Headers
            };
            return McpClientFactory.CreateAsync(transport).GetAwaiter().GetResult();
        });

        services.AddScoped<IMcpClientPort, McpClientAdapter>();
        
        return services;
    }
}
```

---

## Code Examples for .NET 10

### Modern C# Features Integration

```csharp
// Using primary constructors (.NET 10 feature)
public sealed class ModernMcpService(
    IMcpClient mcpClient,
    ILogger<ModernMcpService> logger,
    IOptions<McpOptions> options) : IMcpService
{
    private readonly McpOptions _options = options.Value;

    // Using target-typed new expressions
    public async Task<Result<ToolInvocationResult>> InvokeToolAsync(ToolInvocationRequest request)
    {
        try
        {
            // Pattern matching with modern syntax
            var result = await mcpClient.CallToolAsync(request.ToolName, request.Arguments);
            
            return result switch
            {
                { IsError: true } => Result.Failure<ToolInvocationResult>(Error.External(result.Content.First().Text)),
                { Content.Count: 0 } => Result.Failure<ToolInvocationResult>(Error.External("No content returned")),
                _ => Result.Success(new ToolInvocationResult(result.Content.First().Text))
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tool invocation failed for {ToolName}", request.ToolName);
            return Result.Failure<ToolInvocationResult>(Error.External("Tool invocation failed"));
        }
    }
}
```

### File-scoped Namespaces and Records

```csharp
// Modern C# file structure
namespace Axon.Modules.Chat.Infrastructure.Mcp;

// Record types for DTOs
public sealed record McpToolRequest(string Name, Dictionary<string, object> Arguments);
public sealed record McpToolResponse(string Content, bool Success, string? Error = null);

// Collection expressions (.NET 10)
public sealed class McpToolRegistry
{
    private readonly Dictionary<string, Tool> _tools = [];

    public void RegisterTools(IEnumerable<Tool> tools)
    {
        foreach (var tool in tools)
        {
            _tools[tool.Name] = tool;
        }
    }

    public Tool[] GetAvailableTools() => [.. _tools.Values];
}
```

---

## Comparison Preparation

### Package Ecosystem Analysis

| Package | Purpose | Stability | .NET 10 Ready |
|---------|---------|-----------|----------------|
| **ModelContextProtocol** | Main MCP SDK | Preview | Likely ✅ |
| **ModelContextProtocol.AspNetCore** | HTTP server hosting | Preview | Likely ✅ |
| **ModelContextProtocol.Core** | Minimal APIs | Alpha | Likely ✅ |

### Alternative Implementations

```csharp
// Direct HTTP implementation (alternative to SDK)
public class DirectMcpClient
{
    private readonly HttpClient _httpClient;

    public async Task<Tool[]> ListToolsAsync()
    {
        var response = await _httpClient.GetAsync("/tools/list");
        return await response.Content.ReadFromJsonAsync<Tool[]>();
    }

    public async Task<object> CallToolAsync(string name, object args)
    {
        var request = new { method = "tools/call", params = new { name, arguments = args } };
        var response = await _httpClient.PostAsJsonAsync("/tools/call", request);
        return await response.Content.ReadFromJsonAsync<object>();
    }
}
```

---

## Recommendations

### For Axon Backend Project

1. **✅ ADOPT**: Use ModelContextProtocol 0.3.0-preview.3 for MCP client functionality
2. **📌 PIN VERSION**: Explicitly pin version in Directory.Build.props due to preview status  
3. **🔍 MONITOR**: Watch for breaking changes and .NET 10 compatibility updates
4. **🏗️ ARCHITECTURE**: Implement in Infrastructure layer following Clean Architecture principles
5. **⚡ COMBINE**: Use with OpenAI Direct MCP pattern for optimal performance

### Version Management

```xml
<!-- Directory.Build.props -->
<PropertyGroup>
  <McpVersion>0.3.0-preview.3</McpVersion>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="ModelContextProtocol" Version="$(McpVersion)" />
</ItemGroup>
```

### Monitoring Strategy

```csharp
// Configuration for version monitoring
public class McpConfiguration
{
    public string Version { get; set; } = "0.3.0-preview.3";
    public bool EnableVersionCheck { get; set; } = true;
    public string ServerUrl { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
}
```

---

## Next Steps

1. **Implementation**: Start with basic MCP client integration in Chat module
2. **Testing**: Create integration tests with mock MCP servers
3. **Documentation**: Update API contracts to include MCP tool definitions
4. **Monitoring**: Set up alerts for package updates and breaking changes
5. **Security Review**: Implement approval workflows for production tool usage

---

## References

- [Official MCP Specification 2025-03-26](https://modelcontextprotocol.io/specification/2025-03-26)
- [ModelContextProtocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- [Microsoft .NET Blog: Build MCP Server in C#](https://devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp/)
- [Direct MCP OpenAI Integration](docs/references/Direct_Mcp_OpenAI.md)
- [NuGet: ModelContextProtocol 0.3.0-preview.3](https://www.nuget.org/packages/ModelContextProtocol)