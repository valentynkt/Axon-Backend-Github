# 📚 Axon Backend - Coding Practices & Standards

## Table of Contents
- [Core Philosophy](#core-philosophy)
- [Language & Framework Standards](#language--framework-standards)
- [Code Organization](#code-organization)
- [Naming Conventions](#naming-conventions)
- [Domain-Driven Design Patterns](#domain-driven-design-patterns)
- [CQRS Implementation](#cqrs-implementation)
- [Repository & Data Access Patterns](#repository--data-access-patterns)
- [Unit of Work Pattern](#unit-of-work-pattern)
- [MediatR Pipeline Behaviors](#mediatr-pipeline-behaviors)
- [FastEndpoints Implementation](#fastendpoints-implementation)
- [Entity Framework Core Patterns](#entity-framework-core-patterns)
- [Building Blocks Architecture](#building-blocks-architecture)
- [Error Handling](#error-handling)
- [Validation Patterns](#validation-patterns)
- [Testing Standards](#testing-standards)
- [Logging & Observability](#logging--observability)
- [Dependency Injection](#dependency-injection)
- [Caching Strategies](#caching-strategies)
- [API Design & Versioning](#api-design--versioning)
- [Documentation Requirements](#documentation-requirements)
- [Performance Considerations](#performance-considerations)
- [Security Practices](#security-practices)
- [Migration & Database Management](#migration--database-management)

## Core Philosophy

### 1. Clean Architecture Principles
Our codebase follows Uncle Bob's Clean Architecture with strict separation of concerns:

- **Domain Layer**: Pure business logic with zero dependencies
- **Application Layer**: Use cases and application business rules
- **Infrastructure Layer**: External concerns (DB, APIs, frameworks)
- **API Layer**: Entry points and presentation concerns

### 2. SOLID Principles
Every component must adhere to:
- **S**ingle Responsibility: One reason to change
- **O**pen/Closed: Open for extension, closed for modification
- **L**iskov Substitution: Subtypes must be substitutable
- **I**nterface Segregation: Many specific interfaces over general ones
- **D**ependency Inversion: Depend on abstractions, not concretions

### 3. DRY & KISS
- **Don't Repeat Yourself**: Extract common patterns to Building Blocks
- **Keep It Simple**: Prefer clarity over cleverness
- **YAGNI**: Don't build features you don't need yet

## Language & Framework Standards

### C# 13 / .NET 10 Standards
```csharp
// ✅ Use file-scoped namespaces
namespace Axon.Modules.Chat.Domain;

// ✅ Use primary constructors for simple types
public sealed class ConversationId(Guid value) : IStrongId<Guid>
{
    public Guid Value { get; } = value;
}

// ✅ Use record types for immutable data
public record ProcessMessageCommand(
    string Message,
    Guid? ConversationId,
    long? UserId,
    Guid? PreviousResponseId
) : ICommand<ProcessMessageResponse>;

// ✅ Use pattern matching and switch expressions
public string GetStatus() => State switch
{
    ConversationState.Active => "Active",
    ConversationState.Archived => "Archived",
    ConversationState.Completed => "Completed",
    _ => throw new InvalidOperationException($"Unknown state: {State}")
};

// ✅ Use nullable reference types
public string? OptionalField { get; init; }

// ✅ Use init-only properties for immutability
public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

// ✅ Use collection expressions (C# 12+)
List<string> tags = ["important", "urgent", "review"];

// ✅ Use raw string literals for multi-line strings
var json = """
    {
        "name": "value",
        "nested": {
            "property": 123
        }
    }
    """;
```

### Async/Await Patterns
```csharp
// ✅ Always use async/await for I/O operations
public async Task<Result<ConversationDto>> GetConversationAsync(
    Guid id, 
    CancellationToken cancellationToken = default)
{
    // Always pass cancellation tokens
    var conversation = await _repository
        .GetByIdAsync(id, cancellationToken)
        .ConfigureAwait(false); // Use ConfigureAwait in library code
    
    return Result.Success(conversation.ToDto());
}

// ✅ Use ValueTask for hot paths
public ValueTask<bool> ExistsAsync(Guid id) 
    => _cache.ContainsKeyAsync(id);

// ❌ Avoid async void except for event handlers
public async void HandleEvent() // Only for event handlers!
{
    await ProcessAsync();
}

// ✅ Use IAsyncEnumerable for streaming data
public async IAsyncEnumerable<MessageDto> StreamMessagesAsync(
    [EnumeratorCancellation] CancellationToken ct = default)
{
    await foreach (var message in _repository.StreamAsync(ct))
    {
        yield return message.ToDto();
    }
}
```

## Code Organization

### Module Structure
Each module follows Vertical Slice Architecture within Clean Architecture:
```
src/Modules/[ModuleName]/
├── Domain/                 # Core business logic
│   ├── [Aggregate]/       # Aggregate roots
│   ├── Entities/          # Domain entities
│   ├── ValueObjects/      # Value objects
│   ├── Events/            # Domain events
│   ├── Errors/            # Domain errors
│   └── Services/          # Domain services
├── Application/           # Use cases
│   ├── Commands/          # Write operations (CQRS)
│   │   └── [Command]/
│   │       ├── [Command]Command.cs
│   │       ├── [Command]Handler.cs
│   │       ├── [Command]Validator.cs
│   │       └── [Command]Response.cs
│   ├── Queries/           # Read operations (CQRS)
│   ├── DTOs/              # Data transfer objects
│   ├── Abstractions/      # Application interfaces
│   ├── EventHandlers/     # Domain event handlers
│   └── Services/          # Application services
├── Infrastructure/        # External concerns
│   ├── Persistence/       # Database implementation
│   │   ├── Configurations/# EF Core configurations
│   │   ├── Repositories/  # Repository implementations
│   │   └── Migrations/    # Database migrations
│   ├── Services/          # External service implementations
│   └── Configuration/     # DI and module setup
└── Api/                   # Optional module-specific API
    └── Endpoints/         # FastEndpoints implementations
```

### File Organization Rules
1. **One class per file** (except nested types)
2. **File name matches class name**
3. **Folder structure mirrors namespace**
4. **Group related functionality** in folders
5. **Keep files under 500 lines** (prefer smaller)

## Naming Conventions

### General Rules
```csharp
// Classes, Records, Interfaces: PascalCase
public class ConversationService { }
public record MessageDto { }
public interface IUnitOfWork { }

// Methods, Properties: PascalCase
public async Task<Result> ProcessMessageAsync() { }
public string MessageContent { get; init; }

// Parameters, Local Variables: camelCase
public void ProcessMessage(string messageContent, int retryCount)
{
    var processedMessage = FormatMessage(messageContent);
}

// Private Fields: _camelCase with underscore
private readonly ILogger<Service> _logger;
private readonly IRepository _repository;

// Constants: UPPER_CASE or PascalCase
private const int MAX_RETRY_COUNT = 3;
private const string DefaultMessage = "Hello";

// Async Methods: End with Async suffix
public async Task<Result> SaveAsync() { }

// Interfaces: Start with 'I'
public interface IMessageService { }

// Generic Type Parameters: T prefix
public class Repository<TEntity, TId> { }

// Boolean Properties/Methods: Use Is/Has/Can prefixes
public bool IsActive { get; }
public bool HasMessages() { }
public bool CanProcess() { }
```

### Domain-Specific Naming
```csharp
// Aggregates: Noun (singular)
public class Conversation { }

// Value Objects: Descriptive noun
public record MessageContent { }
public record ConversationId { }

// Domain Events: Past tense verb + "DomainEvent"
public record MessageAddedDomainEvent { }
public record ConversationStartedDomainEvent { }

// Commands: Verb + Noun + "Command"
public record ProcessMessageCommand { }
public record StartConversationCommand { }

// Queries: "Get" + Noun + "Query"
public record GetConversationQuery { }
public record SearchConversationsQuery { }

// Handlers: Command/Query name + "Handler"
public class ProcessMessageHandler { }
public class GetConversationHandler { }

// Validators: Type name + "Validator"
public class ProcessMessageValidator { }

// Repositories: Entity name + "Repository"
public interface IConversationRepository { }

// Specifications: Descriptive name + "Specification"
public class ActiveConversationsSpecification { }

// Configurations: Entity name + "Configuration"
public class ConversationConfiguration : IEntityTypeConfiguration<Conversation> { }
```

## Domain-Driven Design Patterns

### Aggregate Design
```csharp
// Aggregates inherit from BaseAggregate<TId>
public sealed class Conversation : BaseAggregate<ConversationId>
{
    private readonly List<Message> _messages = new();
    
    // Private constructor for EF
    private Conversation() { }
    
    // Factory method for creation
    public static Result<Conversation> Create(
        UserId userId,
        string title)
    {
        // Business rule validation
        if (string.IsNullOrWhiteSpace(title))
            return Result.Failure<Conversation>(ChatErrors.InvalidTitle);
            
        var conversation = new Conversation
        {
            Id = ConversationId.New(),
            UserId = userId,
            Title = title,
            State = ConversationState.Active,
            CreatedAt = DateTime.UtcNow
        };
        
        // Raise domain event
        conversation.AddDomainEvent(
            new ConversationStartedDomainEvent(conversation.Id));
            
        return Result.Success(conversation);
    }
    
    // Business operations
    public Result AddMessage(string content, UserId userId)
    {
        // Enforce invariants
        if (State != ConversationState.Active)
            return Result.Failure(ChatErrors.ConversationNotActive);
            
        var message = Message.Create(content, userId);
        _messages.Add(message);
        
        AddDomainEvent(new MessageAddedDomainEvent(Id, message.Id));
        
        return Result.Success();
    }
}
```

### Value Objects
```csharp
// Use records for immutable value objects
public sealed record MessageContent
{
    public string Value { get; }
    
    private MessageContent(string value) => Value = value;
    
    public static Result<MessageContent> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result.Failure<MessageContent>(ChatErrors.EmptyContent);
            
        if (value.Length > 4000)
            return Result.Failure<MessageContent>(ChatErrors.ContentTooLong);
            
        return Result.Success(new MessageContent(value));
    }
    
    // Implicit conversion for convenience
    public static implicit operator string(MessageContent content) 
        => content.Value;
}
```

### Strong Type IDs
```csharp
// All entity IDs use strong typing
public sealed class ConversationId : IStrongId<Guid>
{
    public Guid Value { get; }
    
    public ConversationId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("ConversationId cannot be empty");
        Value = value;
    }
    
    public static ConversationId New() => new(Guid.NewGuid());
    
    // Equality and conversion operators
    public static implicit operator Guid(ConversationId id) => id.Value;
    public static explicit operator ConversationId(Guid value) => new(value);
}
```

### Domain Events
```csharp
// Domain events are immutable records
public sealed record ConversationStartedDomainEvent(
    ConversationId ConversationId,
    UserId UserId,
    DateTime OccurredAt = default
) : IDomainEvent
{
    public DateTime OccurredAt { get; init; } = 
        OccurredAt == default ? DateTime.UtcNow : OccurredAt;
}

// Handle in application layer
public class ConversationStartedDomainEventHandler 
    : INotificationHandler<ConversationStartedDomainEvent>
{
    public async Task Handle(
        ConversationStartedDomainEvent notification,
        CancellationToken cancellationToken)
    {
        // Handle side effects (e.g., send notifications, update read models)
    }
}
```

## CQRS Implementation

### Command Pattern
```csharp
// Commands are immutable records implementing ICommand<TResponse>
public sealed record ProcessMessageCommand(
    string Message,
    Guid? ConversationId,
    long? UserId,
    Guid? PreviousResponseId
) : ICommand<ProcessMessageResponse>;

// Command handlers implement ICommandHandler<TCommand, TResponse>
public sealed class ProcessMessageHandler 
    : ICommandHandler<ProcessMessageCommand, ProcessMessageResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConversationRepository _repository;
    private readonly IAiClient _aiClient;
    
    public ProcessMessageHandler(
        IUnitOfWork unitOfWork,
        IConversationRepository repository,
        IAiClient aiClient)
    {
        _unitOfWork = unitOfWork;
        _repository = repository;
        _aiClient = aiClient;
    }
    
    public async Task<Result<ProcessMessageResponse>> Handle(
        ProcessMessageCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate
        var conversation = await _repository
            .GetByIdAsync(request.ConversationId, cancellationToken);
            
        // 2. Execute business operation
        var result = conversation.AddMessage(request.Message, request.UserId);
        if (result.IsFailure)
            return Result.Failure<ProcessMessageResponse>(result.Error);
            
        // 3. Process with AI
        var aiResponse = await _aiClient
            .ProcessMessageAsync(request.Message, cancellationToken);
            
        // 4. Save changes
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        // 5. Return response
        return Result.Success(new ProcessMessageResponse(
            ConversationId: conversation.Id,
            ResponseMessage: aiResponse.Content));
    }
}
```

### Query Pattern
```csharp
// Queries are immutable records implementing IQuery<TResponse>
public sealed record GetConversationQuery(
    Guid ConversationId
) : IQuery<ConversationDto>;

// Query handlers implement IQueryHandler<TQuery, TResponse>
public sealed class GetConversationHandler 
    : IQueryHandler<GetConversationQuery, ConversationDto>
{
    private readonly IConversationReadRepository _repository;
    
    public async Task<Result<ConversationDto>> Handle(
        GetConversationQuery request,
        CancellationToken cancellationToken)
    {
        // Use read-optimized repository
        var conversation = await _repository
            .GetDetailedViewAsync(request.ConversationId, cancellationToken);
            
        if (conversation is null)
            return Result.Failure<ConversationDto>(ChatErrors.ConversationNotFound);
            
        return Result.Success(conversation);
    }
}
```

### Command/Query Separation
```csharp
// ❌ WRONG: Mixing read and write
public record CreateUserCommand(string Name) : ICommand<User>; // Returns entity

// ✅ CORRECT: Return only ID or DTO
public record CreateUserCommand(string Name) : ICommand<Guid>; // Returns ID
public record CreateUserCommand(string Name) : ICommand<UserCreatedDto>; // Or DTO

// ❌ WRONG: Query that modifies state
public class GetAndIncrementCounterQuery { } // Bad - queries shouldn't modify

// ✅ CORRECT: Separate read and write
public record GetCounterQuery() : IQuery<int>;
public record IncrementCounterCommand() : ICommand;
```

## Repository & Data Access Patterns

### Repository Interfaces
```csharp
// Separate read and write repositories
public interface IWriteRepository<TEntity, TId> 
    where TEntity : class, IEntity<TId>
{
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default);
    Task AddAsync(TEntity entity, CancellationToken ct = default);
    void Update(TEntity entity);
    void Delete(TEntity entity);
    Task<bool> ExistsAsync(TId id, CancellationToken ct = default);
}

public interface IReadRepository<TEntity, TId>
    where TEntity : class, IEntity<TId>
{
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default);
    Task<List<TEntity>> GetAllAsync(CancellationToken ct = default);
    Task<PagedResult<TEntity>> GetPagedAsync(
        int page, int pageSize, CancellationToken ct = default);
    IQueryable<TEntity> Query();
}

// Domain-specific repository interfaces
public interface IConversationRepository : IWriteRepository<Conversation, ConversationId>
{
    Task<Conversation?> GetActiveByUserAsync(UserId userId, CancellationToken ct);
    Task<List<Conversation>> GetRecentAsync(int count, CancellationToken ct);
}
```

### Repository Implementation with Caching
```csharp
public class ConversationRepository : EfWriteRepository<Conversation, ConversationId>, 
    IConversationRepository
{
    private readonly IMemoryCache _cache;
    
    public ConversationRepository(ChatDbContext context, IMemoryCache cache) 
        : base(context)
    {
        _cache = cache;
    }
    
    public override async Task<Conversation?> GetByIdAsync(
        ConversationId id, 
        CancellationToken ct = default)
    {
        // Check cache first
        if (_cache.TryGetValue(CacheKey(id), out Conversation? cached))
            return cached;
            
        var entity = await base.GetByIdAsync(id, ct);
        
        if (entity != null)
        {
            _cache.Set(CacheKey(id), entity, TimeSpan.FromMinutes(5));
        }
        
        return entity;
    }
    
    public override void Update(Conversation entity)
    {
        base.Update(entity);
        _cache.Remove(CacheKey(entity.Id)); // Invalidate cache
    }
    
    private static string CacheKey(ConversationId id) => $"conversation:{id}";
}
```

### Read Model Repository
```csharp
public class ConversationReadRepository : IConversationReadRepository
{
    private readonly ReadDbContext _context;
    private readonly IMemoryCache _cache;
    
    public async Task<ConversationDetailDto?> GetDetailedViewAsync(
        Guid id, 
        CancellationToken ct)
    {
        return await _context.Conversations
            .AsNoTracking()
            .Include(c => c.Messages)
            .Include(c => c.Participants)
            .Where(c => c.Id == id)
            .Select(c => new ConversationDetailDto
            {
                Id = c.Id,
                Title = c.Title,
                Messages = c.Messages.Select(m => new MessageDto
                {
                    Id = m.Id,
                    Content = m.Content,
                    CreatedAt = m.CreatedAt
                }).ToList(),
                ParticipantCount = c.Participants.Count,
                LastActivityAt = c.Messages.Max(m => m.CreatedAt)
            })
            .FirstOrDefaultAsync(ct);
    }
}
```

## Unit of Work Pattern

### Interface Definition
```csharp
public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
    
    // Repository access
    IConversationRepository Conversations { get; }
    IMessageRepository Messages { get; }
}
```

### Implementation
```csharp
public sealed class ChatUnitOfWork : IUnitOfWork
{
    private readonly ChatDbContext _context;
    private readonly IEventDispatcher _eventDispatcher;
    private IDbContextTransaction? _transaction;
    private bool _disposed;
    
    // Lazy-loaded repositories
    private IConversationRepository? _conversations;
    private IMessageRepository? _messages;
    
    public ChatUnitOfWork(ChatDbContext context, IEventDispatcher eventDispatcher)
    {
        _context = context;
        _eventDispatcher = eventDispatcher;
    }
    
    public IConversationRepository Conversations =>
        _conversations ??= new ConversationRepository(_context);
        
    public IMessageRepository Messages =>
        _messages ??= new MessageRepository(_context);
    
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // Dispatch domain events before saving
        await DispatchDomainEventsAsync(ct);
        
        // Save changes
        return await _context.SaveChangesAsync(ct);
    }
    
    private async Task DispatchDomainEventsAsync(CancellationToken ct)
    {
        var entities = _context.ChangeTracker
            .Entries<IAggregate>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();
            
        var events = entities
            .SelectMany(e => e.DomainEvents)
            .ToList();
            
        // Clear events from entities
        entities.ForEach(e => e.ClearDomainEvents());
        
        // Dispatch events
        foreach (var @event in events)
        {
            await _eventDispatcher.DispatchAsync(@event, ct);
        }
    }
    
    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(ct);
    }
    
    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _transaction?.Dispose();
            _context.Dispose();
            _disposed = true;
        }
    }
}
```

## MediatR Pipeline Behaviors

### Validation Behavior
```csharp
public sealed class ValidationBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IResult
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
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();
            
        if (failures.Count != 0)
        {
            var error = new ValidationError(failures);
            return (TResponse)Result.Failure(error);
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
    
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var requestId = Guid.NewGuid();
        
        _logger.LogInformation(
            "Handling {RequestName} with ID {RequestId}",
            requestName, requestId);
            
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var response = await next();
            
            stopwatch.Stop();
            
            _logger.LogInformation(
                "Handled {RequestName} with ID {RequestId} in {ElapsedMs}ms",
                requestName, requestId, stopwatch.ElapsedMilliseconds);
                
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex,
                "Error handling {RequestName} with ID {RequestId} after {ElapsedMs}ms",
                requestName, requestId, stopwatch.ElapsedMilliseconds);
                
            throw;
        }
    }
}
```

### Transaction Behavior
```csharp
public sealed class TransactionBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
    where TResponse : IResult
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;
    
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Skip if already in transaction
        if (_unitOfWork.HasActiveTransaction)
            return await next();
            
        var typeName = request.GetType().Name;
        
        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            
            _logger.LogInformation("Begin transaction for {Command}", typeName);
            
            var response = await next();
            
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            
            _logger.LogInformation("Committed transaction for {Command}", typeName);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in transaction for {Command}", typeName);
            
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            
            throw;
        }
    }
}
```

### Caching Behavior
```csharp
public sealed class CachingBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICacheableQuery<TResponse>
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;
    
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var cacheKey = request.CacheKey;
        
        if (_cache.TryGetValue<TResponse>(cacheKey, out var cachedResponse))
        {
            _logger.LogDebug("Cache hit for key {CacheKey}", cacheKey);
            return cachedResponse!;
        }
        
        _logger.LogDebug("Cache miss for key {CacheKey}", cacheKey);
        
        var response = await next();
        
        _cache.Set(cacheKey, response, request.CacheDuration);
        
        return response;
    }
}
```

## FastEndpoints Implementation

### Endpoint Structure
```csharp
public sealed class ProcessMessageEndpoint 
    : Endpoint<ProcessMessageRequest, ProcessMessageResponse>
{
    private readonly IMediator _mediator;
    private readonly IErrorMapper _errorMapper;
    
    public override void Configure()
    {
        Post("/api/chat/process");
        Version(1); // API versioning
        
        // Authentication/Authorization
        Policies("RequireAuthenticatedUser");
        
        // Rate limiting
        Throttle(
            hitLimit: 10,
            durationSeconds: 60,
            headerName: "X-Client-Id");
        
        // OpenAPI documentation
        Summary(s =>
        {
            s.Summary = "Process a chat message";
            s.Description = "Processes a user message through the AI chat system";
            s.ExampleRequest = new ProcessMessageRequest("Hello!");
            s.ResponseExamples[200] = new ProcessMessageResponse("Hi there!");
        });
        
        Tags("Chat");
    }
    
    public override async Task HandleAsync(
        ProcessMessageRequest req,
        CancellationToken ct)
    {
        // Validate request
        var validationResult = await ValidateAsync(req, ct);
        if (!validationResult.IsValid)
        {
            await SendErrorsAsync(validationResult.Errors, cancellation: ct);
            return;
        }
        
        // Map to command
        var command = req.ToCommand();
        
        // Execute via MediatR
        var result = await _mediator.Send(command, ct);
        
        // Handle result
        await result.Match(
            onSuccess: async response =>
            {
                await SendOkAsync(response.ToDto(), ct);
            },
            onFailure: async error =>
            {
                await SendErrorAsync(error, ct);
            });
    }
}
```

### Request/Response DTOs
```csharp
public sealed record ProcessMessageRequest(
    string Message,
    string? ConversationId = null
) : IRequest
{
    public ProcessMessageCommand ToCommand() => new(
        Message: Message,
        ConversationId: ConversationId != null ? Guid.Parse(ConversationId) : null,
        UserId: null,
        PreviousResponseId: null);
}

public sealed record ProcessMessageResponse(
    string Response,
    string ConversationId,
    ToolExecutionResponse[]? ToolExecutions = null
);
```

### Endpoint Validation
```csharp
public sealed class ProcessMessageValidator : Validator<ProcessMessageRequest>
{
    public ProcessMessageValidator()
    {
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message is required")
            .MaximumLength(4000).WithMessage("Message too long");
            
        When(x => x.ConversationId != null, () =>
        {
            RuleFor(x => x.ConversationId!)
                .Must(BeValidGuid).WithMessage("Invalid conversation ID");
        });
    }
    
    private bool BeValidGuid(string value) =>
        Guid.TryParse(value, out _);
}
```

### Global Configuration
```csharp
public static class FastEndpointsConfiguration
{
    public static IServiceCollection AddFastEndpointsConfiguration(
        this IServiceCollection services)
    {
        services.AddFastEndpoints(o =>
        {
            o.SourceGeneratorDiscoveredTypes = DiscoveredTypes.All;
        });
        
        // Global error handling
        services.AddSingleton<IErrorMapper, ErrorMapper>();
        
        // Validation
        services.AddValidatorsFromAssemblyContaining<Program>();
        
        return services;
    }
    
    public static WebApplication UseFastEndpointsConfiguration(
        this WebApplication app)
    {
        app.UseFastEndpoints(c =>
        {
            c.Endpoints.RoutePrefix = "api";
            c.Versioning.Prefix = "v";
            c.Versioning.DefaultVersion = 1;
            
            c.Serializer.Options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            
            c.Errors.ResponseBuilder = (failures, ctx, statusCode) =>
            {
                return new ProblemDetails
                {
                    Title = "Validation Failed",
                    Status = statusCode,
                    Detail = string.Join(", ", failures.Select(f => f.ErrorMessage)),
                    Instance = ctx.Request.Path,
                    Extensions = { ["errors"] = failures }
                };
            };
        });
        
        return app;
    }
}
```

## Entity Framework Core Patterns

### DbContext Configuration
```csharp
public sealed class ChatDbContext : WriteDbContextBase
{
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    
    public ChatDbContext(DbContextOptions<ChatDbContext> options) 
        : base(options)
    {
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Apply configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChatDbContext).Assembly);
        
        // Global query filters
        modelBuilder.Entity<Conversation>()
            .HasQueryFilter(c => !c.IsDeleted);
            
        // Indexes
        modelBuilder.Entity<Message>()
            .HasIndex(m => m.CreatedAt)
            .HasDatabaseName("IX_Messages_CreatedAt");
    }
}
```

### Entity Configuration
```csharp
public sealed class ConversationConfiguration 
    : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        // Table mapping
        builder.ToTable("conversations", "chat");
        
        // Primary key
        builder.HasKey(c => c.Id);
        
        // Properties
        builder.Property(c => c.Id)
            .HasConversion(
                v => v.Value,
                v => new ConversationId(v))
            .ValueGeneratedNever();
            
        builder.Property(c => c.Title)
            .IsRequired()
            .HasMaxLength(200);
            
        builder.Property(c => c.State)
            .HasConversion<string>()
            .HasMaxLength(50);
            
        // Value objects
        builder.OwnsOne(c => c.Metadata, metadata =>
        {
            metadata.Property(m => m.Tags)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null)!);
        });
        
        // Relationships
        builder.HasMany(c => c.Messages)
            .WithOne()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
            
        // Ignore domain events
        builder.Ignore(c => c.DomainEvents);
        
        // Optimistic concurrency
        builder.Property(c => c.Version)
            .IsConcurrencyToken();
    }
}
```

### Migrations Management
```csharp
public static class MigrationExtensions
{
    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        
        var contexts = new[]
        {
            scope.ServiceProvider.GetRequiredService<ChatDbContext>(),
            scope.ServiceProvider.GetRequiredService<IdentityDbContext>()
        };
        
        foreach (var context in contexts)
        {
            var pendingMigrations = await context.Database
                .GetPendingMigrationsAsync();
                
            if (pendingMigrations.Any())
            {
                Console.WriteLine($"Applying {pendingMigrations.Count()} migrations...");
                await context.Database.MigrateAsync();
            }
        }
    }
}
```

## Building Blocks Architecture

### Core Building Blocks
```csharp
// Base Entity
public abstract class BaseEntity<TId> : IEntity<TId>
    where TId : notnull
{
    public TId Id { get; protected set; } = default!;
    public DateTime CreatedAt { get; protected set; }
    public DateTime? UpdatedAt { get; protected set; }
    public bool IsDeleted { get; protected set; }
    
    protected BaseEntity() 
    {
        CreatedAt = DateTime.UtcNow;
    }
}

// Base Aggregate
public abstract class BaseAggregate<TId> : BaseEntity<TId>, IAggregate
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = new();
    
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    
    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }
    
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

// Base Value Object
public abstract record ValueObject
{
    protected static Result<T> Create<T>(
        Func<T> factory,
        params (bool condition, Error error)[] validations)
    {
        foreach (var (condition, error) in validations)
        {
            if (!condition)
                return Result.Failure<T>(error);
        }
        
        return Result.Success(factory());
    }
}
```

### Common Interfaces
```csharp
// Core interfaces from BuildingBlocks
public interface IEntity<TId> : IIdentifiable<TId>
    where TId : notnull
{
    DateTime CreatedAt { get; }
    DateTime? UpdatedAt { get; }
}

public interface IAggregate : IEntity
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}

public interface IStrongId<T>
{
    T Value { get; }
}

public interface IDomainEvent : INotification
{
    DateTime OccurredAt { get; }
}

public interface ICommand<TResponse> : IRequest<Result<TResponse>>
{
}

public interface IQuery<TResponse> : IRequest<Result<TResponse>>
{
}
```

## Error Handling

### Result Pattern
```csharp
// All operations return Result<T> for explicit error handling
public async Task<Result<ConversationDto>> GetConversationAsync(Guid id)
{
    try
    {
        var conversation = await _repository.GetByIdAsync(id);
        
        if (conversation is null)
            return Result.Failure<ConversationDto>(ChatErrors.NotFound);
            
        if (!conversation.CanAccess(_currentUser))
            return Result.Failure<ConversationDto>(ChatErrors.Unauthorized);
            
        return Result.Success(conversation.ToDto());
    }
    catch (DatabaseException ex)
    {
        _logger.LogError(ex, "Database error retrieving conversation {Id}", id);
        return Result.Failure<ConversationDto>(ChatErrors.DatabaseError);
    }
}

// Usage with pattern matching
var result = await GetConversationAsync(id);
return result.Match(
    onSuccess: dto => Ok(dto),
    onFailure: error => Problem(error.Message, statusCode: error.Code)
);
```

### Domain Errors
```csharp
public static class ChatErrors
{
    public static readonly Error ConversationNotFound = new(
        "Chat.ConversationNotFound",
        "The specified conversation was not found",
        ErrorType.NotFound);
        
    public static readonly Error MessageTooLong = new(
        "Chat.MessageTooLong",
        "Message exceeds maximum length of 4000 characters",
        ErrorType.Validation);
        
    public static readonly Error ConversationArchived = new(
        "Chat.ConversationArchived",
        "Cannot modify archived conversation",
        ErrorType.BusinessRule);
        
    public static Error UserNotAuthorized(UserId userId) => new(
        "Chat.UserNotAuthorized",
        $"User {userId} is not authorized for this operation",
        ErrorType.Authorization);
}
```

### Exception Handling
```csharp
// Global exception handler in API layer
public class GlobalExceptionMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (DomainException ex)
        {
            // Domain exceptions are expected
            await HandleDomainException(context, ex);
        }
        catch (ValidationException ex)
        {
            // Validation failures
            await HandleValidationException(context, ex);
        }
        catch (Exception ex)
        {
            // Unexpected exceptions
            _logger.LogError(ex, "Unhandled exception occurred");
            await HandleGenericException(context, ex);
        }
    }
}
```

## Validation Patterns

### FluentValidation for Commands/Queries
```csharp
public sealed class ProcessMessageValidator : AbstractValidator<ProcessMessageCommand>
{
    public ProcessMessageValidator()
    {
        // Property-level validation
        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message is required")
            .MaximumLength(4000).WithMessage("Message too long")
            .Must(NotContainProfanity).WithMessage("Inappropriate content");
            
        // Conditional validation
        When(x => x.ConversationId.HasValue, () =>
        {
            RuleFor(x => x.ConversationId!.Value)
                .NotEqual(Guid.Empty).WithMessage("Invalid conversation ID");
        });
        
        // Async validation with dependencies
        RuleFor(x => x.UserId)
            .MustAsync(BeActiveUser).WithMessage("User is not active");
    }
    
    private bool NotContainProfanity(string message) 
        => !_profanityFilter.ContainsProfanity(message);
        
    private async Task<bool> BeActiveUser(long? userId, CancellationToken ct)
        => userId.HasValue && await _userService.IsActiveAsync(userId.Value, ct);
}
```

### Domain Validation
```csharp
// Value object validation in factory methods
public static Result<Email> Create(string value)
{
    if (string.IsNullOrWhiteSpace(value))
        return Result.Failure<Email>(DomainErrors.EmailRequired);
        
    if (!EmailRegex.IsMatch(value))
        return Result.Failure<Email>(DomainErrors.InvalidEmailFormat);
        
    return Result.Success(new Email(value));
}

// Aggregate invariant protection
public Result AddMessage(string content)
{
    // Validate state invariants
    if (State != ConversationState.Active)
        return Result.Failure(ChatErrors.ConversationNotActive);
        
    if (_messages.Count >= MaxMessages)
        return Result.Failure(ChatErrors.MessageLimitExceeded);
        
    // Validate business rules
    var contentResult = MessageContent.Create(content);
    if (contentResult.IsFailure)
        return Result.Failure(contentResult.Error);
        
    _messages.Add(new Message(contentResult.Value));
    return Result.Success();
}
```

## Testing Standards

### Test Organization
```csharp
// Test class naming: [ClassUnderTest]Tests
public class ConversationTests
{
    // Test method naming: [Method]_[Scenario]_[ExpectedResult]
    [Test]
    public void AddMessage_WhenConversationIsActive_ShouldSucceed()
    {
        // Arrange
        var conversation = ConversationFactory.CreateActive();
        var message = "Test message";
        
        // Act
        var result = conversation.AddMessage(message, UserId.New());
        
        // Assert
        result.Should().BeSuccess();
        conversation.Messages.Should().HaveCount(1);
        conversation.DomainEvents.Should().ContainSingle(
            e => e is MessageAddedDomainEvent);
    }
    
    [Test]
    public void AddMessage_WhenConversationIsArchived_ShouldReturnFailure()
    {
        // Arrange
        var conversation = ConversationFactory.CreateArchived();
        
        // Act
        var result = conversation.AddMessage("Test", UserId.New());
        
        // Assert
        result.Should().BeFailure()
            .WithError(ChatErrors.ConversationNotActive);
    }
}
```

### Test Data Builders
```csharp
public class ConversationBuilder
{
    private ConversationId _id = ConversationId.New();
    private UserId _userId = UserId.New();
    private ConversationState _state = ConversationState.Active;
    private readonly List<Message> _messages = new();
    
    public ConversationBuilder WithId(ConversationId id)
    {
        _id = id;
        return this;
    }
    
    public ConversationBuilder WithState(ConversationState state)
    {
        _state = state;
        return this;
    }
    
    public ConversationBuilder WithMessages(params string[] messages)
    {
        foreach (var message in messages)
        {
            _messages.Add(Message.Create(message, _userId));
        }
        return this;
    }
    
    public Conversation Build()
    {
        var conversation = new Conversation(_id, _userId);
        
        // Use reflection or internal methods to set state
        typeof(Conversation)
            .GetProperty(nameof(Conversation.State))!
            .SetValue(conversation, _state);
            
        foreach (var message in _messages)
        {
            conversation.AddMessage(message.Content, message.UserId);
        }
        
        return conversation;
    }
}
```

### Integration Testing
```csharp
public class ProcessMessageIntegrationTests : IntegrationTestBase
{
    [Test]
    public async Task ProcessMessage_WithValidRequest_ShouldReturnAiResponse()
    {
        // Arrange
        var request = new ProcessMessageRequest
        {
            Message = "Hello AI",
            ConversationId = null
        };
        
        // Act
        var response = await Client.PostAsJsonAsync("/api/chat/process", request);
        
        // Assert
        response.Should().BeSuccessful();
        var content = await response.Content.ReadFromJsonAsync<ProcessMessageResponse>();
        content.Should().NotBeNull();
        content!.Response.Should().NotBeNullOrEmpty();
        
        // Verify database state
        await Using<ChatDbContext>(async db =>
        {
            var conversation = await db.Conversations
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == content.ConversationId);
                
            conversation.Should().NotBeNull();
            conversation!.Messages.Should().HaveCount(2); // User + AI
        });
    }
}
```

## Logging & Observability

### Structured Logging with Serilog
```csharp
public static class LoggingConfiguration
{
    public static IHostBuilder ConfigureLogging(this IHostBuilder host)
    {
        return host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName()
                .Enrich.WithProperty("Application", "Axon.Backend")
                .WriteTo.Console(new RenderedCompactJsonFormatter())
                .WriteTo.File(
                    new CompactJsonFormatter(),
                    "logs/axon-.json",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30);
        });
    }
}
```

### Application Insights / OpenTelemetry
```csharp
public static class ObservabilityConfiguration
{
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Metrics
        services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddPrometheusExporter();
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation()
                    .AddSource("Axon.Backend")
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(
                            configuration["OpenTelemetry:Endpoint"]!);
                    });
            });
            
        // Custom metrics
        services.AddSingleton<IMetrics, CustomMetrics>();
        
        return services;
    }
}
```

### Activity Tracking
```csharp
public class ActivityTrackingMiddleware
{
    private static readonly ActivitySource ActivitySource = 
        new("Axon.Backend", "1.0.0");
        
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        using var activity = ActivitySource.StartActivity(
            $"{context.Request.Method} {context.Request.Path}",
            ActivityKind.Server);
            
        activity?.SetTag("http.method", context.Request.Method);
        activity?.SetTag("http.url", context.Request.Path);
        activity?.SetTag("user.id", context.User?.Identity?.Name);
        
        try
        {
            await next(context);
            activity?.SetTag("http.status_code", context.Response.StatusCode);
        }
        catch (Exception ex)
        {
            activity?.RecordException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
}
```

## Dependency Injection

### Service Registration Patterns
```csharp
public static class ServiceCollectionExtensions
{
    // Module registration
    public static IServiceCollection AddChatModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register repositories
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IConversationReadRepository, ConversationReadRepository>();
        
        // Register unit of work
        services.AddScoped<IUnitOfWork, ChatUnitOfWork>();
        
        // Register services
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<IAiClient, OpenAiClient>();
        
        // Register MediatR handlers
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(ChatModule).Assembly);
            
            // Register pipeline behaviors
            cfg.AddBehavior<IPipelineBehavior<,>, ValidationBehavior<,>>();
            cfg.AddBehavior<IPipelineBehavior<,>, LoggingBehavior<,>>();
            cfg.AddBehavior<IPipelineBehavior<,>, TransactionBehavior<,>>();
            cfg.AddBehavior<IPipelineBehavior<,>, CachingBehavior<,>>();
        });
        
        // Register validators
        services.AddValidatorsFromAssembly(typeof(ChatModule).Assembly);
        
        // Register DbContext
        services.AddDbContext<ChatDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("ChatDb"),
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(ChatDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure();
                });
                
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });
        
        return services;
    }
}
```

### Lifetime Management
```csharp
// Service lifetime guidelines:

// Transient - New instance every time
services.AddTransient<IValidator<ProcessMessageCommand>, ProcessMessageValidator>();
services.AddTransient<INotificationHandler<MessageAddedDomainEvent>, MessageAddedHandler>();

// Scoped - One instance per request/scope
services.AddScoped<IUnitOfWork, UnitOfWork>();
services.AddScoped<IConversationRepository, ConversationRepository>();
services.AddScoped<DbContext, ChatDbContext>();

// Singleton - One instance for application lifetime
services.AddSingleton<IMemoryCache, MemoryCache>();
services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect("localhost"));
services.AddSingleton<IMessageBroker, RabbitMqMessageBroker>();

// Factory pattern for complex initialization
services.AddScoped<IAiClient>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>()
        .CreateClient("OpenAI");
        
    return new OpenAiClient(httpClient, configuration["OpenAI:ApiKey"]);
});
```

## Caching Strategies

### Memory Caching
```csharp
public class CachedConversationService : IConversationService
{
    private readonly IMemoryCache _cache;
    private readonly IConversationRepository _repository;
    
    public async Task<ConversationDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var cacheKey = $"conversation:{id}";
        
        // Try get from cache
        if (_cache.TryGetValue<ConversationDto>(cacheKey, out var cached))
        {
            return cached;
        }
        
        // Load from database
        var conversation = await _repository.GetByIdAsync(id, ct);
        if (conversation == null) return null;
        
        var dto = conversation.ToDto();
        
        // Add to cache with sliding expiration
        _cache.Set(cacheKey, dto, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(5),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
            Priority = CacheItemPriority.Normal
        });
        
        return dto;
    }
    
    public async Task InvalidateAsync(Guid id)
    {
        _cache.Remove($"conversation:{id}");
        await Task.CompletedTask;
    }
}
```

### Distributed Caching with Redis
```csharp
public class RedisConversationCache : IConversationCache
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    
    public RedisConversationCache(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _db = redis.GetDatabase();
    }
    
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var value = await _db.StringGetAsync(key);
        
        if (value.IsNullOrEmpty)
            return default;
            
        return JsonSerializer.Deserialize<T>(value!);
    }
    
    public async Task SetAsync<T>(
        string key, 
        T value, 
        TimeSpan? expiry = null,
        CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(value);
        await _db.StringSetAsync(key, json, expiry);
    }
    
    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        await _db.KeyDeleteAsync(key);
    }
    
    public async Task RemoveByPatternAsync(string pattern, CancellationToken ct = default)
    {
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var keys = server.Keys(pattern: pattern).ToArray();
        
        if (keys.Any())
        {
            await _db.KeyDeleteAsync(keys);
        }
    }
}
```

### Cache-Aside Pattern Implementation
```csharp
public class CacheAsideRepository<TEntity, TId> : IRepository<TEntity, TId>
    where TEntity : class, IEntity<TId>
{
    private readonly IRepository<TEntity, TId> _innerRepository;
    private readonly ICache _cache;
    private readonly ICacheKeyGenerator _keyGenerator;
    
    public async Task<TEntity?> GetByIdAsync(TId id, CancellationToken ct = default)
    {
        var key = _keyGenerator.Generate<TEntity>(id);
        
        // Check cache
        var cached = await _cache.GetAsync<TEntity>(key, ct);
        if (cached != null)
            return cached;
            
        // Load from repository
        var entity = await _innerRepository.GetByIdAsync(id, ct);
        
        if (entity != null)
        {
            // Update cache
            await _cache.SetAsync(key, entity, TimeSpan.FromMinutes(5), ct);
        }
        
        return entity;
    }
    
    public async Task AddAsync(TEntity entity, CancellationToken ct = default)
    {
        await _innerRepository.AddAsync(entity, ct);
        
        // Optionally pre-populate cache
        var key = _keyGenerator.Generate<TEntity>(entity.Id);
        await _cache.SetAsync(key, entity, TimeSpan.FromMinutes(5), ct);
    }
    
    public async Task UpdateAsync(TEntity entity, CancellationToken ct = default)
    {
        await _innerRepository.UpdateAsync(entity, ct);
        
        // Invalidate cache
        var key = _keyGenerator.Generate<TEntity>(entity.Id);
        await _cache.RemoveAsync(key, ct);
    }
}
```

## API Design & Versioning

### API Versioning Strategy
```csharp
public static class ApiVersioningConfiguration
{
    public static IServiceCollection AddApiVersioningConfiguration(
        this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = ApiVersionReader.Combine(
                new UrlSegmentApiVersionReader(),
                new HeaderApiVersionReader("X-Api-Version"),
                new MediaTypeApiVersionReader("version"));
        });
        
        services.AddVersionedApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });
        
        return services;
    }
}
```

### RESTful API Design
```csharp
// Resource-based URLs
[Route("api/v{version:apiVersion}/conversations")]
public class ConversationController : BaseController
{
    // GET /api/v1/conversations
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PageRequest page) { }
    
    // GET /api/v1/conversations/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id) { }
    
    // POST /api/v1/conversations
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateConversationRequest request) { }
    
    // PUT /api/v1/conversations/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateConversationRequest request) { }
    
    // DELETE /api/v1/conversations/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id) { }
    
    // POST /api/v1/conversations/{id}/messages
    [HttpPost("{id:guid}/messages")]
    public async Task<IActionResult> AddMessage(Guid id, [FromBody] AddMessageRequest request) { }
}
```

### API Response Standards
```csharp
// Standard response wrapper
public record ApiResponse<T>(
    T? Data,
    string? Message = null,
    Dictionary<string, string[]>? Errors = null,
    PaginationMetadata? Pagination = null
) where T : class;

public record PaginationMetadata(
    int CurrentPage,
    int PageSize,
    int TotalPages,
    int TotalCount,
    bool HasPrevious,
    bool HasNext
);

// Error response
public record ProblemDetailsResponse(
    string Type,
    string Title,
    int Status,
    string Detail,
    string Instance,
    Dictionary<string, object>? Extensions = null
);
```

## Documentation Requirements

### XML Documentation
```csharp
/// <summary>
/// Processes incoming chat messages and generates AI responses using MCP servers.
/// </summary>
/// <remarks>
/// This handler orchestrates the complete message processing flow:
/// 1. Validates the incoming message
/// 2. Creates or retrieves the conversation
/// 3. Sends to AI service with MCP integration
/// 4. Persists the interaction
/// 5. Returns the formatted response
/// </remarks>
public sealed class ProcessMessageHandler : ICommandHandler<ProcessMessageCommand, ProcessMessageResponse>
{
    /// <summary>
    /// Handles the process message command.
    /// </summary>
    /// <param name="request">The command containing message details</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <returns>
    /// A Result containing either:
    /// - Success: ProcessMessageResponse with AI-generated content
    /// - Failure: Error details if processing failed
    /// </returns>
    /// <exception cref="ArgumentNullException">If request is null</exception>
    public async Task<Result<ProcessMessageResponse>> Handle(
        ProcessMessageCommand request,
        CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

### OpenAPI/Swagger Documentation
```csharp
public static class SwaggerConfiguration
{
    public static IServiceCollection AddSwaggerConfiguration(
        this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Axon Backend API",
                Version = "v1",
                Description = "AI-powered chat system with MCP integration",
                Contact = new OpenApiContact
                {
                    Name = "Axon Team",
                    Email = "support@axon.ai"
                }
            });
            
            // Add security definitions
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });
            
            // Include XML comments
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            options.IncludeXmlComments(xmlPath);
            
            // Custom operation filters
            options.OperationFilter<SwaggerDefaultValues>();
            options.SchemaFilter<SwaggerSkipPropertyFilter>();
        });
        
        return services;
    }
}
```

### README Files
Each module should have a README.md:
```markdown
# Chat Module

## Overview
Handles real-time conversation management with AI integration through MCP servers.

## Key Features
- Multi-turn conversations with context preservation
- MCP server integration for tool execution
- Event sourcing for conversation history
- Real-time message streaming

## Architecture
- **Domain**: Conversation aggregate, Message entities
- **Application**: CQRS handlers for message processing
- **Infrastructure**: PostgreSQL persistence, OpenAI client

## Usage
```csharp
// Process a message
var command = new ProcessMessageCommand("Hello", conversationId);
var result = await mediator.Send(command);
```

## Configuration
```json
{
  "ChatModule": {
    "MaxMessageLength": 4000,
    "ConversationTimeout": "00:30:00",
    "AiModel": "gpt-4"
  }
}
```

## Testing
```bash
dotnet test --filter Category=Chat
```
```

## Performance Considerations

### Async Best Practices
```csharp
// ✅ Use async all the way down
public async Task<Result> ProcessAsync()
{
    // Don't block async code
    await DoWorkAsync(); // Good
    // DoWorkAsync().Wait(); // Bad - blocks thread
    
    // Configure await for library code
    await DoWorkAsync().ConfigureAwait(false);
    
    // Parallel async operations
    var tasks = items.Select(ProcessItemAsync);
    await Task.WhenAll(tasks);
}

// ✅ Use ValueTask for hot paths
public ValueTask<bool> IsCachedAsync(string key)
{
    if (_cache.TryGetValue(key, out var value))
        return new ValueTask<bool>(true); // No allocation
        
    return new ValueTask<bool>(CheckDatabaseAsync(key));
}

// ✅ Use async streams for large data sets
public async IAsyncEnumerable<Message> StreamMessagesAsync(
    [EnumeratorCancellation] CancellationToken ct = default)
{
    await using var connection = new NpgsqlConnection(_connectionString);
    await connection.OpenAsync(ct);
    
    await using var cmd = new NpgsqlCommand(
        "SELECT * FROM messages WHERE conversation_id = @id", connection);
    cmd.Parameters.AddWithValue("id", _conversationId);
    
    await using var reader = await cmd.ExecuteReaderAsync(ct);
    
    while (await reader.ReadAsync(ct))
    {
        yield return MapToMessage(reader);
    }
}
```

### Memory Management
```csharp
// ✅ Use object pooling for frequently allocated objects
public class MessageProcessor
{
    private readonly ObjectPool<StringBuilder> _stringBuilderPool;
    
    public string ProcessMessage(string input)
    {
        var sb = _stringBuilderPool.Get();
        try
        {
            sb.Append(input);
            // Process...
            return sb.ToString();
        }
        finally
        {
            sb.Clear();
            _stringBuilderPool.Return(sb);
        }
    }
}

// ✅ Use Span<T> and Memory<T> for zero-allocation string processing
public ReadOnlySpan<char> ExtractToken(ReadOnlySpan<char> input)
{
    var index = input.IndexOf(':');
    return index > 0 ? input.Slice(0, index) : ReadOnlySpan<char>.Empty;
}

// ✅ Use ArrayPool for temporary arrays
public async Task ProcessBatchAsync(IEnumerable<Message> messages)
{
    var array = ArrayPool<Message>.Shared.Rent(messages.Count());
    try
    {
        messages.ToArray().CopyTo(array, 0);
        // Process array
    }
    finally
    {
        ArrayPool<Message>.Shared.Return(array, clearArray: true);
    }
}
```

### Query Optimization
```csharp
// ✅ Use projection for read models
public async Task<ConversationSummaryDto> GetSummaryAsync(Guid id)
{
    return await _context.Conversations
        .Where(c => c.Id == id)
        .Select(c => new ConversationSummaryDto
        {
            Id = c.Id,
            Title = c.Title,
            MessageCount = c.Messages.Count,
            LastMessageAt = c.Messages.Max(m => m.CreatedAt)
        })
        .FirstOrDefaultAsync();
}

// ✅ Use pagination for large datasets
public async Task<PagedResult<MessageDto>> GetMessagesAsync(
    Guid conversationId,
    int page,
    int pageSize)
{
    var query = _context.Messages
        .Where(m => m.ConversationId == conversationId)
        .OrderByDescending(m => m.CreatedAt);
        
    var total = await query.CountAsync();
    
    var items = await query
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(m => m.ToDto())
        .ToListAsync();
        
    return new PagedResult<MessageDto>(items, total, page, pageSize);
}

// ✅ Use compiled queries for hot paths
private static readonly Func<ChatDbContext, Guid, Task<Conversation?>> GetConversationByIdQuery =
    EF.CompileAsyncQuery((ChatDbContext context, Guid id) =>
        context.Conversations
            .Include(c => c.Messages)
            .FirstOrDefault(c => c.Id == id));
            
public Task<Conversation?> GetByIdAsync(Guid id) =>
    GetConversationByIdQuery(_context, id);
```

## Security Practices

### Input Validation
```csharp
// Always validate and sanitize input
public Result<string> SanitizeHtml(string input)
{
    if (string.IsNullOrWhiteSpace(input))
        return Result.Failure<string>("Input required");
        
    var sanitizer = new HtmlSanitizer();
    sanitizer.AllowedTags.Clear();
    sanitizer.AllowedTags.Add("p");
    sanitizer.AllowedTags.Add("strong");
    sanitizer.AllowedTags.Add("em");
    
    var sanitized = sanitizer.Sanitize(input);
    return Result.Success(sanitized);
}
```

### Authentication & Authorization
```csharp
// Use policy-based authorization
[Authorize(Policy = "RequireAuthenticatedUser")]
public class ConversationEndpoint : EndpointBase
{
    public override void Configure()
    {
        Post("/api/conversations");
        Policies("RequireAuthenticatedUser", "CanCreateConversation");
    }
}

// Implement domain-level authorization
public Result<Conversation> GetConversation(Guid id, UserId userId)
{
    var conversation = _repository.GetById(id);
    
    if (!conversation.CanAccess(userId))
        return Result.Failure<Conversation>(AuthErrors.Unauthorized);
        
    return Result.Success(conversation);
}
```

### Sensitive Data Protection
```csharp
// Never log sensitive data
_logger.LogInformation("User {UserId} accessed conversation {ConversationId}",
    userId.Value, // Log ID only
    conversationId);
    
// Don't include sensitive data in exceptions
throw new DomainException($"Invalid operation for user {userId}"); // Only ID

// Use data protection for sensitive fields
public class UserProfile
{
    public string Email { get; set; } // Encrypted at rest
    
    [PersonalData]
    public string PhoneNumber { get; set; }
    
    [JsonIgnore] // Don't serialize
    public string PasswordHash { get; set; }
}
```

### SQL Injection Prevention
```csharp
// ✅ Always use parameterized queries
var conversations = await _context.Conversations
    .Where(c => c.UserId == userId)
    .ToListAsync();

// ✅ Use EF Core for queries
var sql = @"
    SELECT * FROM Conversations 
    WHERE UserId = @userId 
    AND CreatedAt > @startDate";
    
var result = await _context.Conversations
    .FromSqlRaw(sql, 
        new SqlParameter("@userId", userId),
        new SqlParameter("@startDate", startDate))
    .ToListAsync();

// ❌ NEVER concatenate SQL strings
var sql = $"SELECT * FROM Users WHERE Name = '{userName}'"; // SQL Injection risk!
```

## Migration & Database Management

### Migration Best Practices
```csharp
// Create migrations with descriptive names
// dotnet ef migrations add AddConversationStateIndex -c ChatDbContext

public partial class AddConversationStateIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Conversations_State_CreatedAt",
            table: "Conversations",
            columns: new[] { "State", "CreatedAt" },
            descending: new[] { false, true });
            
        // Add data migration if needed
        migrationBuilder.Sql(@"
            UPDATE Conversations 
            SET State = 'Active' 
            WHERE State IS NULL");
    }
    
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Conversations_State_CreatedAt",
            table: "Conversations");
    }
}
```

### Database Seeding
```csharp
public class ChatDataSeeder : IDataSeeder
{
    private readonly ChatDbContext _context;
    
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await _context.Conversations.AnyAsync(ct))
            return; // Already seeded
            
        var conversations = new[]
        {
            Conversation.Create(UserId.New(), "Welcome Conversation"),
            Conversation.Create(UserId.New(), "Sample Chat")
        };
        
        _context.Conversations.AddRange(conversations.Select(r => r.Value));
        await _context.SaveChangesAsync(ct);
    }
}
```

## Code Review Checklist

Before submitting PR, ensure:

- [ ] **SOLID principles** are followed
- [ ] **No code duplication** - extracted to Building Blocks if needed
- [ ] **Proper error handling** with Result pattern
- [ ] **All public APIs documented** with XML comments
- [ ] **Unit tests** for business logic (>80% coverage)
- [ ] **Integration tests** for critical paths
- [ ] **No hardcoded values** - use configuration
- [ ] **No sensitive data** in logs or exceptions
- [ ] **Async/await** used properly
- [ ] **Cancellation tokens** passed through
- [ ] **Null checks** and validation in place
- [ ] **Performance** considered (no N+1 queries)
- [ ] **Security** validated (input sanitization, auth checks)
- [ ] **Code formatted** according to .editorconfig
- [ ] **No compiler warnings**
- [ ] **Repository pattern** used for data access
- [ ] **Unit of Work** for transaction management
- [ ] **MediatR behaviors** for cross-cutting concerns
- [ ] **FastEndpoints** for API implementation
- [ ] **EF Core configurations** properly set up
- [ ] **Caching strategy** implemented where appropriate
- [ ] **Logging** added for important operations
- [ ] **OpenTelemetry** instrumentation included

---

*Last Updated: December 2024*
*Version: 2.0.0*