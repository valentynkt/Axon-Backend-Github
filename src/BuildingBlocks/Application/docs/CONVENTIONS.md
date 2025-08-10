# Application Layer Conventions

## Namespace Rules

- **Base Namespace**: All files in `src/BuildingBlocks/Application/**` should use namespace `BuildingBlocks.Application.*`
- **CQRS References**: All CQRS interfaces (`ICommand`, `IQuery`, `ICommandHandler`, `IQueryHandler`) must be referenced from `BuildingBlocks.Core.Abstractions.CQRS` - **single source of truth**
- **No Abstractions Folder**: All abstractions are integrated directly into appropriate functional folders

## Folder Structure

```
src/BuildingBlocks/Application/
├── Behaviors/              # MediatR pipeline behaviors (validation, caching, etc.)
├── Caching/               # Cache-related interfaces and implementations
├── Configuration/         # DI registration and configuration extensions
├── Events/
│   ├── Collecting/        # IDomainEventCollector, EfDomainEventCollector
│   ├── Consumption/       # Inbound integration event handling (Story 9)
│   │   ├── Inbox/         # IInboxStore, InboxResult, NoOpInboxStore
│   │   ├── IInboundIntegrationEventDispatcher.cs
│   │   ├── IIntegrationEventHandler.cs
│   │   ├── IIntegrationEventHandlerRegistry.cs
│   │   ├── InboundIntegrationEventDispatcher.cs
│   │   └── IntegrationEventHandlerRegistry.cs
│   ├── Dispatching/       # IEventDispatcher (interface only)
│   ├── Enveloping/        # Integration event envelopes, headers, context
│   ├── Mapping/           # IEventMapper, CompositeEventMapper
│   ├── Notifications/     # DomainEventNotification<T>
│   ├── Publishing/        # IIntegrationEventPublisher, NoOpIntegrationEventPublisher
│   └── Serialization/     # IEventSerializer, SystemTextJsonEventSerializer
├── Exceptions/            # Application-layer exceptions
├── Outbox/               # Outbox pattern interfaces + DTOs (no EF/MassTransit)
├── Validation/           # Validation abstractions and implementations
└── docs/                 # This file and other documentation
```

## Transport Neutrality

The Application layer must remain **transport-agnostic**:
- ❌ No MassTransit references
- ❌ No `Microsoft.AspNetCore.*` references  
- ❌ No Entity Framework implementations
- ✅ Interfaces and DTOs only
- ✅ Pure application logic

## Legacy Components

Files marked with `// TODO: Legacy (Phase 2): move to Infrastructure` contain infrastructure concerns and will be moved in future phases.

## Event Organization

- **Collecting**: Domain event collection from EF aggregates (`IDomainEventCollector`)
- **Consumption**: Inbound integration event handling and idempotency (`IInboundIntegrationEventDispatcher`, Story 9)
- **Dispatching**: Event routing interfaces (`IEventDispatcher`)
- **Enveloping**: Integration event envelope wrapping and context (`IntegrationEventEnvelope`)
- **Mapping**: Event transformation logic (`IEventMapper`)
- **Notifications**: MediatR notification wrappers (`DomainEventNotification<T>`)
- **Publishing**: Outbound integration event publishing (`IIntegrationEventPublisher`)
- **Serialization**: Event serialization and deserialization (`IEventSerializer`)

## Domain Events — Two Lanes

We adopt a **two-lane event handling model** for clear separation of concerns:

### Lane A — In-Process Policies (Same Module/Process)
- Triggered after successful transaction commit.
- Uses `DomainEventNotification<T>` via MediatR `Publish`.
- Purpose: update projections, enqueue internal commands, trigger local workflows.
- No external I/O or cross-boundary calls allowed.
- Handlers must be idempotent and fast.

**Flow:** `DomainEvent` → `DomainEventNotification<T>` → `IMediator.Publish(...)` → **local handlers only**

### Lane B — Cross-Boundary Publishing
- Domain events collected during transaction → stored in Outbox.
- `IEventMapper` maps to IntegrationEvent/InternalCommand.
- Published by OutboxProcessor to message bus or other transport.
- Only path for notifying other modules/services.

**Flow:** `DomainEvent` → stored in **Outbox** → `IEventMapper` → IntegrationEvent/InternalCommand → Bus/Transport layer

### RULES
- Never publish external messages from `DomainEventNotification` handlers.
- Never directly inject bus clients or HTTP clients into `DomainEventNotification` handlers.
- Cross-boundary communication must go through Outbox + IEventMapper pipeline.

### Implementation Components (Story 3)

#### In-Process Domain Notifications (Lane A)
- `IDomainEventCollector` collects events from EF-tracked aggregates using duck typing
- `IPostCommitDomainEventPublisher` wraps events in `DomainEventNotification<T>` and publishes via MediatR
- Handlers MUST be local and idempotent; NO external I/O (bus, HTTP)
- Executed after successful commit via `CommandTransactionBehavior`

#### Cross-Boundary (Lane B) 
- Domain events are persisted to Outbox inside the transaction
- Mapping to Integration Events is done via Application Event Mappers
- Publishing is handled by Outbox processing, not in-process notifications

#### Component Registration
```csharp
services.AddInProcessDomainEventNotifications();  // Registers collector + publisher
```

#### TransactionBehavior Flow
1. Collect domain events (pre-commit)
2. Store events to Outbox (Lane B, pre-commit)
3. Commit transaction
4. Publish `DomainEventNotification<T>` via MediatR (Lane A, post-commit)
5. Trigger outbox processing (Lane B, fire-and-forget)

## Outbox Backoff Policy & Metrics (Story 8)

### Retry Policy Configuration

The outbox system uses configurable exponential backoff with jitter for reliable retry handling:

**Backoff Formula**: `delay = base_delay * 2^attempt`, capped at `MaxRetryDelay`
**Jitter Applied**: Optional ±20% randomization to prevent thundering herd
**Dead Letter Decision**: Based on attempt count and error categorization

**Default Configuration**:
- `MaxRetryDelay`: 30 minutes
- `UseJitter`: true  
- `JitterRatio`: 0.2 (20%)
- Permanent errors bypass retry logic and move directly to dead letter

### Metrics Emission Points

The `IOutboxMetrics` interface provides hooks for Infrastructure-layer bindings to OTEL/Prometheus:

- **RecordFetched(count)**: When entries are fetched for processing
- **RecordProcessed(processed, succeeded, failed, deadLettered, duration)**: After batch completion
- **RecordRetryScheduled(delay)**: When retry is scheduled with computed delay  
- **RecordPermanentFailure()**: When entry moves to dead letter
- **RecordCleanup(cleaned)**: During completed entry cleanup
- **RecordProcessorLoopError()**: On processor infrastructure failures

### Infrastructure Integration

Phase 2 Infrastructure should:
1. Replace `NoOpOutboxMetrics` with OTEL/Prometheus implementation
2. Map metrics to appropriate counters, histograms, and gauges
3. Preserve computed backoff decisions from Application layer

## Inbound Integration Events - "Inbox" Pattern (Story 9)

### Overview

The inbound integration event pipeline handles consumption of external integration events with guaranteed idempotent processing. This complements the outbound (outbox) pipeline for a complete event-driven architecture.

### Key Components

#### IInboundIntegrationEventDispatcher
- **Purpose**: Transport-neutral dispatcher for processing inbound integration event envelopes
- **Responsibilities**: 
  - Deserialize envelopes into typed integration events
  - Check idempotency via `IdempotencyKey` header
  - Route events to registered handlers
  - Emit processing metrics
- **Location**: `BuildingBlocks.Application.Events.Consumption.InboundIntegrationEventDispatcher`

#### IIntegrationEventHandler<TEvent>
- **Purpose**: Handler contract for specific integration event types
- **Usage**: Implement this interface to handle inbound integration events
- **Registration**: Register handlers in DI container as `IIntegrationEventHandler<SpecificEventType>`
- **Execution**: Handlers are executed sequentially to maintain ordering

#### IInboxStore (Idempotency)
- **Purpose**: Ensure exactly-once processing of integration events
- **Mechanism**: Uses `IdempotencyKey` header to track processing state
- **Default**: `NoOpInboxStore` (no idempotency guarantees) 
- **Infrastructure**: Real implementations should use database storage

#### IIntegrationEventHandlerRegistry
- **Purpose**: Resolve registered handlers for specific event types from DI container
- **Implementation**: `IntegrationEventHandlerRegistry` uses `IServiceProvider.GetServices()`

### Processing Flow

1. **Envelope Reception**: Transport layer receives `IntegrationEventEnvelope`
2. **Type Resolution**: Resolve CLR type from envelope's `Type` field
3. **Idempotency Check**: Query `IInboxStore` using `IdempotencyKey` header
4. **Deserialization**: Deserialize envelope payload to typed integration event
5. **Handler Resolution**: Find all registered handlers for the event type
6. **Context Setup**: Establish envelope context for handler execution
7. **Handler Execution**: Execute handlers sequentially
8. **Result Tracking**: Record success/failure in inbox store and metrics

### Error Classification

- **UnknownType**: Event type name cannot be resolved to CLR type
- **BadPayload**: Event payload cannot be deserialized  
- **NoHandlers**: No handlers registered (treated as success)
- **AlreadyProcessed**: Duplicate `IdempotencyKey` (skip processing)
- **HandlerFailed**: Handler execution exception

### Metrics Integration

The `IOutboxMetrics` interface has been extended with inbound methods:
- `RecordInboundReceived()`: Event received for processing
- `RecordInboundHandled()`: Event successfully processed
- `RecordInboundDuplicate()`: Duplicate event skipped
- `RecordInboundHandlerFailed()`: Handler execution failure

### Registration

```csharp
// Register complete pipeline (includes inbound)
services.AddApplicationEventing();

// Or register inbound pipeline separately
services.AddInboundIntegrationEventPipeline();

// Register event handlers
services.AddScoped<IIntegrationEventHandler<UserCreatedEvent>, UserCreatedHandler>();
services.AddScoped<IIntegrationEventHandler<UserCreatedEvent>, NotificationHandler>();
```

### Infrastructure Requirements (Phase 2)

1. **Inbox Store**: Replace `NoOpInboxStore` with database-backed implementation
2. **Transport Integration**: Wire `IInboundIntegrationEventDispatcher` to message bus consumers
3. **Type Discovery**: Ensure all integration event types are loaded in application domain
4. **Metrics**: Replace `NoOpOutboxMetrics` with OTEL/Prometheus implementation

## Import Guidelines

- Use specific namespace imports rather than root `BuildingBlocks.Application.Events`
- Examples:
  - `using BuildingBlocks.Application.Events.Collecting;` for `IDomainEventCollector`
  - `using BuildingBlocks.Application.Events.Consumption;` for `IInboundIntegrationEventDispatcher`, `IIntegrationEventHandler`
  - `using BuildingBlocks.Application.Events.Consumption.Inbox;` for `IInboxStore`
  - `using BuildingBlocks.Application.Events.Dispatching;` for `IEventDispatcher`
  - `using BuildingBlocks.Application.Events.Enveloping;` for `IntegrationEventEnvelope`, `IEventTypeNameResolver`
  - `using BuildingBlocks.Application.Events.Mapping;` for `IEventMapper`
  - `using BuildingBlocks.Application.Events.Notifications;` for `DomainEventNotification<T>`
  - `using BuildingBlocks.Application.Events.Publishing;` for `IIntegrationEventPublisher`
  - `using BuildingBlocks.Application.Events.Serialization;` for `IEventSerializer`
  - `using BuildingBlocks.Application.Outbox.Retry;` for `IOutboxBackoffPolicy`
  - `using BuildingBlocks.Application.Outbox.Monitoring;` for `IOutboxMetrics`