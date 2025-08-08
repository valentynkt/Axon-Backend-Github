# Story 05: Cross-Module Event Propagation

## Story Overview

**Story ID**: Epic_06_Story_05  
**Story Name**: Cross-Module Event Propagation  
**Epic**: Epic 06 - Event System Enhancement & Integration  
**Priority**: P1 - High  
**Estimated Duration**: 8 hours  
**Dependencies**: Epic_06_Story_01 (Outbox Processor), Epic_06_Story_02 (Integration Publisher)

## User Story

**As a developer**, I want events to propagate between modules so that bounded contexts can react to events from other modules while maintaining loose coupling and proper module boundaries.

## Current State Analysis

### ✅ What Exists Today
- **Domain Events**: Working within individual modules (Chat, Identity)
- **Event Mapping**: `CompositeEventMapper` supports domain→integration transformations
- **Module Boundaries**: Clear separation between Chat and Identity modules
- **Event Infrastructure**: Solid foundation from previous stories (outbox, publishing, schema)

### ❌ What's Missing
- **Cross-Module Communication**: No mechanism for inter-module event handling
- **Event Bus**: No shared event bus for module communication
- **Event Routing**: No configuration-driven event routing between modules
- **Module Isolation**: No security/access control for cross-module events
- **Event Discovery**: No way for modules to discover available events from other modules

### 🔍 Current Module Structure Analysis
```
src/Modules/
├── Chat/                    # Chat bounded context
│   ├── Domain/Events/       # ConversationStartedDomainEvent, etc.
│   └── Application/         # Event handlers within Chat module
├── Identity/                # Identity bounded context  
│   ├── Domain/Events/       # UserRegisteredDomainEvent, etc.
│   └── Application/         # Event handlers within Identity module
└── [Future Modules]/        # Additional bounded contexts
```

## Acceptance Criteria

### Event Propagation Requirements
- [ ] `IInterModuleEventBus` enables modules to subscribe to events from other modules
- [ ] Event routing configuration defines which events cross module boundaries
- [ ] Module event handlers can subscribe to external module events through standard patterns
- [ ] Event propagation maintains strong typing and compile-time safety

### Module Isolation Requirements  
- [ ] Module boundary enforcement prevents unauthorized access to internal events
- [ ] Event contracts explicitly define public events available to other modules
- [ ] Module event subscriptions configurable and discoverable
- [ ] Event access control based on module permissions and policies

### Performance & Reliability Requirements
- [ ] Cross-module event propagation supports both synchronous and asynchronous processing
- [ ] Event ordering preserved within aggregate boundaries across modules
- [ ] Failed cross-module event processing doesn't impact source module
- [ ] Event propagation latency < 200ms p95 for synchronous scenarios

### Development Experience Requirements
- [ ] Clear patterns for defining public events vs internal events
- [ ] Module event discovery tooling for development and documentation
- [ ] Type-safe event subscription with compile-time validation
- [ ] Comprehensive tracing for cross-module event flows

## Technical Implementation

### 1. Inter-Module Event Bus Interface

**Location**: `src/BuildingBlocks/Core/Abstractions/Events/IInterModuleEventBus.cs`

```csharp
namespace Axon.BuildingBlocks.Core.Abstractions.Events;

/// <summary>
/// Event bus for cross-module communication while maintaining module boundaries
/// Enables modules to publish and subscribe to events from other bounded contexts
/// </summary>
public interface IInterModuleEventBus
{
    /// <summary>
    /// Publish module event for cross-module consumption
    /// Only events marked as public can be published across modules
    /// </summary>
    Task<Result<Unit>> PublishAsync<TEvent>(
        TEvent moduleEvent,
        ModuleEventContext context,
        CancellationToken cancellationToken = default)
        where TEvent : IModuleEvent;

    /// <summary>
    /// Publish batch of module events for performance
    /// </summary>
    Task<Result<BatchPublishResult>> PublishBatchAsync<TEvent>(
        IEnumerable<TEvent> moduleEvents,
        ModuleEventContext context,
        CancellationToken cancellationToken = default)
        where TEvent : IModuleEvent;

    /// <summary>
    /// Subscribe to module events from other bounded contexts
    /// Returns subscription handle for lifecycle management
    /// </summary>
    Task<Result<IModuleEventSubscription>> SubscribeAsync<TEvent>(
        IModuleEventHandler<TEvent> handler,
        ModuleSubscriptionOptions options,
        CancellationToken cancellationToken = default)
        where TEvent : IModuleEvent;

    /// <summary>
    /// Get available module events for discovery and documentation
    /// </summary>
    Task<Result<IReadOnlyList<ModuleEventDescriptor>>> GetAvailableEventsAsync(
        string? sourceModule = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current subscription status and health
    /// </summary>
    Task<Result<InterModuleEventBusHealth>> GetHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Marker interface for events that can cross module boundaries
/// Must be explicitly implemented to make events available to other modules
/// </summary>
public interface IModuleEvent : IIntegrationEvent
{
    /// <summary>
    /// Source module that published this event
    /// </summary>
    string SourceModule { get; }

    /// <summary>
    /// Event contract version for compatibility
    /// </summary>
    string ContractVersion { get; }

    /// <summary>
    /// Visibility level for access control
    /// </summary>
    ModuleEventVisibility Visibility { get; }
}

/// <summary>
/// Event visibility levels for module boundary control
/// </summary>
public enum ModuleEventVisibility
{
    /// <summary>
    /// Internal to the module only - cannot cross boundaries
    /// </summary>
    Internal,

    /// <summary>
    /// Available to all modules within the same application
    /// </summary>
    Public,

    /// <summary>
    /// Available to explicitly configured modules only
    /// </summary>
    Restricted
}

/// <summary>
/// Context information for module event publication
/// </summary>
public sealed record ModuleEventContext
{
    public required string SourceModule { get; init; }
    public required string CorrelationId { get; init; }
    public string? CausationId { get; init; }
    public string? UserId { get; init; }
    public string? TenantId { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }

    public static ModuleEventContext Create(string sourceModule, string correlationId) =>
        new() { SourceModule = sourceModule, CorrelationId = correlationId };
}

/// <summary>
/// Handler interface for processing cross-module events
/// </summary>
public interface IModuleEventHandler<in TEvent> where TEvent : IModuleEvent
{
    /// <summary>
    /// Handle cross-module event with full context
    /// </summary>
    Task<Result<Unit>> HandleAsync(
        TEvent moduleEvent,
        ModuleEventContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Module that owns this handler (for access control)
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Priority for handler execution ordering
    /// </summary>
    int Priority => 0;
}

/// <summary>
/// Options for module event subscriptions
/// </summary>
public sealed record ModuleSubscriptionOptions
{
    public required string SubscriberModule { get; init; }
    public ExecutionMode Mode { get; init; } = ExecutionMode.Asynchronous;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public int MaxRetryAttempts { get; init; } = 3;
    public bool EnableDeadLetterQueue { get; init; } = true;
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Execution modes for cross-module event handling
/// </summary>
public enum ExecutionMode
{
    /// <summary>
    /// Handle event asynchronously (recommended)
    /// </summary>
    Asynchronous,

    /// <summary>
    /// Handle event synchronously within transaction
    /// </summary>
    Synchronous,

    /// <summary>
    /// Handle event in background task
    /// </summary>
    FireAndForget
}

/// <summary>
/// Handle for managing module event subscriptions
/// </summary>
public interface IModuleEventSubscription : IAsyncDisposable
{
    Guid Id { get; }
    string EventType { get; }
    string SubscriberModule { get; }
    DateTime CreatedAtUtc { get; }
    bool IsActive { get; }
    
    Task<Result<Unit>> ActivateAsync(CancellationToken cancellationToken = default);
    Task<Result<Unit>> DeactivateAsync(CancellationToken cancellationToken = default);
    Task<Result<SubscriptionHealth>> GetHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Description of available module events for discovery
/// </summary>
public sealed record ModuleEventDescriptor
{
    public required string EventType { get; init; }
    public required string SourceModule { get; init; }
    public required string ContractVersion { get; init; }
    public required ModuleEventVisibility Visibility { get; init; }
    public string? Description { get; init; }
    public string? JsonSchema { get; init; }
    public IReadOnlyList<string> AllowedSubscribers { get; init; } = Array.Empty<string>();
    public DateTime PublishedAtUtc { get; init; }
}

/// <summary>
/// Health information for inter-module event bus
/// </summary>
public sealed record InterModuleEventBusHealth
{
    public bool IsHealthy { get; init; }
    public int ActiveSubscriptions { get; init; }
    public int TotalEventsPublishedToday { get; init; }
    public int TotalEventsHandledToday { get; init; }
    public TimeSpan AverageEventLatency { get; init; }
    public int FailedEventsToday { get; init; }
    public Dictionary<string, ModuleHealth> ModuleHealth { get; init; } = new();
}

/// <summary>
/// Health information per module
/// </summary>
public sealed record ModuleHealth
{
    public string ModuleName { get; init; } = default!;
    public bool IsActive { get; init; }
    public int PublishedEvents { get; init; }
    public int HandledEvents { get; init; }
    public int FailedEvents { get; init; }
    public DateTime LastActivityUtc { get; init; }
}

/// <summary>
/// Health information for event subscription
/// </summary>
public sealed record SubscriptionHealth
{
    public bool IsHealthy { get; init; }
    public int EventsHandledToday { get; init; }
    public int FailedEventsToday { get; init; }
    public TimeSpan AverageHandlingTime { get; init; }
    public DateTime LastEventHandledUtc { get; init; }
    public string? LastError { get; init; }
}
```

### 2. Module Event Base Classes

**Location**: `src/BuildingBlocks/Core/Events/ModuleEventBase.cs`

```csharp
namespace Axon.BuildingBlocks.Core.Events;

/// <summary>
/// Base class for events that can cross module boundaries
/// Provides common functionality for inter-module communication
/// </summary>
public abstract record ModuleEventBase : IntegrationEventBase, IModuleEvent
{
    /// <summary>
    /// Source module that published this event (automatically set)
    /// </summary>
    public string SourceModule { get; init; } = default!;

    /// <summary>
    /// Event contract version for compatibility (default: "1.0")
    /// </summary>
    public virtual string ContractVersion { get; init; } = "1.0";

    /// <summary>
    /// Event visibility level (default: Public)
    /// </summary>
    public virtual ModuleEventVisibility Visibility { get; init; } = ModuleEventVisibility.Public;

    protected ModuleEventBase(string sourceModule) : base()
    {
        SourceModule = sourceModule ?? throw new ArgumentNullException(nameof(sourceModule));
    }

    protected ModuleEventBase(Guid eventId, DateTime occurredAt, string sourceModule, int version = 1) 
        : base(eventId, occurredAt, version)
    {
        SourceModule = sourceModule ?? throw new ArgumentNullException(nameof(sourceModule));
    }
}

/// <summary>
/// Base class for restricted module events with explicit access control
/// </summary>
public abstract record RestrictedModuleEventBase : ModuleEventBase
{
    /// <summary>
    /// Modules allowed to subscribe to this event
    /// </summary>
    public virtual IReadOnlyList<string> AllowedSubscribers { get; init; } = Array.Empty<string>();

    public override ModuleEventVisibility Visibility { get; init; } = ModuleEventVisibility.Restricted;

    protected RestrictedModuleEventBase(string sourceModule, IReadOnlyList<string> allowedSubscribers) 
        : base(sourceModule)
    {
        AllowedSubscribers = allowedSubscribers ?? throw new ArgumentNullException(nameof(allowedSubscribers));
    }
}
```

### 3. Inter-Module Event Bus Implementation

**Location**: `src/BuildingBlocks/Infrastructure/Events/InterModuleEventBus.cs`

```csharp
namespace Axon.BuildingBlocks.Infrastructure.Events;

/// <summary>
/// Implementation of inter-module event bus using MediatR and outbox pattern
/// Provides reliable cross-module communication with proper isolation
/// </summary>
public sealed class InterModuleEventBus : IInterModuleEventBus
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IMediator _mediator;
    private readonly IOptions<InterModuleEventOptions> _options;
    private readonly ILogger<InterModuleEventBus> _logger;
    private readonly ConcurrentDictionary<Guid, ModuleEventSubscription> _subscriptions = new();
    private readonly ConcurrentDictionary<string, ModuleEventDescriptor> _eventRegistry = new();

    public InterModuleEventBus(
        IServiceScopeFactory serviceScopeFactory,
        IMediator mediator,
        IOptions<InterModuleEventOptions> options,
        ILogger<InterModuleEventBus> logger)
    {
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<Unit>> PublishAsync<TEvent>(
        TEvent moduleEvent,
        ModuleEventContext context,
        CancellationToken cancellationToken = default)
        where TEvent : IModuleEvent
    {
        try
        {
            // Validate event can be published cross-module
            var validationResult = ValidateModuleEvent(moduleEvent, context);
            if (validationResult.IsFailure)
                return validationResult;

            // Register event in discovery registry
            await RegisterEventForDiscoveryAsync(moduleEvent, cancellationToken);

            // Find subscribers for this event type
            var subscribers = await GetEventSubscribersAsync<TEvent>(cancellationToken);
            
            if (!subscribers.Any())
            {
                _logger.LogDebug(
                    "No subscribers found for module event {EventType} from {SourceModule}",
                    typeof(TEvent).Name, context.SourceModule);
                return Result<Unit>.Success(Unit.Value);
            }

            // Publish to subscribers based on their execution mode
            var publishTasks = subscribers.Select(subscription => 
                PublishToSubscriberAsync(moduleEvent, context, subscription, cancellationToken));

            await Task.WhenAll(publishTasks);

            _logger.LogInformation(
                "Published module event {EventType} from {SourceModule} to {SubscriberCount} subscribers",
                typeof(TEvent).Name, context.SourceModule, subscribers.Count);

            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            var error = Error.Failure("MODULE_EVENT_001", "Failed to publish module event")
                .WithMetadata("EventType", typeof(TEvent).Name)
                .WithMetadata("SourceModule", context.SourceModule)
                .WithMetadata("Exception", ex.Message);

            _logger.LogError(ex, "Error publishing module event {EventType} from {SourceModule}",
                typeof(TEvent).Name, context.SourceModule);

            return Result<Unit>.Failure(error);
        }
    }

    public async Task<Result<IModuleEventSubscription>> SubscribeAsync<TEvent>(
        IModuleEventHandler<TEvent> handler,
        ModuleSubscriptionOptions options,
        CancellationToken cancellationToken = default)
        where TEvent : IModuleEvent
    {
        try
        {
            // Validate subscription permissions
            var validationResult = await ValidateSubscriptionAsync<TEvent>(options, cancellationToken);
            if (validationResult.IsFailure)
                return Result<IModuleEventSubscription>.Failure(validationResult.Error);

            // Create subscription
            var subscription = new ModuleEventSubscription(
                id: Guid.NewGuid(),
                eventType: typeof(TEvent).Name,
                subscriberModule: options.SubscriberModule,
                handler: handler,
                options: options);

            // Register subscription
            _subscriptions.TryAdd(subscription.Id, subscription);

            // Activate subscription
            var activationResult = await subscription.ActivateAsync(cancellationToken);
            if (activationResult.IsFailure)
            {
                _subscriptions.TryRemove(subscription.Id, out _);
                return Result<IModuleEventSubscription>.Failure(activationResult.Error);
            }

            _logger.LogInformation(
                "Created module event subscription for {EventType} by {SubscriberModule}",
                typeof(TEvent).Name, options.SubscriberModule);

            return Result<IModuleEventSubscription>.Success(subscription);
        }
        catch (Exception ex)
        {
            var error = Error.Failure("MODULE_EVENT_002", "Failed to create module event subscription")
                .WithMetadata("EventType", typeof(TEvent).Name)
                .WithMetadata("SubscriberModule", options.SubscriberModule)
                .WithMetadata("Exception", ex.Message);

            _logger.LogError(ex, "Error creating subscription for {EventType} by {SubscriberModule}",
                typeof(TEvent).Name, options.SubscriberModule);

            return Result<IModuleEventSubscription>.Failure(error);
        }
    }

    public async Task<Result<IReadOnlyList<ModuleEventDescriptor>>> GetAvailableEventsAsync(
        string? sourceModule = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var events = _eventRegistry.Values.AsEnumerable();

            if (!string.IsNullOrEmpty(sourceModule))
            {
                events = events.Where(e => e.SourceModule.Equals(sourceModule, StringComparison.OrdinalIgnoreCase));
            }

            var result = events
                .OrderBy(e => e.SourceModule)
                .ThenBy(e => e.EventType)
                .ToList();

            return Result<IReadOnlyList<ModuleEventDescriptor>>.Success(result);
        }
        catch (Exception ex)
        {
            var error = Error.Failure("MODULE_EVENT_003", "Failed to retrieve available module events")
                .WithMetadata("SourceModule", sourceModule ?? "All")
                .WithMetadata("Exception", ex.Message);

            return Result<IReadOnlyList<ModuleEventDescriptor>>.Failure(error);
        }
    }

    private Result<Unit> ValidateModuleEvent<TEvent>(TEvent moduleEvent, ModuleEventContext context)
        where TEvent : IModuleEvent
    {
        // Check if event is public or has proper permissions
        if (moduleEvent.Visibility == ModuleEventVisibility.Internal)
        {
            return Result<Unit>.Failure(
                Error.Forbidden("MODULE_EVENT_004", "Internal events cannot be published across modules")
                    .WithMetadata("EventType", typeof(TEvent).Name)
                    .WithMetadata("SourceModule", context.SourceModule));
        }

        // Validate source module matches context
        if (!moduleEvent.SourceModule.Equals(context.SourceModule, StringComparison.OrdinalIgnoreCase))
        {
            return Result<Unit>.Failure(
                Error.Validation("MODULE_EVENT_005", "Event source module must match context source module")
                    .WithMetadata("EventSourceModule", moduleEvent.SourceModule)
                    .WithMetadata("ContextSourceModule", context.SourceModule));
        }

        return Result<Unit>.Success(Unit.Value);
    }

    private async Task<Result<Unit>> ValidateSubscriptionAsync<TEvent>(
        ModuleSubscriptionOptions options,
        CancellationToken cancellationToken)
        where TEvent : IModuleEvent
    {
        // Check if module is allowed to subscribe based on configuration
        var eventType = typeof(TEvent).Name;
        
        if (_options.Value.ModuleAccessControl.ContainsKey(eventType))
        {
            var allowedModules = _options.Value.ModuleAccessControl[eventType];
            if (!allowedModules.Contains(options.SubscriberModule))
            {
                return Result<Unit>.Failure(
                    Error.Forbidden("MODULE_EVENT_006", "Module not authorized to subscribe to this event type")
                        .WithMetadata("EventType", eventType)
                        .WithMetadata("SubscriberModule", options.SubscriberModule));
            }
        }

        return Result<Unit>.Success(Unit.Value);
    }

    private async Task<List<ModuleEventSubscription>> GetEventSubscribersAsync<TEvent>(
        CancellationToken cancellationToken)
        where TEvent : IModuleEvent
    {
        var eventType = typeof(TEvent).Name;
        
        return _subscriptions.Values
            .Where(s => s.EventType == eventType && s.IsActive)
            .ToList();
    }

    private async Task PublishToSubscriberAsync<TEvent>(
        TEvent moduleEvent,
        ModuleEventContext context,
        ModuleEventSubscription subscription,
        CancellationToken cancellationToken)
        where TEvent : IModuleEvent
    {
        try
        {
            switch (subscription.Options.Mode)
            {
                case ExecutionMode.Synchronous:
                    await HandleEventSynchronouslyAsync(moduleEvent, context, subscription, cancellationToken);
                    break;
                    
                case ExecutionMode.Asynchronous:
                    await HandleEventAsynchronouslyAsync(moduleEvent, context, subscription, cancellationToken);
                    break;
                    
                case ExecutionMode.FireAndForget:
                    _ = Task.Run(() => HandleEventAsynchronouslyAsync(moduleEvent, context, subscription, CancellationToken.None));
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing to subscriber {SubscriberModule} for event {EventType}",
                subscription.SubscriberModule, typeof(TEvent).Name);
        }
    }

    private async Task HandleEventSynchronouslyAsync<TEvent>(
        TEvent moduleEvent,
        ModuleEventContext context,
        ModuleEventSubscription subscription,
        CancellationToken cancellationToken)
        where TEvent : IModuleEvent
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(subscription.Options.Timeout);

        var handler = (IModuleEventHandler<TEvent>)subscription.Handler;
        var result = await handler.HandleAsync(moduleEvent, context, timeoutCts.Token);

        if (result.IsFailure)
        {
            _logger.LogError(
                "Synchronous handling failed for {EventType} by {SubscriberModule}: {Error}",
                typeof(TEvent).Name, subscription.SubscriberModule, result.Error.Message);
        }
    }

    private async Task HandleEventAsynchronouslyAsync<TEvent>(
        TEvent moduleEvent,
        ModuleEventContext context,
        ModuleEventSubscription subscription,
        CancellationToken cancellationToken)
        where TEvent : IModuleEvent
    {
        var retryCount = 0;
        
        while (retryCount <= subscription.Options.MaxRetryAttempts)
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(subscription.Options.Timeout);

                var handler = (IModuleEventHandler<TEvent>)subscription.Handler;
                var result = await handler.HandleAsync(moduleEvent, context, timeoutCts.Token);

                if (result.IsSuccess)
                {
                    return; // Success - exit retry loop
                }

                _logger.LogWarning(
                    "Handling failed for {EventType} by {SubscriberModule} (attempt {Attempt}/{MaxAttempts}): {Error}",
                    typeof(TEvent).Name, subscription.SubscriberModule, retryCount + 1, 
                    subscription.Options.MaxRetryAttempts + 1, result.Error.Message);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Handling timeout for {EventType} by {SubscriberModule} (attempt {Attempt}/{MaxAttempts})",
                    typeof(TEvent).Name, subscription.SubscriberModule, retryCount + 1, 
                    subscription.Options.MaxRetryAttempts + 1);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Handling exception for {EventType} by {SubscriberModule} (attempt {Attempt}/{MaxAttempts})",
                    typeof(TEvent).Name, subscription.SubscriberModule, retryCount + 1, 
                    subscription.Options.MaxRetryAttempts + 1);
            }

            retryCount++;
            
            if (retryCount <= subscription.Options.MaxRetryAttempts)
            {
                // Exponential backoff
                var delay = TimeSpan.FromMilliseconds(Math.Pow(2, retryCount) * 100);
                await Task.Delay(delay, cancellationToken);
            }
        }

        // All retries exhausted - send to dead letter queue if enabled
        if (subscription.Options.EnableDeadLetterQueue)
        {
            await SendToDeadLetterQueueAsync(moduleEvent, context, subscription, cancellationToken);
        }
    }

    // Additional helper methods implementation...
}
```

### 4. Example Module Event Definitions

**Location**: `src/Modules/Identity/Application/Events/UserRegisteredModuleEvent.cs`

```csharp
namespace Axon.Modules.Identity.Application.Events;

/// <summary>
/// Cross-module event fired when a new user is registered
/// Available to other modules for integration purposes
/// </summary>
public sealed record UserRegisteredModuleEvent : ModuleEventBase
{
    public UserId UserId { get; init; }
    public string Email { get; init; } = default!;
    public string Name { get; init; } = default!;
    public DateTime RegisteredAtUtc { get; init; }

    public UserRegisteredModuleEvent() : base("Identity") { }

    public UserRegisteredModuleEvent(
        UserId userId,
        string email,
        string name,
        DateTime registeredAtUtc) : base("Identity")
    {
        UserId = userId;
        Email = email;
        Name = name;
        RegisteredAtUtc = registeredAtUtc;
    }
}
```

**Location**: `src/Modules/Chat/Application/Events/ConversationStartedModuleEvent.cs`

```csharp
namespace Axon.Modules.Chat.Application.Events;

/// <summary>
/// Cross-module event fired when a conversation is started
/// Restricted to specific modules for security
/// </summary>
public sealed record ConversationStartedModuleEvent : RestrictedModuleEventBase
{
    public ConversationId ConversationId { get; init; }
    public UserId OwnerId { get; init; }
    public string Title { get; init; } = default!;
    public DateTime StartedAtUtc { get; init; }

    public ConversationStartedModuleEvent() 
        : base("Chat", new[] { "Identity", "Notifications" }) { }

    public ConversationStartedModuleEvent(
        ConversationId conversationId,
        UserId ownerId,
        string title,
        DateTime startedAtUtc) 
        : base("Chat", new[] { "Identity", "Notifications" })
    {
        ConversationId = conversationId;
        OwnerId = ownerId;
        Title = title;
        StartedAtUtc = startedAtUtc;
    }
}
```

### 5. Configuration Options

**Location**: `src/BuildingBlocks/Infrastructure/Events/InterModuleEventOptions.cs`

```csharp
namespace Axon.BuildingBlocks.Infrastructure.Events;

/// <summary>
/// Configuration options for inter-module event system
/// </summary>
public sealed class InterModuleEventOptions
{
    public const string ConfigurationSection = "InterModuleEvents";
    
    /// <summary>
    /// Enable cross-module event propagation
    /// Default: true
    /// </summary>
    public bool EnableCrossModuleEvents { get; set; } = true;
    
    /// <summary>
    /// Default timeout for synchronous cross-module event handling
    /// Default: 30 seconds
    /// </summary>
    public TimeSpan DefaultSynchronousTimeout { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Default retry attempts for failed event handling
    /// Default: 3
    /// </summary>
    public int DefaultMaxRetryAttempts { get; set; } = 3;
    
    /// <summary>
    /// Enable automatic event discovery and registration
    /// Default: true
    /// </summary>
    public bool EnableEventDiscovery { get; set; } = true;
    
    /// <summary>
    /// Access control rules for module event subscriptions
    /// Key: EventType, Value: List of allowed subscriber modules
    /// </summary>
    public Dictionary<string, List<string>> ModuleAccessControl { get; set; } = new();
    
    /// <summary>
    /// Enable dead letter queue for failed cross-module events
    /// Default: true
    /// </summary>
    public bool EnableDeadLetterQueue { get; set; } = true;
    
    /// <summary>
    /// Module event processing metrics collection
    /// Default: true
    /// </summary>
    public bool EnableMetrics { get; set; } = true;
}
```

## Tasks Breakdown

### Phase 1: Core Infrastructure (4 hours)
- [ ] Create `IInterModuleEventBus` interface with comprehensive event handling
- [ ] Design module event base classes with visibility and access control
- [ ] Create subscription management and handler interfaces
- [ ] Implement event discovery and registry mechanisms

### Phase 2: Event Bus Implementation (3 hours)
- [ ] Implement `InterModuleEventBus` with MediatR integration
- [ ] Add event routing and subscription management
- [ ] Implement synchronous and asynchronous execution modes
- [ ] Add retry logic and dead letter queue support

### Phase 3: Integration & Examples (1 hour)
- [ ] Create example module events for Identity and Chat modules
- [ ] Add configuration options and access control rules
- [ ] Integrate with existing outbox and publishing infrastructure
- [ ] Write comprehensive tests for cross-module scenarios

## Definition of Done

### Functionality
- [ ] Modules can publish and subscribe to events from other modules
- [ ] Event access control enforces module boundary security
- [ ] Both synchronous and asynchronous cross-module event processing supported
- [ ] Event discovery mechanism allows modules to find available events

### Quality
- [ ] All operations return `Result<T>` following Epic_03 error patterns
- [ ] Type-safe event subscriptions with compile-time validation
- [ ] Comprehensive tracing for cross-module event flows
- [ ] Unit tests cover subscription management and event routing

### Operations
- [ ] Configuration allows fine-grained control over module access
- [ ] Health checks report inter-module event bus status
- [ ] Performance metrics track cross-module event latency and success rates
- [ ] Dead letter queue handles failed cross-module event processing

## Success Criteria

### Technical Success
- Cross-module events delivered with < 200ms latency p95 for synchronous processing
- Event access control prevents 100% of unauthorized cross-module subscriptions
- Module event discovery provides complete visibility into available events
- Zero impact on module internal event processing performance

### Operational Success
- Developers can easily create cross-module integrations following established patterns
- Module boundaries remain well-defined with explicit event contracts
- Failed cross-module events handled gracefully without impacting source modules
- Event system provides complete audit trail for cross-module interactions

This story enables powerful cross-module integration while maintaining proper bounded context isolation and following established event system patterns.