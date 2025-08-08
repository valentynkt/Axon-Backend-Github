# Story 06: Transaction Management Enhancement with Outbox Pattern

**Story ID:** AXON-CQRS-006  
**Epic:** Epic_04_CQRS_Foundation  
**Priority:** P1 - High  
**Estimated Effort:** 8 hours  
**Dependencies:** Story_01 (W3C TraceContext and Metadata)  
**Target Sprint:** Current  

---

## 📋 User Story

**As a** backend developer implementing CQRS commands,  
**I want** enhanced transaction management with outbox pattern support and metadata-aware transaction boundaries,  
**So that** I can ensure data consistency and reliable event publishing in distributed scenarios.

---

## 🎯 Story Context

### Existing System Integration

- **Current State:** 
  - Basic transaction behavior may exist
  - No outbox pattern implementation
  - Manual transaction boundary management
  - Limited distributed transaction support

- **Integration Points:**
  - Entity Framework Core transaction management
  - Domain events publishing
  - MediatR pipeline behaviors
  - Distributed systems integration

- **Technology Stack:** 
  - .NET 10 with EF Core 9
  - MediatR for CQRS
  - SQL Server/PostgreSQL
  - Event publishing infrastructure

- **Architectural Layer:** BuildingBlocks/Application/Behaviors

### Patterns to Follow

```csharp
// Transaction boundary around command execution
using var transaction = await _dbContext.Database.BeginTransactionAsync();
try
{
    // Execute command
    await next();
    
    // Publish events
    await PublishEvents();
    
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

---

## ✅ Acceptance Criteria

### Functional Requirements

1. **Automatic Transaction Management**
   - [ ] Start transaction for commands automatically
   - [ ] Skip transactions for queries
   - [ ] Support nested transaction scenarios
   - [ ] Handle transaction timeout configuration

2. **Outbox Pattern Implementation**
   - [ ] Store events in outbox table within transaction
   - [ ] Reliable event publishing after transaction commit
   - [ ] Event deduplication and retry logic
   - [ ] Event ordering guarantees

3. **Metadata-Aware Transactions**
   - [ ] Include trace context in transaction logs
   - [ ] Tenant-specific transaction isolation
   - [ ] Feature flag controlled transaction behavior
   - [ ] User context in transaction metadata

4. **Distributed Transaction Support**
   - [ ] Integration with message brokers
   - [ ] Saga pattern preparation
   - [ ] Compensation action tracking
   - [ ] Cross-service transaction coordination

### Non-Functional Requirements

1. **Reliability**
   - [ ] ACID compliance for data operations
   - [ ] At-least-once delivery for events
   - [ ] Dead letter handling for failed events
   - [ ] Transaction recovery mechanisms

2. **Performance**
   - [ ] Minimize transaction scope duration
   - [ ] Bulk event processing optimization
   - [ ] Connection pooling efficiency
   - [ ] Async processing where possible

3. **Observability**
   - [ ] Transaction duration metrics
   - [ ] Event publishing success/failure rates
   - [ ] Deadlock detection and logging
   - [ ] Transaction trace correlation

---

## 🔧 Technical Implementation

### Files to Create/Modify

```yaml
New_Files:
  - src/BuildingBlocks/Application/Behaviors/TransactionBehavior.cs
  - src/BuildingBlocks/Application/Outbox/IOutboxService.cs
  - src/BuildingBlocks/Application/Outbox/OutboxService.cs
  - src/BuildingBlocks/Infrastructure/Outbox/OutboxEntry.cs
  - src/BuildingBlocks/Infrastructure/Outbox/IOutboxRepository.cs
  - src/BuildingBlocks/Infrastructure/Outbox/EfOutboxRepository.cs
  - src/BuildingBlocks/Infrastructure/Outbox/OutboxProcessor.cs

Configuration:
  - src/BuildingBlocks/Application/Configuration/TransactionConfiguration.cs

Tests:
  - tests/BuildingBlocks.Tests/Application/Behaviors/TransactionBehaviorTests.cs
  - tests/BuildingBlocks.Tests/Infrastructure/Outbox/OutboxServiceTests.cs
```

### Implementation Steps

#### Step 1: Transaction Behavior with Outbox

```csharp
public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IAxonRequest<TResponse>
    where TResponse : class
{
    private readonly IWriteDbContext _dbContext;
    private readonly IOutboxService _outboxService;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;
    private readonly TransactionOptions _options;
    
    public TransactionBehavior(
        IWriteDbContext dbContext,
        IOutboxService outboxService,
        ILogger<TransactionBehavior<TRequest, TResponse>> logger,
        IOptions<TransactionOptions> options)
    {
        _dbContext = dbContext;
        _outboxService = outboxService;
        _logger = logger;
        _options = options.Value;
    }
    
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Skip transactions for queries
        if (request is IQuery<TResponse>)
        {
            return await next();
        }
        
        // Skip if already in transaction
        if (_dbContext.Database.CurrentTransaction != null)
        {
            return await next();
        }
        
        var requestName = typeof(TRequest).Name;
        var transactionId = Guid.NewGuid();
        
        _logger.LogDebug("Starting transaction {TransactionId} for {RequestType} (Trace: {TraceId})",
            transactionId, requestName, request.TraceId);
        
        using var activity = Activity.Current?.Source.StartActivity("Transaction");
        activity?.SetTag("transaction.id", transactionId);
        activity?.SetTag("transaction.type", "command");
        activity?.SetTag("request.type", requestName);
        
        // Add metadata to activity
        foreach (var (key, value) in request.Metadata)
        {
            activity?.SetTag($"metadata.{key}", value?.ToString());
        }
        
        var stopwatch = Stopwatch.StartNew();
        
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            GetIsolationLevel(request),
            cancellationToken);
        
        try
        {
            // Create transaction context
            var transactionContext = new TransactionContext(
                transactionId,
                request.TraceId,
                request.RequestId,
                request.Metadata);
            
            // Set transaction context for the scope
            using var scope = TransactionContext.Create(transactionContext);
            
            // Execute the command
            var response = await next();
            
            // Collect and store domain events in outbox
            var domainEvents = CollectDomainEvents();
            if (domainEvents.Any())
            {
                await _outboxService.StoreEventsAsync(
                    domainEvents, 
                    transactionContext, 
                    cancellationToken);
                
                _logger.LogDebug("Stored {EventCount} domain events in outbox for transaction {TransactionId}",
                    domainEvents.Count, transactionId);
            }
            
            // Commit transaction
            await transaction.CommitAsync(cancellationToken);
            stopwatch.Stop();
            
            _logger.LogInformation(
                "Transaction {TransactionId} committed successfully for {RequestType} in {ElapsedMs}ms (Trace: {TraceId})",
                transactionId, requestName, stopwatch.ElapsedMilliseconds, request.TraceId);
            
            activity?.SetStatus(ActivityStatusCode.Ok);
            activity?.SetTag("transaction.duration_ms", stopwatch.ElapsedMilliseconds);
            activity?.SetTag("transaction.events_count", domainEvents.Count);
            
            // Process outbox events asynchronously after commit
            _ = Task.Run(async () =>
            {
                try
                {
                    await _outboxService.ProcessPendingEventsAsync(transactionId, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process outbox events for transaction {TransactionId}", 
                        transactionId);
                }
            }, cancellationToken);
            
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex,
                "Transaction {TransactionId} failed for {RequestType} after {ElapsedMs}ms (Trace: {TraceId})",
                transactionId, requestName, stopwatch.ElapsedMilliseconds, request.TraceId);
            
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
    
    private IsolationLevel GetIsolationLevel(TRequest request)
    {
        // Check metadata for custom isolation level
        if (request.Metadata.TryGetValue("IsolationLevel", out var level) &&
            Enum.TryParse<IsolationLevel>(level.ToString(), out var isolationLevel))
        {
            return isolationLevel;
        }
        
        // Check for high-consistency requirements
        if (request.Metadata.ContainsKey("RequireSerializable"))
        {
            return IsolationLevel.Serializable;
        }
        
        // Default based on tenant or feature flags
        if (request.Metadata.TryGetValue("TenantId", out var tenantId) &&
            tenantId?.ToString() == "high-consistency-tenant")
        {
            return IsolationLevel.RepeatableRead;
        }
        
        return IsolationLevel.ReadCommitted;
    }
    
    private List<IDomainEvent> CollectDomainEvents()
    {
        var entities = _dbContext.ChangeTracker
            .Entries<IAggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();
        
        var events = entities
            .SelectMany(e => e.DomainEvents)
            .ToList();
        
        // Clear events from entities after collection
        foreach (var entity in entities)
        {
            entity.ClearDomainEvents();
        }
        
        return events;
    }
}
```

#### Step 2: Outbox Service Implementation

```csharp
public interface IOutboxService
{
    Task StoreEventsAsync(
        IReadOnlyList<IDomainEvent> events, 
        TransactionContext context,
        CancellationToken cancellationToken = default);
    
    Task ProcessPendingEventsAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default);
    
    Task ProcessAllPendingEventsAsync(CancellationToken cancellationToken = default);
}

public class OutboxService : IOutboxService
{
    private readonly IOutboxRepository _repository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<OutboxService> _logger;
    private readonly OutboxOptions _options;
    
    public OutboxService(
        IOutboxRepository repository,
        IEventPublisher eventPublisher,
        ILogger<OutboxService> logger,
        IOptions<OutboxOptions> options)
    {
        _repository = repository;
        _eventPublisher = eventPublisher;
        _logger = logger;
        _options = options.Value;
    }
    
    public async Task StoreEventsAsync(
        IReadOnlyList<IDomainEvent> events,
        TransactionContext context,
        CancellationToken cancellationToken = default)
    {
        var outboxEntries = events.Select(domainEvent => new OutboxEntry
        {
            Id = Guid.NewGuid(),
            TransactionId = context.TransactionId,
            EventType = domainEvent.GetType().AssemblyQualifiedName!,
            EventData = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
            TraceId = context.TraceId,
            RequestId = context.RequestId,
            TenantId = context.Metadata.GetValueOrDefault("TenantId")?.ToString(),
            CreatedAt = DateTime.UtcNow,
            Status = OutboxEntryStatus.Pending,
            Metadata = JsonSerializer.Serialize(context.Metadata)
        }).ToList();
        
        await _repository.AddAsync(outboxEntries, cancellationToken);
        
        _logger.LogDebug("Stored {EventCount} events in outbox for transaction {TransactionId}",
            events.Count, context.TransactionId);
    }
    
    public async Task ProcessPendingEventsAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        var entries = await _repository.GetPendingByTransactionAsync(transactionId, cancellationToken);
        
        await ProcessEntries(entries, cancellationToken);
    }
    
    public async Task ProcessAllPendingEventsAsync(CancellationToken cancellationToken = default)
    {
        var entries = await _repository.GetPendingAsync(_options.BatchSize, cancellationToken);
        
        await ProcessEntries(entries, cancellationToken);
    }
    
    private async Task ProcessEntries(IReadOnlyList<OutboxEntry> entries, CancellationToken cancellationToken)
    {
        var processingTasks = entries.Select(async entry =>
        {
            using var activity = Activity.Current?.Source.StartActivity("ProcessOutboxEvent");
            activity?.SetTag("event.id", entry.Id);
            activity?.SetTag("event.type", entry.EventType);
            activity?.SetTag("transaction.id", entry.TransactionId);
            activity?.SetTag("trace.id", entry.TraceId);
            
            try
            {
                await ProcessSingleEntry(entry, cancellationToken);
                
                activity?.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.RecordException(ex);
                throw;
            }
        });
        
        // Process events in parallel with controlled concurrency
        var semaphore = new SemaphoreSlim(_options.MaxConcurrency, _options.MaxConcurrency);
        var concurrentTasks = processingTasks.Select(async task =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                await task;
            }
            finally
            {
                semaphore.Release();
            }
        });
        
        await Task.WhenAll(concurrentTasks);
    }
    
    private async Task ProcessSingleEntry(OutboxEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            // Mark as processing
            entry.MarkAsProcessing();
            await _repository.UpdateAsync(entry, cancellationToken);
            
            // Deserialize event
            var eventType = Type.GetType(entry.EventType);
            if (eventType == null)
            {
                throw new InvalidOperationException($"Event type {entry.EventType} not found");
            }
            
            var domainEvent = (IDomainEvent)JsonSerializer.Deserialize(entry.EventData, eventType)!;
            
            // Publish event with retry
            await PublishWithRetry(domainEvent, entry, cancellationToken);
            
            // Mark as completed
            entry.MarkAsCompleted();
            await _repository.UpdateAsync(entry, cancellationToken);
            
            _logger.LogDebug("Successfully processed outbox event {EventId} of type {EventType}",
                entry.Id, entry.EventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process outbox event {EventId} of type {EventType}",
                entry.Id, entry.EventType);
            
            // Mark as failed and increment retry count
            entry.MarkAsFailed(ex.Message);
            await _repository.UpdateAsync(entry, cancellationToken);
            
            // Move to dead letter if max retries exceeded
            if (entry.RetryCount >= _options.MaxRetries)
            {
                entry.MoveToDeadLetter();
                await _repository.UpdateAsync(entry, cancellationToken);
                
                _logger.LogWarning("Moved outbox event {EventId} to dead letter after {RetryCount} retries",
                    entry.Id, entry.RetryCount);
            }
        }
    }
    
    private async Task PublishWithRetry(
        IDomainEvent domainEvent, 
        OutboxEntry entry, 
        CancellationToken cancellationToken)
    {
        var retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Retry {RetryCount} for event {EventId} after {Delay}s",
                        retryCount, entry.Id, timespan.TotalSeconds);
                });
        
        await retryPolicy.ExecuteAsync(async () =>
        {
            await _eventPublisher.PublishAsync(domainEvent, cancellationToken);
        });
    }
}
```

#### Step 3: Outbox Entry Entity

```csharp
public class OutboxEntry
{
    public Guid Id { get; set; }
    public Guid TransactionId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string EventData { get; set; } = string.Empty;
    public string? TraceId { get; set; }
    public Guid RequestId { get; set; }
    public string? TenantId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public OutboxEntryStatus Status { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public string? Metadata { get; set; }
    
    public void MarkAsProcessing()
    {
        Status = OutboxEntryStatus.Processing;
        ProcessedAt = DateTime.UtcNow;
    }
    
    public void MarkAsCompleted()
    {
        Status = OutboxEntryStatus.Completed;
        ProcessedAt = DateTime.UtcNow;
    }
    
    public void MarkAsFailed(string error)
    {
        Status = OutboxEntryStatus.Failed;
        RetryCount++;
        LastError = error;
        NextRetryAt = DateTime.UtcNow.AddMinutes(Math.Pow(2, RetryCount)); // Exponential backoff
    }
    
    public void MoveToDeadLetter()
    {
        Status = OutboxEntryStatus.DeadLetter;
    }
}

public enum OutboxEntryStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
    DeadLetter = 4
}
```

#### Step 4: Configuration and DI Setup

```csharp
public class TransactionOptions
{
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(2);
    public IsolationLevel DefaultIsolationLevel { get; set; } = IsolationLevel.ReadCommitted;
    public bool EnableOutbox { get; set; } = true;
}

public class OutboxOptions
{
    public int BatchSize { get; set; } = 100;
    public int MaxRetries { get; set; } = 3;
    public int MaxConcurrency { get; set; } = Environment.ProcessorCount;
    public TimeSpan ProcessingInterval { get; set; } = TimeSpan.FromSeconds(30);
}

public static class TransactionConfiguration
{
    public static IServiceCollection AddTransactionManagement(
        this IServiceCollection services,
        Action<TransactionOptions>? configureTransaction = null,
        Action<OutboxOptions>? configureOutbox = null)
    {
        // Configure options
        services.Configure<TransactionOptions>(configureTransaction ?? (_ => { }));
        services.Configure<OutboxOptions>(configureOutbox ?? (_ => { }));
        
        // Add services
        services.AddScoped<IOutboxService, OutboxService>();
        services.AddScoped<IOutboxRepository, EfOutboxRepository>();
        
        // Add pipeline behavior
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
        
        // Add background service for processing outbox
        services.AddHostedService<OutboxProcessor>();
        
        return services;
    }
}
```

---

## 🧪 Testing Requirements

### Unit Tests

```csharp
[Fact]
public async Task Should_Start_Transaction_For_Commands_Only()
{
    // Arrange
    var command = new TestCommand();
    var query = new TestQuery();
    
    var dbContextMock = new Mock<IWriteDbContext>();
    var behavior = new TransactionBehavior<IAxonRequest<Result>, Result>(...);
    
    // Act & Assert
    await behavior.Handle(command, next, CancellationToken.None);
    dbContextMock.Verify(x => x.Database.BeginTransactionAsync(It.IsAny<IsolationLevel>(), It.IsAny<CancellationToken>()), Times.Once);
    
    await behavior.Handle(query, next, CancellationToken.None);
    dbContextMock.Verify(x => x.Database.BeginTransactionAsync(It.IsAny<IsolationLevel>(), It.IsAny<CancellationToken>()), Times.Once); // Still once
}

[Fact]
public async Task Should_Store_Domain_Events_In_Outbox()
{
    // Arrange
    var events = new List<IDomainEvent> { new TestDomainEvent() };
    var context = new TransactionContext(Guid.NewGuid(), "trace-123", Guid.NewGuid(), new Dictionary<string, object>());
    
    var outboxService = new Mock<IOutboxService>();
    
    // Act
    await outboxService.Object.StoreEventsAsync(events, context, CancellationToken.None);
    
    // Assert
    outboxService.Verify(x => x.StoreEventsAsync(
        It.Is<IReadOnlyList<IDomainEvent>>(e => e.Count == 1),
        It.IsAny<TransactionContext>(),
        It.IsAny<CancellationToken>()), Times.Once);
}
```

### Integration Tests

```csharp
[Fact]
public async Task Should_Commit_Transaction_And_Process_Outbox_Events()
{
    // Arrange
    using var scope = _serviceProvider.CreateScope();
    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
    var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
    
    var command = new CreateUserCommand("test@example.com");
    
    // Act
    var result = await mediator.Send(command);
    
    // Assert
    result.IsSuccess.Should().BeTrue();
    
    // Verify data was saved
    var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == "test@example.com");
    user.Should().NotBeNull();
    
    // Verify outbox entry was created
    var outboxEntries = await dbContext.OutboxEntries.ToListAsync();
    outboxEntries.Should().HaveCountGreaterThan(0);
}
```

---

## 📐 Architecture Considerations

### Transaction Scope Design

1. **Command Boundary**: Transactions wrap entire command execution
2. **Event Consistency**: Events stored in same transaction as data changes
3. **Compensation**: Support for saga pattern rollback scenarios
4. **Nested Transactions**: Handle composition scenarios correctly

### Outbox Pattern Benefits

- **Atomicity**: Events stored atomically with business data
- **Reliability**: At-least-once delivery guarantee
- **Decoupling**: Event publishing decoupled from business logic
- **Ordering**: Events processed in correct order per aggregate

### Performance Considerations

```csharp
// Batch processing for better throughput
var entries = await _repository.GetPendingAsync(batchSize: 100);
await Task.WhenAll(entries.Select(ProcessEntry));

// Connection pooling optimization
services.AddDbContextPool<AppDbContext>(options => 
    options.UseSqlServer(connectionString), poolSize: 100);
```

---

## 📦 Definition of Done

- [ ] TransactionBehavior with automatic transaction management
- [ ] Outbox pattern implementation with reliable event publishing
- [ ] Metadata-aware transaction configuration
- [ ] Background service for outbox processing
- [ ] Unit tests with 100% coverage
- [ ] Integration tests with real database
- [ ] Performance benchmarks documented
- [ ] Dead letter queue handling
- [ ] Monitoring and alerting setup

---

## 🔄 Migration Strategy

### Phase 1: Infrastructure Setup
- Deploy outbox table schema
- Configure background processing service
- Set up monitoring and alerts

### Phase 2: Gradual Rollout
```csharp
// Existing commands automatically get transaction support
public class CreateUserHandler : ICommandHandler<CreateUserCommand, UserId>
{
    // No changes needed - transaction applied via pipeline
}
```

### Phase 3: Advanced Features
- Implement saga pattern support
- Add distributed transaction coordination
- Optimize performance based on metrics

---

## 📊 Success Metrics

- 100% transaction success rate for commands
- Event publishing reliability > 99.9%
- Average transaction duration < 50ms
- Zero data consistency issues in production

---

## 🚀 Follow-up Stories

1. **Story 07**: Saga Pattern Implementation
2. **Story 08**: Distributed Transaction Coordination
3. **Story 09**: Event Sourcing Integration
4. **Story 10**: Performance Monitoring Dashboard