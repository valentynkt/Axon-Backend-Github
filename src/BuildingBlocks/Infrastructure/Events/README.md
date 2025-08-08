# Centralized Outbox Processor Service

This directory contains the implementation of **Epic 06 Story 01: Centralized Outbox Processor Service**, which provides a centralized, reliable outbox message processing infrastructure for the Axon Backend.

## Overview

The centralized outbox processor service enables reliable event publishing with transactional guarantees across all modules in the Axon Backend. It implements the Outbox Pattern to ensure that domain events are persisted atomically with business data and processed reliably in the background.

## Key Components

### 1. OutboxMessage (`OutboxMessage.cs`)
- **Purpose**: Centralized entity for storing outbox messages with Result pattern integration
- **Features**: 
  - Factory methods with comprehensive validation
  - Exponential backoff retry logic
  - Processing state management
  - Comprehensive metadata support
- **Migration**: Moved from Chat module to BuildingBlocks for shared use

### 2. IOutboxMessageProcessor (`IOutboxMessageProcessor.cs`)
- **Purpose**: Interface defining the contract for centralized outbox message processing
- **Features**:
  - Batch processing capabilities
  - Health monitoring integration
  - Retry logic for failed messages
  - Graceful start/stop operations

### 3. OutboxProcessorService (`OutboxProcessorService.cs`)
- **Purpose**: Background service implementation for continuous outbox message processing
- **Features**:
  - Implements IHostedService for ASP.NET Core integration
  - Circuit breaker pattern for resilience
  - Comprehensive error handling and logging
  - Configurable processing intervals and batch sizes
  - Result pattern integration throughout

### 4. OutboxProcessorOptions (`OutboxProcessorOptions.cs`)
- **Purpose**: Configuration options for the outbox processor
- **Features**:
  - Comprehensive validation
  - Circuit breaker configuration
  - Performance tuning options
  - Health check thresholds
  - Cleanup settings

### 5. Health Check Integration
- **OutboxProcessorHealthCheck.cs**: Health check implementation
- **OutboxProcessorServiceExtensions.cs**: Service registration extensions

### 6. Data Access Layer
- **EfOutboxMessageRepository.cs**: Entity Framework repository implementation
- **OutboxMessageConfiguration.cs**: EF Core entity configuration with PostgreSQL optimizations

## Architecture Compliance

This implementation follows Axon Backend's architectural principles:

- **Clean Architecture**: Clear separation between core abstractions and infrastructure
- **CQRS**: Proper separation of command and query responsibilities
- **Result Pattern**: Comprehensive error handling using Result<T> throughout
- **SPARC Patterns**: Event sourcing and domain-driven design principles
- **Modern C#**: File-scoped namespaces, records, nullable reference types

## Integration

### Service Registration

```csharp
// In Program.cs or startup
services.AddOutboxMessageProcessor(configuration);
services.AddOutboxProcessorHealthChecks();
```

### Configuration

```json
{
  "OutboxProcessor": {
    "Enabled": true,
    "BatchSize": 100,
    "MaxRetries": 3,
    "ProcessingInterval": "00:00:30",
    "EnableHealthChecks": true,
    "CircuitBreaker": {
      "Enabled": true,
      "FailureThreshold": 5,
      "OpenCircuitDuration": "00:05:00"
    }
  }
}
```

### Usage Example

```csharp
// Creating an outbox message
var messageResult = OutboxMessage.Create(
    type: "UserRegistered",
    payload: eventJson,
    metadata: metadataJson,
    occurredAtUtc: DateTime.UtcNow);

if (messageResult.IsSuccess)
{
    await repository.AddAsync(messageResult.Value);
}
```

## Zero Breaking Changes

The implementation maintains complete backward compatibility:

- Chat module continues to use its local OutboxMessage entity
- Existing functionality remains unchanged
- Gradual migration path is available through type aliasing
- New modules can adopt the centralized version immediately

## Performance Optimizations

- PostgreSQL-specific indexing strategy
- Batch processing for improved throughput
- Circuit breaker pattern for fault tolerance
- FOR UPDATE SKIP LOCKED queries for concurrent processing
- Configurable cleanup operations

## Monitoring and Observability

- Comprehensive health checks
- Detailed logging with structured data
- Performance metrics collection
- Distributed tracing support
- Real-time processing statistics

## Future Enhancements

- Integration with existing EventDispatcher
- Event deserialization and dispatching implementation
- Dead letter queue management
- Message replay capabilities
- Dashboard and monitoring UI

This implementation provides a solid foundation for reliable event processing while maintaining the flexibility to extend and enhance functionality as needed.