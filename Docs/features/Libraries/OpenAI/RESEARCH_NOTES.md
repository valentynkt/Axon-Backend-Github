# Research Notes: OpenAI SDK for .NET 2.2.0 with MCP Integration

## Research Question
Research the OpenAI SDK for .NET version 2.2.0 with highly detailed focus on MCP (Model Context Protocol) components for integration with Axon Backend's modular monolith architecture.

## Primary Sources
- **OpenAI .NET SDK Official Repository**: https://github.com/openai/openai-dotnet
- **NuGet Package**: https://www.nuget.org/packages/OpenAI (version 2.2.0)
- **Microsoft MCP Documentation**: https://learn.microsoft.com/en-us/dotnet/ai/get-started-mcp
- **MCP C# SDK**: https://github.com/modelcontextprotocol/csharp-sdk
- **ModelContextProtocol NuGet**: https://www.nuget.org/packages/ModelContextProtocol

## Findings

### 1. Core SDK Overview

#### Package Details and Installation
```bash
# Official OpenAI .NET SDK
dotnet add package OpenAI --version 2.2.0

# For MCP integration with Microsoft.Extensions.AI
dotnet add package Microsoft.Extensions.AI
dotnet add package Microsoft.Extensions.AI.OpenAI
dotnet add package ModelContextProtocol --prerelease
```

#### .NET 10 Compatibility
- **Target Framework**: Compatible with .NET Standard 2.0+ applications
- **Language Support**: Supports latest C# language features used in .NET 10
- **Nullable Reference Types**: Fully supported and enabled
- **Modern C# Patterns**: Primary constructors, file-scoped namespaces, target-typed new

#### Authentication Patterns
```csharp
// Environment variable approach (recommended)
var client = new OpenAIClient(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));

// Direct API key configuration
var client = new OpenAIClient("your-api-key-here");

// Custom configuration with options
var options = new OpenAIClientOptions
{
    Endpoint = new Uri("https://api.openai.com/v1/"),
    // Additional configuration
};
var client = new OpenAIClient(apiKey, options);
```

#### Core Client Types
- **OpenAIClient**: Main client for accessing all OpenAI services
- **ChatClient**: Specialized client for chat completions
- **EmbeddingClient**: For text embeddings
- **AudioClient**: For speech-to-text and text-to-speech
- **ImageClient**: For image generation and manipulation

### 2. MCP Components (CRITICAL - HIGH DETAIL)

#### MCP Architecture Overview
The OpenAI SDK .NET 2.2.0 does NOT include direct MCP components. MCP integration is achieved through:
1. **Microsoft MCP C# SDK** (`ModelContextProtocol` package)
2. **Microsoft.Extensions.AI** abstraction layer
3. **OpenAI .NET SDK** for LLM communication

#### MCP Client Implementation
```csharp
using ModelContextProtocol.Client;
using Microsoft.Extensions.AI;

// Create MCP client with different transport mechanisms
IMcpClient mcpClient = await McpClientFactory.CreateAsync(
    new StdioClientTransport(new StdioClientTransportOptions
    {
        Command = "dotnet run",
        Arguments = ["--project", "path/to/mcp/server"],
        Name = "My MCP Server"
    }));

// HTTP-based MCP client
IMcpClient httpMcpClient = await McpClientFactory.CreateAsync(
    new HttpMcpClientTransport(new HttpMcpClientTransportOptions
    {
        Endpoint = new Uri("https://api.example.com/mcp")
    }));
```

#### MCP Server Hosting Patterns
```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()  // For stdio communication
    .WithToolsFromAssembly();    // Auto-discover tools from assembly

// For HTTP-based MCP server
builder.Services
    .AddMcpServer()
    .WithHttpServerTransport()   // Requires ModelContextProtocol.AspNetCore
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
```

#### Resource Mapping and Protocol Handling
```csharp
// MCP Resource definition
public class WeatherResource : IMcpResource
{
    public string Uri { get; set; } = "weather://current";
    public string Name { get; set; } = "Current Weather";
    public string Description { get; set; } = "Current weather information";
}

// Protocol message handling
public class McpServerOptions
{
    public Implementation ServerInfo { get; set; } = new()
    {
        Name = "Axon Weather Server",
        Version = "1.0.0"
    };
    
    public ServerCapabilities Capabilities { get; set; } = new()
    {
        Tools = new ToolsCapability
        {
            ListToolsHandler = async (request, ct) => new ListToolsResult
            {
                Tools = [/* tool definitions */]
            }
        },
        Resources = new ResourcesCapability
        {
            ListResourcesHandler = async (request, ct) => new ListResourcesResult
            {
                Resources = [/* resource definitions */]
            }
        }
    };
}
```

#### Tool Integration and Function Calling
```csharp
// Tool definition using attributes
[McpServerToolType]
public static class WeatherTools
{
    [McpServerTool]
    [Description("Gets current weather for a city")]
    public static async Task<string> GetWeather(
        [Description("City name")] string city,
        [Description("Country code")] string country = "US")
    {
        // Implementation
        return $"Weather in {city}, {country}: 22°C, Sunny";
    }
}

// Manual tool registration
public class CustomToolHandler : IToolHandler
{
    public async Task<ToolResult> HandleToolCallAsync(
        ToolCall toolCall, 
        CancellationToken cancellationToken)
    {
        return toolCall.Name switch
        {
            "get_weather" => await HandleWeatherTool(toolCall, cancellationToken),
            _ => new ToolResult { Error = "Unknown tool" }
        };
    }
}
```

#### Request/Response Models for MCP Operations
```csharp
// Tool call request model
public record ToolCall
{
    public string Name { get; init; } = string.Empty;
    public JsonElement Arguments { get; init; }
    public string CallId { get; init; } = string.Empty;
}

// Tool result response model
public record ToolResult
{
    public object? Content { get; init; }
    public string? Error { get; init; }
    public bool IsError => !string.IsNullOrEmpty(Error);
}

// List tools response
public record ListToolsResult
{
    public IList<Tool> Tools { get; init; } = new List<Tool>();
}

// Resource content response
public record ResourceContent
{
    public string Uri { get; init; } = string.Empty;
    public string MimeType { get; init; } = "text/plain";
    public object Content { get; init; } = string.Empty;
}
```

#### Connection Management and Lifecycle
```csharp
public class McpClientManager : IDisposable
{
    private readonly Dictionary<string, IMcpClient> _clients = new();
    
    public async Task<IMcpClient> GetOrCreateClientAsync(string serverName, McpTransportOptions options)
    {
        if (_clients.TryGetValue(serverName, out var existingClient))
            return existingClient;
            
        var client = await McpClientFactory.CreateAsync(options);
        _clients[serverName] = client;
        return client;
    }
    
    public async Task DisconnectAsync(string serverName)
    {
        if (_clients.TryGetValue(serverName, out var client))
        {
            await client.DisconnectAsync();
            _clients.Remove(serverName);
        }
    }
    
    public void Dispose()
    {
        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }
        _clients.Clear();
    }
}
```

#### Error Handling and Retry Policies
```csharp
using Polly;
using Polly.Extensions.Http;

public class ResilientMcpClient
{
    private readonly IMcpClient _mcpClient;
    private readonly IAsyncPolicy _retryPolicy;
    
    public ResilientMcpClient(IMcpClient mcpClient)
    {
        _mcpClient = mcpClient;
        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<McpException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => 
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    // Log retry attempt
                });
    }
    
    public async Task<IList<McpClientTool>> ListToolsWithRetryAsync()
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            return await _mcpClient.ListToolsAsync();
        });
    }
}
```

### 3. Integration Patterns

#### Dependency Injection Setup
```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using OpenAI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAxonAiServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Register OpenAI client
        services.AddSingleton<OpenAIClient>(provider =>
        {
            var apiKey = configuration["OpenAI:ApiKey"] ?? 
                Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            return new OpenAIClient(apiKey);
        });
        
        // Register chat client with Microsoft.Extensions.AI abstraction
        services.AddSingleton<IChatClient>(provider =>
        {
            var openAiClient = provider.GetRequiredService<OpenAIClient>();
            return openAiClient.GetChatClient("gpt-4o")
                .AsIChatClient()
                .UseFunctionInvocation();
        });
        
        // Register MCP client factory
        services.AddSingleton<IMcpClientFactory, McpClientFactory>();
        
        // Register MCP client manager
        services.AddScoped<McpClientManager>();
        
        return services;
    }
}
```

#### Clean Architecture Integration
```csharp
// Domain layer - interfaces
namespace Axon.Modules.Chat.Domain.Services;

public interface IAiChatService
{
    Task<Result<string>> ProcessMessageAsync(string message, CancellationToken ct);
}

// Application layer - implementation
namespace Axon.Modules.Chat.Application.Services;

public sealed class AiChatService : IAiChatService
{
    private readonly IChatClient _chatClient;
    private readonly McpClientManager _mcpClientManager;
    private readonly ILogger<AiChatService> _logger;
    
    public AiChatService(
        IChatClient chatClient,
        McpClientManager mcpClientManager,
        ILogger<AiChatService> logger)
    {
        _chatClient = chatClient;
        _mcpClientManager = mcpClientManager;
        _logger = logger;
    }
    
    public async Task<Result<string>> ProcessMessageAsync(string message, CancellationToken ct)
    {
        try
        {
            // Get available MCP tools
            var mcpClient = await _mcpClientManager.GetOrCreateClientAsync(
                "weather-server", 
                new StdioClientTransportOptions { /* config */ });
                
            var tools = await mcpClient.ListToolsAsync();
            
            // Configure chat with tools
            var chatOptions = new ChatOptions
            {
                Tools = tools.Cast<AIFunction>().ToList()
            };
            
            // Process message
            var response = await _chatClient.GetResponseAsync(message, chatOptions, ct);
            return Result.Success(response.Message.Text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process AI chat message");
            return Result.Failure($"Chat processing failed: {ex.Message}");
        }
    }
}
```

#### CQRS Pattern Compatibility
```csharp
// Command
public record ProcessChatMessageCommand(
    string Message, 
    Guid UserId, 
    string[]? EnabledTools = null) : IRequest<Result<ChatResponse>>;

// Handler
public sealed class ProcessChatMessageHandler 
    : IRequestHandler<ProcessChatMessageCommand, Result<ChatResponse>>
{
    private readonly IAiChatService _aiChatService;
    private readonly IChatRepository _chatRepository;
    
    public ProcessChatMessageHandler(
        IAiChatService aiChatService,
        IChatRepository chatRepository)
    {
        _aiChatService = aiChatService;
        _chatRepository = chatRepository;
    }
    
    public async Task<Result<ChatResponse>> Handle(
        ProcessChatMessageCommand request, 
        CancellationToken ct)
    {
        // Process with AI
        var aiResponse = await _aiChatService.ProcessMessageAsync(request.Message, ct);
        if (aiResponse.IsFailure)
            return aiResponse.Error;
        
        // Store conversation
        var conversation = new Conversation(
            ConversationId.New(),
            request.UserId,
            request.Message,
            aiResponse.Value);
            
        await _chatRepository.SaveAsync(conversation, ct);
        
        return new ChatResponse(aiResponse.Value, conversation.Id.Value);
    }
}
```

#### Configuration Management
```csharp
// Configuration models
public class OpenAiConfiguration
{
    public const string SectionName = "OpenAI";
    
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";
    public int MaxTokens { get; set; } = 4000;
    public double Temperature { get; set; } = 0.7;
}

public class McpConfiguration
{
    public const string SectionName = "MCP";
    
    public Dictionary<string, McpServerConfig> Servers { get; set; } = new();
}

public class McpServerConfig
{
    public string Type { get; set; } = "stdio"; // stdio, http
    public string Command { get; set; } = string.Empty;
    public string[] Arguments { get; set; } = [];
    public string? Endpoint { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
}

// Configuration registration
services.Configure<OpenAiConfiguration>(
    configuration.GetSection(OpenAiConfiguration.SectionName));
services.Configure<McpConfiguration>(
    configuration.GetSection(McpConfiguration.SectionName));
```

### 4. Advanced Features

#### Streaming Responses
```csharp
public async Task<IAsyncEnumerable<string>> GetStreamingResponseAsync(
    string message, 
    CancellationToken ct)
{
    var chatOptions = new ChatOptions
    {
        Tools = await GetAvailableToolsAsync()
    };
    
    return _chatClient.GetStreamingResponseAsync(message, chatOptions, ct)
        .Select(update => update.Text)
        .Where(text => !string.IsNullOrEmpty(text));
}

// Usage in endpoint
[HttpPost("chat/stream")]
public async Task StreamChat(
    [FromBody] ChatRequest request, 
    CancellationToken ct)
{
    Response.ContentType = "text/event-stream";
    Response.Headers.CacheControl = "no-cache";
    Response.Headers.Connection = "keep-alive";
    
    await foreach (var chunk in _chatService.GetStreamingResponseAsync(request.Message, ct))
    {
        await Response.WriteAsync($"data: {JsonSerializer.Serialize(new { text = chunk })}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }
    
    await Response.WriteAsync("data: [DONE]\n\n", ct);
}
```

#### Function Calling Capabilities
```csharp
// Advanced function definition with complex parameters
[McpServerTool]
[Description("Searches for products with filters")]
public static async Task<ProductSearchResult> SearchProducts(
    [Description("Search query")] string query,
    [Description("Product category")] ProductCategory category,
    [Description("Price range")] PriceRange priceRange,
    [Description("Sort options")] SortOptions sortBy = SortOptions.Relevance)
{
    // Complex search implementation
    var filters = new ProductFilters
    {
        Query = query,
        Category = category,
        MinPrice = priceRange.Min,
        MaxPrice = priceRange.Max,
        SortBy = sortBy
    };
    
    return await _productService.SearchAsync(filters);
}

// Complex return types
public record ProductSearchResult(
    IList<Product> Products,
    int TotalCount,
    SearchMetadata Metadata);
```

#### Tool Use Patterns
```csharp
// Tool orchestration pattern
public class ToolOrchestrator
{
    private readonly Dictionary<string, Func<JsonElement, Task<object>>> _toolHandlers;
    
    public ToolOrchestrator()
    {
        _toolHandlers = new()
        {
            ["weather"] = HandleWeatherTool,
            ["calendar"] = HandleCalendarTool,
            ["email"] = HandleEmailTool
        };
    }
    
    public async Task<object> ExecuteToolAsync(string toolName, JsonElement arguments)
    {
        if (!_toolHandlers.TryGetValue(toolName, out var handler))
            throw new InvalidOperationException($"Unknown tool: {toolName}");
            
        return await handler(arguments);
    }
    
    private async Task<object> HandleWeatherTool(JsonElement args)
    {
        var city = args.GetProperty("city").GetString();
        return await _weatherService.GetWeatherAsync(city);
    }
}
```

#### Structured Outputs
```csharp
// Define structured output schema
public record WeatherResponse(
    string City,
    double Temperature,
    string Conditions,
    double Humidity,
    DateTime Timestamp);

// Use with structured output
public async Task<Result<WeatherResponse>> GetStructuredWeatherAsync(string city)
{
    var systemMessage = """
        You are a weather assistant. Always respond with structured JSON 
        containing: city, temperature, conditions, humidity, and timestamp.
        """;
    
    var chatOptions = new ChatOptions
    {
        ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
        Tools = await GetWeatherToolsAsync()
    };
    
    var messages = new[]
    {
        new ChatMessage(ChatRole.System, systemMessage),
        new ChatMessage(ChatRole.User, $"Get weather for {city}")
    };
    
    var response = await _chatClient.CompleteAsync(messages, chatOptions);
    
    try
    {
        var weatherResponse = JsonSerializer.Deserialize<WeatherResponse>(response.Message.Text);
        return Result.Success(weatherResponse);
    }
    catch (JsonException ex)
    {
        return Result.Failure($"Failed to parse structured response: {ex.Message}");
    }
}
```

#### Vision and Multimodal Support
```csharp
// Image analysis with MCP tools
public async Task<string> AnalyzeImageWithToolsAsync(byte[] imageData, string query)
{
    var imageContent = new ImageContent(imageData, "image/jpeg");
    var textContent = new TextContent(query);
    
    var message = new ChatMessage(ChatRole.User, [textContent, imageContent]);
    
    var chatOptions = new ChatOptions
    {
        Tools = await GetVisionToolsAsync() // MCP tools for image analysis
    };
    
    var response = await _chatClient.CompleteAsync([message], chatOptions);
    return response.Message.Text;
}

// Vision-enabled MCP tool
[McpServerTool]
[Description("Analyzes image content and extracts text")]
public static async Task<ImageAnalysisResult> AnalyzeImage(
    [Description("Base64 encoded image data")] string imageData,
    [Description("Analysis type")] ImageAnalysisType analysisType)
{
    var imageBytes = Convert.FromBase64String(imageData);
    
    return analysisType switch
    {
        ImageAnalysisType.Text => await ExtractTextFromImage(imageBytes),
        ImageAnalysisType.Objects => await DetectObjects(imageBytes),
        ImageAnalysisType.Scene => await AnalyzeScene(imageBytes),
        _ => throw new ArgumentException("Invalid analysis type")
    };
}
```

### 5. Implementation Examples

#### Complete MCP Client Setup
```csharp
// Program.cs for Axon Backend
using Axon.Shared.Common.Extensions;
using Axon.Modules.Chat.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add Axon services
builder.Services.AddAxonAiServices(builder.Configuration);
builder.Services.AddChatModule(builder.Configuration);

// Add MCP servers configuration
builder.Services.Configure<McpConfiguration>(
    builder.Configuration.GetSection("MCP"));

var app = builder.Build();

// Configure endpoints
app.MapChatEndpoints();

await app.RunAsync();

// appsettings.json
{
  "OpenAI": {
    "ApiKey": "your-api-key",
    "Model": "gpt-4o",
    "MaxTokens": 4000,
    "Temperature": 0.7
  },
  "MCP": {
    "Servers": {
      "weather": {
        "Type": "stdio",
        "Command": "dotnet",
        "Arguments": ["run", "--project", "../WeatherMcpServer"]
      },
      "calendar": {
        "Type": "http",
        "Endpoint": "https://api.calendar.example.com/mcp"
      }
    }
  }
}
```

#### Complete Server Hosting Example
```csharp
// WeatherMcpServer/Program.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Information;
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithResourcesFromAssembly()
    .WithToolsFromAssembly();

// Register business services
builder.Services.AddScoped<IWeatherService, WeatherService>();
builder.Services.AddHttpClient();

await builder.Build().RunAsync();

// Weather tools implementation
[McpServerToolType]
public class WeatherTools
{
    private readonly IWeatherService _weatherService;
    
    public WeatherTools(IWeatherService weatherService)
    {
        _weatherService = weatherService;
    }
    
    [McpServerTool]
    [Description("Gets current weather conditions for a city")]
    public async Task<WeatherData> GetCurrentWeather(
        [Description("City name")] string city,
        [Description("Country code (optional)")] string? country = null)
    {
        return await _weatherService.GetCurrentWeatherAsync(city, country);
    }
    
    [McpServerTool]
    [Description("Gets weather forecast for next 5 days")]
    public async Task<WeatherForecast> GetWeatherForecast(
        [Description("City name")] string city,
        [Description("Number of days (1-5)")] int days = 5)
    {
        return await _weatherService.GetForecastAsync(city, days);
    }
}
```

#### Request/Response Handling Example
```csharp
// Custom MCP request handler
public class ChatMcpHandler : IRequestHandler<ProcessChatWithMcpCommand, Result<ChatResponse>>
{
    private readonly IChatClient _chatClient;
    private readonly IMcpClientFactory _mcpFactory;
    private readonly IOptions<McpConfiguration> _mcpConfig;
    
    public ChatMcpHandler(
        IChatClient chatClient,
        IMcpClientFactory mcpFactory,
        IOptions<McpConfiguration> mcpConfig)
    {
        _chatClient = chatClient;
        _mcpFactory = mcpFactory;
        _mcpConfig = mcpConfig;
    }
    
    public async Task<Result<ChatResponse>> Handle(
        ProcessChatWithMcpCommand request,
        CancellationToken ct)
    {
        // Initialize MCP clients for enabled servers
        var mcpClients = new List<IMcpClient>();
        var allTools = new List<AIFunction>();
        
        try
        {
            foreach (var serverName in request.EnabledServers)
            {
                if (!_mcpConfig.Value.Servers.TryGetValue(serverName, out var serverConfig))
                    continue;
                    
                var transport = CreateTransport(serverConfig);
                var client = await _mcpFactory.CreateAsync(transport);
                mcpClients.Add(client);
                
                var tools = await client.ListToolsAsync();
                allTools.AddRange(tools.Cast<AIFunction>());
            }
            
            // Process chat with tools
            var chatOptions = new ChatOptions
            {
                Tools = allTools,
                MaxTokens = request.MaxTokens,
                Temperature = request.Temperature
            };
            
            var response = await _chatClient.CompleteAsync(
                request.Messages.Select(m => new ChatMessage(m.Role, m.Content)).ToArray(),
                chatOptions,
                ct);
            
            return Result.Success(new ChatResponse
            {
                Content = response.Message.Text,
                UsedTools = ExtractUsedTools(response),
                TokenUsage = response.Usage
            });
        }
        finally
        {
            // Clean up MCP clients
            foreach (var client in mcpClients)
            {
                await client.DisconnectAsync();
                client.Dispose();
            }
        }
    }
    
    private McpTransportOptions CreateTransport(McpServerConfig config)
    {
        return config.Type.ToLower() switch
        {
            "stdio" => new StdioClientTransportOptions
            {
                Command = config.Command,
                Arguments = config.Arguments
            },
            "http" => new HttpMcpClientTransportOptions
            {
                Endpoint = new Uri(config.Endpoint!),
                Headers = config.Headers
            },
            _ => throw new ArgumentException($"Unsupported transport type: {config.Type}")
        };
    }
}
```

## Applicability Assessment

### Apply to Axon Backend: HIGH CONFIDENCE

**Reasons:**
1. **Architecture Alignment**: The MCP pattern fits perfectly with Axon's modular monolith approach, allowing each module to expose tools while maintaining boundaries
2. **Clean Architecture Integration**: MCP clients can be properly abstracted in the Application layer, with implementations in Infrastructure
3. **CQRS Compatibility**: MCP operations can be wrapped in Commands/Queries using MediatR
4. **Modern .NET Support**: Full compatibility with .NET 10 and modern C# features

### Specific Integration Points for Axon Backend:

1. **Module-Specific MCP Servers**: Each module (Chat, User, etc.) can host its own MCP server
2. **Cross-Module Communication**: Modules can communicate via MCP instead of direct dependencies
3. **External Integration**: Third-party services can be integrated via MCP for loose coupling
4. **Testing**: MCP interfaces provide excellent seams for testing with mock servers

### Implementation Recommendations:

1. **Start Small**: Begin with a single MCP server for the Chat module
2. **Use Microsoft.Extensions.AI**: Leverage the abstraction layer for better testability
3. **Configuration-Driven**: Make MCP server connections configurable per environment
4. **Proper Error Handling**: Implement circuit breakers and retry policies for MCP calls
5. **Monitoring**: Add observability for MCP operations using OpenTelemetry

## Confidence Level: HIGH

**Supporting Evidence:**
- Comprehensive official documentation from Microsoft and MCP consortium
- Active development and Microsoft partnership
- Clear integration patterns with existing .NET ecosystem
- Successful examples in production scenarios
- Strong alignment with Axon Backend's architectural principles

**Caveats:**
- MCP C# SDK is still in preview (0.3.0-preview.3)
- Potential breaking changes before stable release
- Limited community examples compared to Python/TypeScript

**Next Steps:**
1. Implement a proof-of-concept MCP server for the Chat module
2. Create integration patterns documentation
3. Establish monitoring and error handling standards
4. Plan migration strategy for existing tool integrations

## Primary Source Links
- OpenAI .NET SDK: https://github.com/openai/openai-dotnet
- MCP C# SDK: https://github.com/modelcontextprotocol/csharp-sdk
- Microsoft MCP Documentation: https://learn.microsoft.com/en-us/dotnet/ai/get-started-mcp
- MCP Protocol Specification: https://modelcontextprotocol.io/introduction