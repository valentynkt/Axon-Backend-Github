# FastEndpoints 7.0.1 Implementation Guide for Axon Backend

## Overview

FastEndpoints is a lightweight REST API development framework for ASP.NET 8+ that implements the REPR (Request-Endpoint-Response) pattern. It provides a clean alternative to both Minimal APIs and MVC Controllers with performance comparable to Minimal APIs and notably better than MVC Controllers.

## .NET 10 Compatibility

✅ **Confirmed Compatible with .NET 10**
- FastEndpoints 7.0.1 officially supports .NET 8.0, 9.0, and 10.0
- Released: July 24, 2025
- License: MIT
- Compatible with multiple platforms including Android, iOS, macOS, Windows, and browser

## Installation

### Required Packages

```xml
<PackageReference Include="FastEndpoints" Version="7.0.1" />
<PackageReference Include="FastEndpoints.Swagger" Version="7.0.0" />
```

### Dependencies Included
- FastEndpoints.Attributes (>= 7.0.1)
- FastEndpoints.Messaging.Core (>= 7.0.1)
- FluentValidation (>= 12.0.0) - Built-in integration

## Program.cs Configuration

```csharp
using FastEndpoints;
using FastEndpoints.Swagger;

var builder = WebApplication.CreateBuilder(args);

// Add FastEndpoints services
builder.Services
    .AddFastEndpoints()
    .SwaggerDocument(o =>
    {
        o.DocumentSettings = s =>
        {
            s.DocumentName = "Axon-Backend-API";
            s.Title = "Axon Backend Web API";
            s.Version = "v1.0";
            
            // JWT Bearer Authentication
            s.AddAuth("Bearer", new()
            {
                Type = OpenApiSecuritySchemeType.Http,
                Scheme = JwtBearerDefaults.AuthenticationScheme,
                BearerFormat = "JWT"
            });
            
            // API Key Authentication (if needed)
            s.AddAuth("ApiKey", new()
            {
                Name = "X-API-Key",
                In = OpenApiSecurityApiKeyLocation.Header,
                Type = OpenApiSecuritySchemeType.ApiKey
            });
        };
    });

var app = builder.Build();

// Configure pipeline
app.UseFastEndpoints()
   .UseSwaggerGen();

app.Run();
```

## REPR Pattern Implementation

### 1. Request DTOs

```csharp
namespace Axon.Api.Contracts.Chat;

/// <summary>
/// Request to send a message in a conversation
/// </summary>
public sealed record SendMessageRequest
{
    public Guid ConversationId { get; init; }
    public string Message { get; init; } = string.Empty;
}
```

### 2. Response DTOs

```csharp
namespace Axon.Api.Contracts.Chat;

/// <summary>
/// Response containing the AI-generated message
/// </summary>
public sealed record SendMessageResponse
{
    public Guid MessageId { get; init; }
    public string Content { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public string Status { get; init; } = string.Empty;
}
```

### 3. Endpoint Implementation

```csharp
namespace Axon.Api.Endpoints.Chat;

/// <summary>
/// Endpoint for sending messages to AI chat system
/// </summary>
public sealed class SendMessageEndpoint : Endpoint<SendMessageRequest, SendMessageResponse>
{
    private readonly IMediator _mediator;
    
    public SendMessageEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    public override void Configure()
    {
        Post("/api/chat/conversations/{conversationId}/messages");
        
        // Security Configuration
        Claims("UserID");
        Roles("User", "Admin");
        
        // OpenAPI Documentation
        Summary(s =>
        {
            s.Summary = "Send a message to the AI chat system";
            s.Description = "Processes a user message and returns AI response";
            s.Responses[200] = "Message processed successfully";
            s.Responses[400] = "Invalid request data";
            s.Responses[401] = "Unauthorized";
            s.Responses[404] = "Conversation not found";
        });
        
        // Tags for API organization
        Tags("Chat", "Messages");
    }
    
    public override async Task HandleAsync(SendMessageRequest req, CancellationToken ct)
    {
        // Map to MediatR command (Clean Architecture boundary)
        var command = new ProcessMessageCommand(
            ConversationId: req.ConversationId,
            Message: req.Message,
            UserId: User.GetUserId()
        );
        
        var result = await _mediator.Send(command, ct);
        
        if (result.IsFailure)
        {
            await SendErrorsAsync(ct);
            return;
        }
        
        await SendOkAsync(new SendMessageResponse
        {
            MessageId = result.Value.MessageId,
            Content = result.Value.Content,
            Timestamp = result.Value.Timestamp,
            Status = "Completed"
        }, ct);
    }
}
```

## Validation Integration

### FluentValidation Setup

```csharp
namespace Axon.Api.Endpoints.Chat;

/// <summary>
/// Validator for SendMessageRequest using FluentValidation
/// </summary>
public sealed class SendMessageValidator : Validator<SendMessageRequest>
{
    public SendMessageValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty()
            .WithMessage("ConversationId is required");
            
        RuleFor(x => x.Message)
            .NotEmpty()
            .WithMessage("Message cannot be empty")
            .MaximumLength(4000)
            .WithMessage("Message cannot exceed 4000 characters");
    }
}
```

### Validation in Endpoint

```csharp
public override async Task HandleAsync(SendMessageRequest req, CancellationToken ct)
{
    // Validation is automatically applied by FastEndpoints
    // If validation fails, BadRequest is returned automatically
    
    // Continue with business logic...
}
```

## Vertical Slice Architecture Integration

### Recommended Folder Structure

```
src/Api/Endpoints/
├── Chat/
│   ├── SendMessage/
│   │   ├── SendMessageEndpoint.cs
│   │   ├── SendMessageValidator.cs
│   │   └── SendMessageModels.cs (if complex DTOs)
│   ├── GetConversations/
│   │   ├── GetConversationsEndpoint.cs
│   │   └── GetConversationsValidator.cs
│   └── CreateConversation/
│       ├── CreateConversationEndpoint.cs
│       └── CreateConversationValidator.cs
```

### Vertical Slice Example

```csharp
namespace Axon.Api.Endpoints.Chat.CreateConversation;

// Request/Response in same file for cohesion
public sealed record CreateConversationRequest
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public sealed record CreateConversationResponse
{
    public Guid ConversationId { get; init; }
    public string Title { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

// Validator
public sealed class CreateConversationValidator : Validator<CreateConversationRequest>
{
    public CreateConversationValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);
    }
}

// Endpoint
public sealed class CreateConversationEndpoint : Endpoint<CreateConversationRequest, CreateConversationResponse>
{
    private readonly IMediator _mediator;
    
    public CreateConversationEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    public override void Configure()
    {
        Post("/api/chat/conversations");
        Claims("UserID");
        
        Summary(s =>
        {
            s.Summary = "Create a new chat conversation";
            s.Description = "Creates a new conversation for the authenticated user";
        });
        
        Tags("Chat", "Conversations");
    }
    
    public override async Task HandleAsync(CreateConversationRequest req, CancellationToken ct)
    {
        var command = new CreateConversationCommand(
            Title: req.Title,
            Description: req.Description,
            UserId: User.GetUserId()
        );
        
        var result = await _mediator.Send(command, ct);
        
        if (result.IsFailure)
        {
            await SendErrorsAsync(ct);
            return;
        }
        
        await SendCreatedAtAsync(
            $"/api/chat/conversations/{result.Value.Id}",
            new CreateConversationResponse
            {
                ConversationId = result.Value.Id,
                Title = result.Value.Title,
                CreatedAt = result.Value.CreatedAt
            },
            ct);
    }
}
```

## Clean Architecture Integration

### Boundary Management

FastEndpoints serves as the **Presentation Layer** in Clean Architecture:

```csharp
// ✅ ALLOWED: Api → Application (via MediatR)
namespace Axon.Api.Endpoints.Chat;

public sealed class ChatEndpoint : Endpoint<ChatRequest, ChatResponse>
{
    private readonly IMediator _mediator; // Application layer boundary
    
    public override async Task HandleAsync(ChatRequest req, CancellationToken ct)
    {
        // Map API contract to Application command
        var command = new ProcessChatCommand(req.Message, req.UserId);
        var result = await _mediator.Send(command, ct);
        
        // Map Application result to API response
        await SendOkAsync(result.ToResponse(), ct);
    }
}
```

### Dependency Flow

```
FastEndpoints (Api) → MediatR → Application → Domain
                                     ↓
                               Infrastructure
```

## Authentication & Authorization

### JWT Bearer Configuration

```csharp
// In Program.cs
builder.Services
    .AddAuthenticationJwtBearer(s => 
    {
        s.SigningKey = builder.Configuration["JWT:Secret"]!;
        s.TokenValidationParameters = tvp =>
        {
            tvp.ClockSkew = TimeSpan.Zero;
            tvp.ValidateLifetime = true;
        };
    })
    .AddAuthorization()
    .AddFastEndpoints();
```

### Endpoint Security

```csharp
public override void Configure()
{
    Post("/api/admin/users");
    
    // Multiple authorization options
    Claims("AdminID");                    // Require specific claim
    Roles("Admin", "SuperAdmin");        // Require specific roles
    Permissions("ManageUsers");           // Require specific permissions
    Policies("AdminOnly");               // Use custom policy
    
    // Or allow anonymous access
    AllowAnonymous();
}
```

### Custom Authorization

```csharp
public sealed class AdminOnlyEndpoint : Endpoint<AdminRequest, AdminResponse>
{
    public override void Configure()
    {
        Post("/api/admin/action");
        
        // Custom authorization logic
        PreProcessor<AdminAuthorizationProcessor>();
    }
}

public sealed class AdminAuthorizationProcessor : IPreProcessor<AdminRequest>
{
    public async Task PreProcessAsync(IPreProcessorContext<AdminRequest> context, CancellationToken ct)
    {
        var user = context.HttpContext.User;
        
        if (!user.IsInRole("Admin") || !user.HasClaim("AdminLevel", "High"))
        {
            await context.HttpContext.Response.SendForbiddenAsync(ct);
        }
    }
}
```

## Error Handling

### Result Pattern Integration

```csharp
public override async Task HandleAsync(ProcessMessageRequest req, CancellationToken ct)
{
    var command = new ProcessMessageCommand(req.Message, User.GetUserId());
    var result = await _mediator.Send(command, ct);
    
    if (result.IsFailure)
    {
        // Map domain errors to HTTP responses
        var errorResponse = result.Error.Type switch
        {
            ErrorType.Validation => SendErrorsAsync(400, result.Error.Message, ct),
            ErrorType.NotFound => SendErrorsAsync(404, result.Error.Message, ct),
            ErrorType.Unauthorized => SendUnauthorizedAsync(ct),
            ErrorType.Forbidden => SendForbiddenAsync(ct),
            _ => SendErrorsAsync(500, "An unexpected error occurred", ct)
        };
        
        await errorResponse;
        return;
    }
    
    await SendOkAsync(result.Value.ToResponse(), ct);
}
```

### Global Error Handler

```csharp
// In Program.cs
app.UseExceptionHandler(builder =>
{
    builder.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        
        var response = exception switch
        {
            ValidationException => new { error = "Validation failed", details = exception.Message },
            UnauthorizedAccessException => new { error = "Unauthorized access" },
            _ => new { error = "An unexpected error occurred" }
        };
        
        context.Response.StatusCode = exception switch
        {
            ValidationException => 400,
            UnauthorizedAccessException => 401,
            _ => 500
        };
        
        await context.Response.WriteAsJsonAsync(response);
    });
});
```

## OpenAPI & Swagger Configuration

### Advanced Swagger Setup

```csharp
builder.Services.SwaggerDocument(o =>
{
    o.DocumentSettings = s =>
    {
        s.DocumentName = "Axon-Backend-API";
        s.Title = "Axon Backend Web API";
        s.Version = "v1.0";
        s.Description = "Modular monolith API using Clean Architecture + DDD + CQRS";
        
        // Security schemes
        s.AddAuth("Bearer", new()
        {
            Type = OpenApiSecuritySchemeType.Http,
            Scheme = JwtBearerDefaults.AuthenticationScheme,
            BearerFormat = "JWT",
            Description = "Enter JWT Bearer token"
        });
        
        // Global security requirement
        s.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("Bearer"));
        
        // XML documentation
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            s.GenerateXmlObjects = true;
        }
    };
    
    // Endpoint filtering
    o.EndpointFilter = ep => !ep.EndpointTags?.Contains("Internal") == true;
});
```

### Endpoint Documentation

```csharp
public override void Configure()
{
    Post("/api/chat/conversations/{conversationId}/messages");
    
    Summary(s =>
    {
        s.Summary = "Send a message to the AI chat system";
        s.Description = """
            Processes a user message through the AI chat system and returns the AI's response.
            The conversation must exist and the user must have access to it.
            """;
        s.ExampleRequest = new SendMessageRequest
        {
            ConversationId = Guid.NewGuid(),
            Message = "Hello, how can you help me today?"
        };
        s.ResponseExamples[200] = new SendMessageResponse
        {
            MessageId = Guid.NewGuid(),
            Content = "Hello! I'm here to help you with any questions you might have.",
            Timestamp = DateTime.UtcNow,
            Status = "Completed"
        };
        s.Responses[400] = "Invalid request: message is empty or conversation ID is invalid";
        s.Responses[401] = "Unauthorized: valid JWT token required";
        s.Responses[404] = "Conversation not found or user doesn't have access";
        s.Responses[429] = "Rate limit exceeded";
    });
    
    Tags("Chat", "Messages");
}
```

## Performance Characteristics

### Benchmarks vs Other Approaches

According to official documentation and benchmarks:

- **FastEndpoints**: Performance on par with Minimal APIs
- **Minimal APIs**: Fastest option
- **MVC Controllers**: Noticeably slower than FastEndpoints
- **Memory allocation**: Lower than MVC, comparable to Minimal APIs

### Optimization Tips

```csharp
// ✅ Use record types for DTOs (value semantics, immutable)
public sealed record ChatRequest(Guid ConversationId, string Message);

// ✅ Configure JSON serialization for performance
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// ✅ Use async/await consistently
public override async Task HandleAsync(ChatRequest req, CancellationToken ct)
{
    var result = await _mediator.Send(command, ct);
    await SendOkAsync(response, ct);
}

// ✅ Leverage cancellation tokens
public override async Task HandleAsync(ChatRequest req, CancellationToken ct)
{
    // All async operations should accept cancellation token
    var result = await _service.ProcessAsync(req.Message, ct);
}
```

## Testing FastEndpoints

### Integration Testing

```csharp
namespace Axon.Api.Tests.Integration.Chat;

public class SendMessageEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    
    public SendMessageEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }
    
    [Fact]
    public async Task SendMessage_WithValidRequest_ReturnsOkResponse()
    {
        // Arrange
        var request = new SendMessageRequest
        {
            ConversationId = Guid.NewGuid(),
            Message = "Test message"
        };
        
        var jsonContent = JsonContent.Create(request);
        
        // Act
        var response = await _client.PostAsync("/api/chat/conversations/123/messages", jsonContent);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<SendMessageResponse>();
        content.Should().NotBeNull();
        content!.Content.Should().NotBeEmpty();
    }
    
    [Fact]
    public async Task SendMessage_WithEmptyMessage_ReturnsBadRequest()
    {
        // Arrange
        var request = new SendMessageRequest
        {
            ConversationId = Guid.NewGuid(),
            Message = ""
        };
        
        var jsonContent = JsonContent.Create(request);
        
        // Act
        var response = await _client.PostAsync("/api/chat/conversations/123/messages", jsonContent);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
```

### Unit Testing Endpoints

```csharp
namespace Axon.Api.Tests.Unit.Chat;

public class SendMessageEndpointTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly SendMessageEndpoint _endpoint;
    
    public SendMessageEndpointTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _endpoint = new SendMessageEndpoint(_mediatorMock.Object);
    }
    
    [Fact]
    public async Task HandleAsync_WithValidRequest_CallsMediatorWithCorrectCommand()
    {
        // Arrange
        var request = new SendMessageRequest
        {
            ConversationId = Guid.NewGuid(),
            Message = "Test message"
        };
        
        var expectedResult = Result.Success(new ProcessMessageResult
        {
            MessageId = Guid.NewGuid(),
            Content = "AI response",
            Timestamp = DateTime.UtcNow
        });
        
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<ProcessMessageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);
        
        // Act
        await _endpoint.HandleAsync(request, CancellationToken.None);
        
        // Assert
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<ProcessMessageCommand>(cmd => 
                    cmd.ConversationId == request.ConversationId &&
                    cmd.Message == request.Message),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
```

## Migration from MVC Controllers

### Before (MVC Controller)

```csharp
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IMediator _mediator;
    
    public ChatController(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    [HttpPost("conversations/{conversationId}/messages")]
    [Authorize]
    public async Task<ActionResult<SendMessageResponse>> SendMessage(
        Guid conversationId, 
        [FromBody] SendMessageRequest request)
    {
        var command = new ProcessMessageCommand(conversationId, request.Message, User.GetUserId());
        var result = await _mediator.Send(command);
        
        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }
        
        return Ok(new SendMessageResponse 
        { 
            MessageId = result.Value.MessageId,
            Content = result.Value.Content,
            Timestamp = result.Value.Timestamp,
            Status = "Completed"
        });
    }
}
```

### After (FastEndpoints)

```csharp
public sealed class SendMessageEndpoint : Endpoint<SendMessageRequest, SendMessageResponse>
{
    private readonly IMediator _mediator;
    
    public SendMessageEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    public override void Configure()
    {
        Post("/api/chat/conversations/{conversationId}/messages");
        Claims("UserID");
        
        Summary(s =>
        {
            s.Summary = "Send a message to the AI chat system";
            s.Description = "Processes a user message and returns AI response";
        });
        
        Tags("Chat", "Messages");
    }
    
    public override async Task HandleAsync(SendMessageRequest req, CancellationToken ct)
    {
        var command = new ProcessMessageCommand(req.ConversationId, req.Message, User.GetUserId());
        var result = await _mediator.Send(command, ct);
        
        if (result.IsFailure)
        {
            await SendErrorsAsync(ct);
            return;
        }
        
        await SendOkAsync(new SendMessageResponse
        {
            MessageId = result.Value.MessageId,
            Content = result.Value.Content,
            Timestamp = result.Value.Timestamp,
            Status = "Completed"
        }, ct);
    }
}
```

### Key Differences

1. **Separation of Concerns**: Each endpoint is its own class
2. **Built-in Validation**: Automatic FluentValidation integration
3. **Type Safety**: Strong typing for requests/responses
4. **Performance**: Better performance than MVC controllers
5. **Documentation**: Built-in OpenAPI support
6. **Testing**: Easier to unit test individual endpoints
7. **Vertical Slices**: Natural fit for feature-based organization

## Best Practices

### 1. Endpoint Organization

```csharp
// ✅ DO: Organize by feature/vertical slice
src/Api/Endpoints/
├── Chat/
│   ├── SendMessage/
│   ├── GetConversations/
│   └── CreateConversation/
├── Users/
│   ├── Register/
│   ├── Login/
│   └── GetProfile/
```

### 2. Request/Response Design

```csharp
// ✅ DO: Use record types for immutability
public sealed record CreateUserRequest(
    string Email,
    string FirstName,
    string LastName);

// ✅ DO: Include all necessary data
public sealed record CreateUserResponse(
    Guid UserId,
    string Email,
    string FullName,
    DateTime CreatedAt);

// ❌ DON'T: Expose domain entities directly
public sealed record BadResponse(User User); // Leaks domain
```

### 3. Validation

```csharp
// ✅ DO: Validate at the API boundary
public sealed class CreateUserValidator : Validator<CreateUserRequest>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);
            
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(50);
            
        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(50);
    }
}
```

### 4. Security

```csharp
// ✅ DO: Be explicit about security requirements
public override void Configure()
{
    Post("/api/admin/users");
    
    // Explicit security requirements
    Claims("AdminID");
    Roles("Admin");
    Permissions("ManageUsers");
    
    // Clear documentation
    Summary(s => s.Summary = "Admin-only endpoint for user management");
}

// ✅ DO: Use HTTPS in production
// ✅ DO: Validate all input
// ✅ DO: Use proper authentication/authorization
```

### 5. Error Handling

```csharp
// ✅ DO: Use consistent error responses
public override async Task HandleAsync(CreateUserRequest req, CancellationToken ct)
{
    var result = await _mediator.Send(command, ct);
    
    if (result.IsFailure)
    {
        var statusCode = result.Error.Type switch
        {
            ErrorType.Validation => 400,
            ErrorType.NotFound => 404,
            ErrorType.Conflict => 409,
            _ => 500
        };
        
        await SendErrorsAsync(statusCode, result.Error.Message, ct);
        return;
    }
    
    await SendCreatedAtAsync($"/api/users/{result.Value.Id}", result.Value.ToResponse(), ct);
}
```

## Conclusion

FastEndpoints 7.0.1 provides an excellent foundation for implementing the REPR pattern in the Axon Backend project with:

- ✅ Full .NET 10 compatibility
- ✅ Clean Architecture boundary management
- ✅ Vertical slice architecture support
- ✅ Built-in FluentValidation integration
- ✅ Comprehensive OpenAPI/Swagger support
- ✅ Performance on par with Minimal APIs
- ✅ Strong typing and compile-time safety
- ✅ Easy testing and maintainability

The framework aligns perfectly with the project's goals of shipping fast via vertical slices while maintaining clean architectural boundaries and seams for potential microservice extraction.

## Additional Resources

- [Official Documentation](https://fast-endpoints.com/)
- [GitHub Repository](https://github.com/FastEndpoints/FastEndpoints)
- [NuGet Package](https://www.nuget.org/packages/FastEndpoints)
- [Swagger Support Documentation](https://fast-endpoints.com/docs/swagger-support)
- [Security Documentation](https://fast-endpoints.com/docs/security)