# MediatR 13.0.0 Implementation Guide for Axon Backend

## Overview

This guide covers the implementation of MediatR 13.0.0 in the Axon Backend project, focusing on .NET 10 compatibility, CQRS patterns, Clean Architecture integration, and commercial licensing requirements.

## Research Summary

**Research Question**: Document MediatR 13.0.0 for .NET 10 compatibility and Clean Architecture CQRS implementation in Axon Backend.

**Primary Sources**:
- NuGet Gallery: [MediatR 13.0.0](https://www.nuget.org/packages/MediatR)
- GitHub Repository: [jbogard/MediatR Releases](https://github.com/jbogard/MediatR/releases)
- Official Documentation: Multiple CQRS implementation guides
- .NET 10 Compatibility Research: Previous memory analysis confirmed compatibility

**Confidence Level**: HIGH - Primary sources confirm .NET 10 compatibility and implementation patterns

## Version 13.0.0 Key Changes

### Major Breaking Changes
1. **Commercial Licensing Required**: MediatR moved from Apache license to dual commercial/OSS license
2. **License Key Configuration**: Required during service registration
3. **Framework Support**: Added .NET Standard 2.0 support while maintaining .NET 10 compatibility

### License Configuration
```csharp
services.AddMediatR(cfg => 
{
    cfg.LicenseKey = "<Your License Key>";
    cfg.RegisterServicesFromAssemblyContaining<Program>();
});
```

**License Acquisition**: Register at [MediatR.io](https://mediatr.io) for license key

## Installation & Configuration

### Package Installation
```xml
<!-- Main MediatR package (includes DI extensions) -->
<PackageReference Include="MediatR" Version="13.0.0" />

<!-- Optional: Contracts package for shared interfaces -->
<PackageReference Include="MediatR.Contracts" Version="2.0.1" />
```

### Dependency Injection Setup
```csharp
// Program.cs or Startup.cs
public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    services.AddMediatR(cfg =>
    {
        // REQUIRED: Set license key
        cfg.LicenseKey = configuration["MediatR:LicenseKey"];
        
        // Register handlers from assemblies
        cfg.RegisterServicesFromAssemblyContaining<Program>();
        cfg.RegisterServicesFromAssemblies(
            typeof(ChatModule.Application.AssemblyMarker).Assembly
            // Add other module assemblies
        );
        
        // Pipeline behaviors (cross-cutting concerns)
        cfg.AddBehavior<ValidationBehavior>();
        cfg.AddBehavior<LoggingBehavior>();
        cfg.AddBehavior<PerformanceBehavior>();
    });
}
```

## CQRS Implementation Patterns

### Command Pattern
```csharp
// Command Definition
public record ProcessChatMessageCommand(
    string Message,
    Guid UserId,
    Guid ConversationId
) : IRequest<Result<ChatMessageDto>>;

// Command Handler
public sealed class ProcessChatMessageHandler 
    : IRequestHandler<ProcessChatMessageCommand, Result<ChatMessageDto>>
{
    private readonly IChatRepository _repository;
    private readonly IAiClient _aiClient;
    private readonly ILogger<ProcessChatMessageHandler> _logger;

    public ProcessChatMessageHandler(
        IChatRepository repository,
        IAiClient aiClient,
        ILogger<ProcessChatMessageHandler> logger)
    {
        _repository = repository;
        _aiClient = aiClient;
        _logger = logger;
    }

    public async Task<Result<ChatMessageDto>> Handle(
        ProcessChatMessageCommand request, 
        CancellationToken cancellationToken)
    {
        // Load aggregate
        var conversation = await _repository.GetByIdAsync(
            ConversationId.Create(request.ConversationId).Value, 
            cancellationToken);
            
        if (conversation is null)
            return Error.NotFound("Conversation not found");

        // Domain logic
        var addMessageResult = conversation.AddMessage(
            request.Message, 
            UserId.Create(request.UserId).Value);
            
        if (addMessageResult.IsFailure)
            return addMessageResult.Error;

        // AI processing
        var aiResponse = await _aiClient.ProcessAsync(
            request.Message, 
            cancellationToken);
            
        if (aiResponse.IsFailure)
            return aiResponse.Error;

        // Save changes
        await _repository.SaveAsync(conversation, cancellationToken);

        return new ChatMessageDto(
            conversation.Messages.Last().Id.Value,
            request.Message,
            aiResponse.Value,
            DateTime.UtcNow);
    }
}
```

### Query Pattern
```csharp
// Query Definition
public record GetConversationHistoryQuery(
    Guid ConversationId,
    int PageSize = 20,
    int PageNumber = 1
) : IRequest<Result<ConversationHistoryDto>>;

// Query Handler
public sealed class GetConversationHistoryHandler 
    : IRequestHandler<GetConversationHistoryQuery, Result<ConversationHistoryDto>>
{
    private readonly IConversationReadRepository _readRepository;

    public GetConversationHistoryHandler(IConversationReadRepository readRepository)
    {
        _readRepository = readRepository;
    }

    public async Task<Result<ConversationHistoryDto>> Handle(
        GetConversationHistoryQuery request, 
        CancellationToken cancellationToken)
    {
        var conversationId = ConversationId.Create(request.ConversationId);
        if (conversationId.IsFailure)
            return conversationId.Error;

        var history = await _readRepository.GetHistoryAsync(
            conversationId.Value,
            request.PageSize,
            request.PageNumber,
            cancellationToken);

        return history is null 
            ? Error.NotFound("Conversation not found")
            : new ConversationHistoryDto(
                history.Id,
                history.Messages.Select(m => new MessageDto(m.Id, m.Content, m.Timestamp)).ToList(),
                history.TotalMessages);
    }
}
```

### Abstract Base Classes (Axon-Specific)
```csharp
// Create base abstractions for cleaner separation
public abstract record Command : IRequest<Result>;
public abstract record Command<TResponse> : IRequest<Result<TResponse>>;
public abstract record Query<TResponse> : IRequest<Result<TResponse>>;

// Usage examples
public record CreateConversationCommand(string Title, Guid UserId) : Command<ConversationDto>;
public record GetUserConversationsQuery(Guid UserId) : Query<List<ConversationSummaryDto>>;
```

## Pipeline Behaviors (Cross-Cutting Concerns)

### Validation Behavior
```csharp
public sealed class ValidationBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        
        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .Where(r => !r.IsValid)
            .SelectMany(r => r.Errors)
            .ToArray();

        if (failures.Any())
        {
            // For Result<T> pattern integration
            if (typeof(TResponse).IsGenericType && 
                typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
            {
                var errorResult = Error.Validation(
                    "Validation failed", 
                    failures.ToDictionary(f => f.PropertyName, f => f.ErrorMessage));
                    
                return (TResponse)(object)Result<object>.Failure(errorResult);
            }
            
            throw new ValidationException(failures);
        }

        return await next();
    }
}
```

### Logging Behavior
```csharp
public sealed class LoggingBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var requestId = Guid.NewGuid();

        _logger.LogInformation(
            "Starting request {RequestName} with ID {RequestId}: {@Request}",
            requestName, requestId, request);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next();
            
            stopwatch.Stop();
            
            _logger.LogInformation(
                "Completed request {RequestName} with ID {RequestId} in {ElapsedMs}ms",
                requestName, requestId, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex,
                "Request {RequestName} with ID {RequestId} failed after {ElapsedMs}ms",
                requestName, requestId, stopwatch.ElapsedMilliseconds);
                
            throw;
        }
    }
}
```

### Performance Behavior
```csharp
public sealed class PerformanceBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;
    private readonly IMetrics _metrics;

    public PerformanceBehavior(
        ILogger<PerformanceBehavior<TRequest, TResponse>> logger,
        IMetrics metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        using var activity = Activity.StartActivity($"MediatR.{requestName}");
        
        try
        {
            var response = await next();
            
            stopwatch.Stop();
            
            // Log slow requests
            if (stopwatch.ElapsedMilliseconds > 500)
            {
                _logger.LogWarning(
                    "Slow request detected: {RequestName} took {ElapsedMs}ms. Request: {@Request}",
                    requestName, stopwatch.ElapsedMilliseconds, request);
            }

            // Record metrics
            _metrics.RecordRequestDuration(requestName, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            _metrics.RecordRequestFailure(requestName, ex.GetType().Name);
            throw;
        }
    }
}
```

## Clean Architecture Integration

### Folder Structure
```
src/
├── Api/
│   ├── Endpoints/
│   │   └── Chat/
│   │       ├── ProcessMessageEndpoint.cs
│   │       └── GetConversationEndpoint.cs
│   └── Contracts/
│       └── Chat/
│           ├── ProcessMessageRequest.cs
│           └── ConversationResponse.cs
├── Modules/
│   └── Chat/
│       ├── Application/
│       │   ├── Commands/
│       │   │   └── ProcessMessage/
│       │   │       ├── ProcessMessageCommand.cs
│       │   │       ├── ProcessMessageHandler.cs
│       │   │       └── ProcessMessageValidator.cs
│       │   ├── Queries/
│       │   │   └── GetConversation/
│       │   │       ├── GetConversationQuery.cs
│       │   │       └── GetConversationHandler.cs
│       │   ├── Behaviors/
│       │   │   ├── ValidationBehavior.cs
│       │   │   ├── LoggingBehavior.cs
│       │   │   └── PerformanceBehavior.cs
│       │   └── Interfaces/
│       │       ├── IChatRepository.cs
│       │       └── IAiClient.cs
│       ├── Domain/
│       │   ├── Entities/
│       │   ├── ValueObjects/
│       │   └── Events/
│       └── Infrastructure/
│           ├── Repositories/
│           └── ExternalServices/
└── Shared/
    ├── Common/
    │   ├── Result.cs
    │   └── Error.cs
    └── Extensions/
        └── MediatRExtensions.cs
```

### API Integration with FastEndpoints
```csharp
public sealed class ProcessMessageEndpoint : Endpoint<ProcessMessageRequest, ProcessMessageResponse>
{
    private readonly ISender _sender;

    public ProcessMessageEndpoint(ISender sender)
    {
        _sender = sender;
    }

    public override void Configure()
    {
        Post("/api/chat/{conversationId}/messages");
        AllowAnonymous(); // Configure as needed
        Summary(s =>
        {
            s.Summary = "Process a chat message and get AI response";
            s.Description = "Sends a message to a conversation and returns AI-generated response";
        });
    }

    public override async Task HandleAsync(ProcessMessageRequest req, CancellationToken ct)
    {
        var command = new ProcessChatMessageCommand(
            req.Message,
            req.UserId,
            req.ConversationId);

        var result = await _sender.Send(command, ct);

        if (result.IsFailure)
        {
            await SendErrorsAsync(result.Error.ToValidationFailures(), ct);
            return;
        }

        var response = new ProcessMessageResponse
        {
            MessageId = result.Value.MessageId,
            Content = result.Value.Content,
            AiResponse = result.Value.AiResponse,
            Timestamp = result.Value.Timestamp
        };

        await SendOkAsync(response, ct);
    }
}
```

## Performance Considerations

### Best Practices
1. **Handler Scope**: Keep handlers focused and lightweight
2. **Async/Await**: Always use async patterns for I/O operations
3. **Cancellation Tokens**: Propagate cancellation tokens through the pipeline
4. **Memory Allocation**: Use records for commands/queries to reduce allocations
5. **Database Queries**: Optimize queries in read handlers, use projection when possible

### Monitoring & Metrics
```csharp
// Extension for metrics integration
public static class MediatRMetricsExtensions
{
    public static IServiceCollection AddMediatRMetrics(this IServiceCollection services)
    {
        services.AddSingleton<IMetrics, MediatRMetrics>();
        return services;
    }
}

public class MediatRMetrics : IMetrics
{
    private readonly ILogger<MediatRMetrics> _logger;
    private readonly Counter<long> _requestCounter;
    private readonly Histogram<double> _requestDuration;

    public MediatRMetrics(ILogger<MediatRMetrics> logger, IMeterFactory meterFactory)
    {
        _logger = logger;
        var meter = meterFactory.Create("Axon.MediatR");
        
        _requestCounter = meter.CreateCounter<long>(
            "mediatr_requests_total",
            description: "Total number of MediatR requests");
            
        _requestDuration = meter.CreateHistogram<double>(
            "mediatr_request_duration_seconds",
            description: "Duration of MediatR requests in seconds");
    }

    public void RecordRequestDuration(string requestName, long durationMs)
    {
        _requestCounter.Add(1, new("request_name", requestName), new("status", "success"));
        _requestDuration.Record(durationMs / 1000.0, new("request_name", requestName));
    }

    public void RecordRequestFailure(string requestName, string exceptionType)
    {
        _requestCounter.Add(1, 
            new("request_name", requestName), 
            new("status", "failure"),
            new("exception_type", exceptionType));
    }
}
```

## Error Handling Patterns

### Result Pattern Integration
```csharp
// Shared Result pattern for consistent error handling
public class Result<T>
{
    public bool IsSuccess { get; private set; }
    public bool IsFailure => !IsSuccess;
    public T Value { get; private set; }
    public Error Error { get; private set; }

    private Result(T value)
    {
        IsSuccess = true;
        Value = value;
        Error = Error.None;
    }

    private Result(Error error)
    {
        IsSuccess = false;
        Value = default;
        Error = error;
    }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
}

// Error types for domain modeling
public record Error(string Code, string Message, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);

    public static Error NotFound(string message) => 
        new("NOT_FOUND", message, ErrorType.NotFound);
        
    public static Error Validation(string message) => 
        new("VALIDATION", message, ErrorType.Validation);
        
    public static Error Conflict(string message) => 
        new("CONFLICT", message, ErrorType.Conflict);
}

public enum ErrorType
{
    None,
    NotFound,
    Validation,
    Conflict,
    Unauthorized,
    Internal
}
```

## Testing Strategies

### Unit Testing Handlers
```csharp
public sealed class ProcessChatMessageHandlerTests
{
    private readonly Mock<IChatRepository> _repositoryMock;
    private readonly Mock<IAiClient> _aiClientMock;
    private readonly Mock<ILogger<ProcessChatMessageHandler>> _loggerMock;
    private readonly ProcessChatMessageHandler _handler;

    public ProcessChatMessageHandlerTests()
    {
        _repositoryMock = new Mock<IChatRepository>();
        _aiClientMock = new Mock<IAiClient>();
        _loggerMock = new Mock<ILogger<ProcessChatMessageHandler>>();
        
        _handler = new ProcessChatMessageHandler(
            _repositoryMock.Object,
            _aiClientMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsSuccessResult()
    {
        // Arrange
        var command = new ProcessChatMessageCommand("Hello", Guid.NewGuid(), Guid.NewGuid());
        var conversation = CreateTestConversation();
        
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<ConversationId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);
            
        _aiClientMock
            .Setup(a => a.ProcessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("AI Response"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        _repositoryMock.Verify(r => r.SaveAsync(conversation, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ConversationNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var command = new ProcessChatMessageCommand("Hello", Guid.NewGuid(), Guid.NewGuid());
        
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<ConversationId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Conversation)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    private static Conversation CreateTestConversation()
    {
        return new Conversation(
            ConversationId.New(),
            "Test Conversation",
            UserId.New(),
            DateTime.UtcNow);
    }
}
```

### Integration Testing with Pipeline
```csharp
public class MediatRIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly IServiceScope _scope;
    private readonly ISender _sender;

    public MediatRIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _scope = _factory.Services.CreateScope();
        _sender = _scope.ServiceProvider.GetRequiredService<ISender>();
    }

    [Fact]
    public async Task SendCommand_WithValidationBehavior_ValidatesCorrectly()
    {
        // Arrange
        var invalidCommand = new ProcessChatMessageCommand("", Guid.Empty, Guid.Empty);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _sender.Send(invalidCommand));
            
        exception.Errors.Should().NotBeEmpty();
    }

    public void Dispose()
    {
        _scope?.Dispose();
    }
}
```

## Axon Backend Implementation Checklist

### ✅ Apply to Axon Backend
1. **Install MediatR 13.0.0** with license key configuration
2. **Implement base Command/Query abstractions** for cleaner separation
3. **Add pipeline behaviors** for validation, logging, and performance monitoring
4. **Integrate with FastEndpoints** using ISender interface
5. **Use Result pattern** for consistent error handling
6. **Structure handlers** following Clean Architecture module boundaries
7. **Add comprehensive testing** for handlers and pipeline behaviors

### 🔄 Configuration Steps
1. Add license key to configuration (appsettings.json/environment variables)
2. Register MediatR services in Program.cs with all module assemblies
3. Configure pipeline behaviors in dependency injection
4. Implement base abstractions in Shared project
5. Add validation using FluentValidation integration
6. Set up metrics and performance monitoring

### ⚠️ Important Considerations
1. **Commercial License**: Required for production use - obtain from MediatR.io
2. **Breaking Changes**: Review migration from any existing MediatR usage
3. **Performance**: Monitor handler execution times and optimize as needed
4. **Testing**: Ensure comprehensive coverage of command/query handlers
5. **Documentation**: Update API contracts and sequence diagrams

## Primary Source Links

- **NuGet Package**: https://www.nuget.org/packages/MediatR
- **GitHub Repository**: https://github.com/jbogard/MediatR
- **License Information**: https://mediatr.io
- **CQRS Best Practices**: Multiple authoritative implementation guides
- **.NET 10 Compatibility**: Confirmed through primary source research

---

**Document Status**: FINAL  
**Research Confidence**: HIGH  
**Apply to Axon Backend**: ✅ RECOMMENDED  
**Next Actions**: Install package, configure license key, implement base patterns