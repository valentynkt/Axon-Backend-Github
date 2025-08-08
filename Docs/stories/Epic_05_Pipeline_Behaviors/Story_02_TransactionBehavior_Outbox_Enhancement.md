# Story 02: TransactionBehavior - Outbox Pattern Enhancement

## Story Overview
**Story ID**: Epic_05_Story_02  
**Story Name**: TransactionBehavior - Outbox Pattern Integration  
**Estimated Duration**: 4 hours (brownfield enhancement)  
**Dependencies**: 
- Existing TransactionBehavior in `src/BuildingBlocks/Application/Behaviors/`
- Epic_04 (CQRS Foundation)

## User Story
**As a developer**, I want the TransactionBehavior to trigger outbox processing after successful commits, so that domain events are reliably processed even when the outbox processor service is temporarily unavailable.

## Acceptance Criteria
- [ ] IOutboxProcessor integration added to existing TransactionBehavior
- [ ] Fire-and-forget outbox triggering after successful commit
- [ ] Proper error logging without blocking main request
- [ ] Optional configuration for processing delay
- [ ] Existing functionality remains unchanged
- [ ] All existing tests continue to pass
- [ ] New integration tests for outbox triggering

## Technical Implementation

### Integration Points

#### 1. Enhanced TransactionBehavior
```csharp
namespace Axon.BuildingBlocks.Application.Behaviors;

public sealed class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>  // Only for commands
    where TResponse : IResult
{
    // Existing dependencies...
    private readonly IOutboxProcessor? _outboxProcessor;  // NEW: Optional dependency
    private readonly IOptions<TransactionOptions> _options;  // NEW: Configuration
    
    public TransactionBehavior(
        IDbContext dbContext,
        ILogger<TransactionBehavior<TRequest, TResponse>> logger,
        IOutboxProcessor? outboxProcessor = null,  // Optional
        IOptions<TransactionOptions>? options = null)
    {
        // Constructor implementation
    }
}
```

#### 2. Outbox Processor Interface
```csharp
namespace Axon.BuildingBlocks.Application.Outbox;

public interface IOutboxProcessor
{
    Task ProcessPendingAsync(CancellationToken cancellationToken = default);
    Task ProcessPendingAsync(string aggregateId, CancellationToken cancellationToken = default);
}
```

### Tasks

#### Task 1: Add IOutboxProcessor Dependency
- [ ] Add optional IOutboxProcessor parameter to constructor
- [ ] Add IOptions<TransactionOptions> for configuration
- [ ] Maintain backward compatibility (optional parameters)
- [ ] Update dependency injection registration

#### Task 2: Implement Outbox Triggering
- [ ] Locate successful commit point in Handle method
- [ ] Add fire-and-forget Task.Run for outbox processing
- [ ] Include configurable delay (default 100ms)
- [ ] Ensure non-blocking execution

#### Task 3: Add Error Handling
- [ ] Wrap outbox trigger in try-catch
- [ ] Use separate logger scope for clarity
- [ ] Log errors at Error level with full exception
- [ ] Never throw or propagate outbox errors

#### Task 4: Configuration Support
- [ ] Create TransactionOptions configuration class
- [ ] Add OutboxProcessingDelay property (TimeSpan)
- [ ] Add EnableOutboxProcessing flag (bool)
- [ ] Configure via appsettings.json

#### Task 5: Update Existing Tests
- [ ] Verify existing tests still pass
- [ ] Mock IOutboxProcessor in existing tests (null is fine)
- [ ] Ensure no breaking changes

#### Task 6: Add New Tests
- [ ] Test outbox triggering after successful commit
- [ ] Test that outbox errors don't fail main request
- [ ] Test configuration options
- [ ] Test with null outbox processor (graceful handling)

## Code Changes

### Modified Handle Method
```csharp
public async Task<TResponse> Handle(
    TRequest request,
    RequestHandlerDelegate<TResponse> next,
    CancellationToken cancellationToken)
{
    // Existing transaction logic...
    
    await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
    
    try
    {
        var response = await next();
        
        if (response.IsSuccess)
        {
            await transaction.CommitAsync(cancellationToken);
            
            // NEW: Trigger outbox processing
            TriggerOutboxProcessing();
        }
        else
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        
        return response;
    }
    catch
    {
        await transaction.RollbackAsync(cancellationToken);
        throw;
    }
}

private void TriggerOutboxProcessing()
{
    if (_outboxProcessor is null || !_options.Value.EnableOutboxProcessing)
        return;
        
    _ = Task.Run(async () =>
    {
        try
        {
            // Optional delay for transaction visibility
            if (_options.Value.OutboxProcessingDelay > TimeSpan.Zero)
            {
                await Task.Delay(_options.Value.OutboxProcessingDelay);
            }
            
            await _outboxProcessor.ProcessPendingAsync();
        }
        catch (Exception ex)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["Operation"] = "OutboxProcessingTrigger"
            }))
            {
                _logger.LogError(ex, "In-process outbox processing trigger failed after commit");
            }
        }
    });
}
```

## Definition of Done
- [ ] IOutboxProcessor integrated without breaking changes
- [ ] Fire-and-forget processing implemented correctly
- [ ] Error handling prevents main request failures
- [ ] All existing tests pass
- [ ] New tests validate outbox triggering
- [ ] Configuration options work as expected
- [ ] Code follows project conventions
- [ ] Performance impact negligible (< 1ms)

## Technical Notes

### Fire-and-Forget Pattern
- Use Task.Run to avoid blocking main request
- Don't await the task (intentionally fire-and-forget)
- Separate error handling to prevent propagation
- Consider using IHostedService for production scenarios

### Transaction Visibility
- 100ms delay ensures database transaction is visible
- Prevents "phantom read" issues in outbox processor
- Configurable per environment needs

### Performance Considerations
- Fire-and-forget adds < 1ms overhead
- Outbox processing happens asynchronously
- No impact on request latency
- Memory usage minimal (single Task allocation)

## Dependencies
- No new NuGet packages required
- Uses existing Entity Framework Core transaction support
- Compatible with existing DI container configuration

## Risk Assessment
- **Risk**: Outbox errors could flood logs
- **Mitigation**: Rate limiting in logger, separate log scope
- **Risk**: Memory leak from uncompleted tasks
- **Mitigation**: Tasks are short-lived, proper disposal

## References
- Outbox Pattern: https://microservices.io/patterns/data/transactional-outbox.html
- Existing TransactionBehavior: `src/BuildingBlocks/Application/Behaviors/TransactionBehavior.cs`