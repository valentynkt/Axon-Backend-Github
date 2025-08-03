# 🎯 SPARC Phase 1: Comprehensive Conversation Persistence Specification v2.0

## 📋 Executive Summary

This specification defines a comprehensive conversation message persistence system for Axon Backend using modern C# approaches, Entity Framework Core built-in capabilities, proper Clean Architecture boundaries, and sophisticated abstraction hierarchies.

## 🏗️ Architecture Analysis - Current State

### ✅ Strong Foundation Already Exists

**Domain Layer Excellence:**
- `Entity<TId>` with proper equality semantics and type safety
- `AggregateRoot<TId>` with domain events collection and lifecycle management
- `Conversation` aggregate with rich business logic and validation
- Strong typing with `ConversationId`, `MessageId`, `MessageRole` value objects
- Domain events: `MessageAddedDomainEvent`, `ConversationCompletedDomainEvent`, `ToolExecutedDomainEvent`

**Clean Architecture Boundaries:**
```
src/Shared/        -> Cross-cutting concerns, primitives
src/Api/           -> HTTP host, endpoints, DTOs  
src/Modules/Chat/  -> Bounded context implementation
  ├── Domain/      -> Business logic, aggregates, events
  ├── Application/ -> Use cases, handlers, contracts
  └── Infrastructure/ -> Adapters, external concerns
```

### ❌ Missing Critical Infrastructure

**Infrastructure Gaps Identified:**
- No Entity Framework DbContext implementation
- No repository abstractions or implementations  
- No Unit of Work pattern
- No comprehensive auditing system leveraging EF capabilities
- No event sourcing infrastructure
- Missing abstraction hierarchy (Identifiable, BaseEntity, AuditableEntity)

## 🎯 Enhanced Requirements Specification

### R1: Comprehensive Abstraction Hierarchy

**R1.1 - Identifiable Interface**
```csharp
/// <summary>
/// Represents an entity that can be uniquely identified
/// </summary>
/// <typeparam name="TId">The type of the identifier</typeparam>
public interface IIdentifiable<out TId> where TId : notnull
{
    /// <summary>
    /// Gets the unique identifier for this entity
    /// </summary>
    TId Id { get; }
}
```

**R1.2 - Base Entity Enhancement**
```csharp
/// <summary>
/// Enhanced base entity with identity and equality semantics
/// </summary>
/// <typeparam name="TId">The type of the identifier</typeparam>
public abstract class BaseEntity<TId> : IIdentifiable<TId>, IEquatable<BaseEntity<TId>>
    where TId : notnull
{
    public TId Id { get; protected set; }
    
    // Enhanced equality with proper type checking
    // Thread-safe hash code generation
    // Domain event capabilities for specialized entities
}
```

**R1.3 - Auditable Entity with EF Built-in Capabilities**
```csharp
/// <summary>
/// Auditable entity leveraging Entity Framework built-in auditing features
/// Uses Shadow Properties and SaveChangesInterceptor for automatic auditing
/// </summary>
/// <typeparam name="TId">The type of the identifier</typeparam>
public abstract class AuditableEntity<TId> : BaseEntity<TId>, IAuditable
    where TId : notnull
{
    // NO explicit audit properties - uses EF Shadow Properties
    // Auditing handled by SaveChangesInterceptor automatically
    // Provides methods to access audit data when needed
}

/// <summary>
/// Marker interface for entities that should be audited
/// EF will automatically track CreatedAt, CreatedBy, UpdatedAt, UpdatedBy via Shadow Properties
/// </summary>
public interface IAuditable
{
    // Marker interface - no properties
    // EF Shadow Properties: CreatedAt, CreatedBy, UpdatedAt, UpdatedBy
}
```

### R2: Modern Entity Framework Infrastructure

**R2.1 - Advanced DbContext with Built-in Auditing**
```csharp
/// <summary>
/// Chat bounded context DbContext with comprehensive auditing and event sourcing
/// </summary>
public sealed class ChatDbContext : DbContext
{
    // DbSets for aggregates and read models
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<ConversationEvent> ConversationEvents => Set<ConversationEvent>(); // Event Store
    public DbSet<ConversationReadModel> ConversationReadModels => Set<ConversationReadModel>(); // CQRS Projections
    
    // Advanced features
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Shadow Properties for auditing
        ConfigureAuditShadowProperties(modelBuilder);
        
        // Owned Entity Types for Value Objects
        ConfigureValueObjects(modelBuilder);
        
        // Event Sourcing tables
        ConfigureEventSourcing(modelBuilder);
        
        // PostgreSQL-specific optimizations
        ConfigurePostgreSQLOptimizations(modelBuilder);
    }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Add auditing interceptor
        optionsBuilder.AddInterceptors(new AuditSaveChangesInterceptor(_currentUserService));
        
        // Add domain event publishing interceptor
        optionsBuilder.AddInterceptors(new DomainEventPublishingInterceptor(_mediator));
        
        // Performance optimizations
        optionsBuilder.EnableSensitiveDataLogging(false);
        optionsBuilder.EnableServiceProviderCaching();
    }
}
```

**R2.2 - SaveChangesInterceptor for Automatic Auditing**
```csharp
/// <summary>
/// Interceptor that automatically handles auditing using EF Shadow Properties
/// No need for explicit audit properties in entities
/// </summary>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService;
    
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            HandleAuditProperties(eventData.Context);
        }
        return base.SavingChanges(eventData, result);
    }
    
    private void HandleAuditProperties(DbContext context)
    {
        var entries = context.ChangeTracker
            .Entries()
            .Where(e => e.Entity is IAuditable && (e.State == EntityState.Added || e.State == EntityState.Modified));
            
        foreach (var entry in entries)
        {
            var userId = _currentUserService.GetCurrentUserId();
            var timestamp = DateTime.UtcNow;
            
            if (entry.State == EntityState.Added)
            {
                entry.Property("CreatedAt").CurrentValue = timestamp;
                entry.Property("CreatedBy").CurrentValue = userId;
            }
            
            entry.Property("UpdatedAt").CurrentValue = timestamp;
            entry.Property("UpdatedBy").CurrentValue = userId;
        }
    }
}
```

### R3: Repository Pattern with Generic and Specialized Implementations

**R3.1 - Generic Repository Interface**
```csharp
/// <summary>
/// Generic repository interface for common CRUD operations
/// </summary>
/// <typeparam name="TEntity">The entity type</typeparam>
/// <typeparam name="TId">The identifier type</typeparam>
public interface IGenericRepository<TEntity, in TId> 
    where TEntity : BaseEntity<TId>
    where TId : notnull
{
    // Standard CRUD operations
    Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TEntity>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    
    // Advanced querying
    Task<IReadOnlyList<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    
    // Modifications
    void Add(TEntity entity);
    void Update(TEntity entity);
    void Remove(TEntity entity);
    void RemoveRange(IEnumerable<TEntity> entities);
}
```

**R3.2 - Specialized Conversation Repository**
```csharp
/// <summary>
/// Specialized repository for Conversation aggregate with domain-specific queries
/// </summary>
public interface IConversationRepository : IGenericRepository<Conversation, ConversationId>
{
    // Domain-specific queries
    Task<IReadOnlyList<Conversation>> GetActiveConversationsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Conversation>> GetCompletedConversationsAsync(DateTime since, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Conversation>> GetConversationsWithMessageCountAsync(int minMessages, CancellationToken cancellationToken = default);
    
    // Event sourcing support
    Task<IReadOnlyList<IDomainEvent>> GetConversationEventsAsync(ConversationId conversationId, CancellationToken cancellationToken = default);
    Task AppendEventsAsync(ConversationId conversationId, IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default);
    
    // Performance queries
    Task<ConversationStatistics> GetStatisticsAsync(ConversationId conversationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Conversation>> GetRecentConversationsAsync(int count, CancellationToken cancellationToken = default);
    
    // Audit support
    Task<IReadOnlyList<AuditEntry>> GetAuditTrailAsync(ConversationId conversationId, CancellationToken cancellationToken = default);
}
```

### R4: Unit of Work Pattern for Transaction Management

**R4.1 - Unit of Work Interface**
```csharp
/// <summary>
/// Unit of Work pattern for coordinating multiple repository operations in a single transaction
/// </summary>
public interface IUnitOfWork : IDisposable
{
    // Repository access
    IConversationRepository Conversations { get; }
    IGenericRepository<TEntity, TId> Repository<TEntity, TId>() 
        where TEntity : BaseEntity<TId> 
        where TId : notnull;
    
    // Transaction management
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    
    // Event handling
    Task PublishDomainEventsAsync(CancellationToken cancellationToken = default);
}
```

### R5: Event Sourcing Infrastructure

**R5.1 - Event Store Design**
```csharp
/// <summary>
/// Event sourcing event entity for append-only event storage
/// </summary>
public sealed class ConversationEvent : BaseEntity<Guid>
{
    public ConversationId AggregateId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string EventData { get; private set; } = string.Empty; // JSON serialized
    public string EventMetadata { get; private set; } = string.Empty; // JSON metadata
    public int Version { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    
    // Static factory methods for creation
    public static ConversationEvent Create(ConversationId aggregateId, IDomainEvent domainEvent, int version, string userId)
    {
        // Implementation
    }
}

/// <summary>
/// Event store interface for event sourcing operations
/// </summary>
public interface IEventStore
{
    Task AppendEventsAsync<TId>(TId aggregateId, IEnumerable<IDomainEvent> events, int expectedVersion, CancellationToken cancellationToken = default) where TId : notnull;
    Task<IReadOnlyList<IDomainEvent>> GetEventsAsync<TId>(TId aggregateId, CancellationToken cancellationToken = default) where TId : notnull;
    Task<IReadOnlyList<IDomainEvent>> GetEventsAsync<TId>(TId aggregateId, int fromVersion, CancellationToken cancellationToken = default) where TId : notnull;
    Task<TAggregate?> RehydrateAggregateAsync<TAggregate, TId>(TId aggregateId, CancellationToken cancellationToken = default) 
        where TAggregate : AggregateRoot<TId> 
        where TId : notnull;
}
```

### R6: CQRS Read Model Projections

**R6.1 - Read Model Design for Query Optimization**
```csharp
/// <summary>
/// Optimized read model for conversation queries
/// Separate from the domain aggregate for query performance
/// </summary>
public sealed class ConversationReadModel : AuditableEntity<ConversationId>
{
    // Flattened properties for fast querying
    public string Title { get; private set; } = string.Empty;
    public int MessageCount { get; private set; }
    public int ToolExecutionCount { get; private set; }
    public DateTime LastMessageAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public bool IsActive { get; private set; }
    public string Context { get; private set; } = string.Empty; // JSON
    public string Tags { get; private set; } = string.Empty; // JSON array
    
    // Search optimization
    public string SearchVector { get; private set; } = string.Empty; // PostgreSQL full-text search
    
    // Performance metrics
    public TimeSpan TotalDuration { get; private set; }
    public TimeSpan TotalToolExecutionTime { get; private set; }
    
    // Update methods for projections
    public void UpdateFromDomainEvent(IDomainEvent domainEvent)
    {
        // Handle different event types and update read model accordingly
    }
}
```

## 🔧 Implementation Strategy

### Phase 1: Infrastructure Foundation
1. **Abstraction Hierarchy**: Implement `IIdentifiable<TId>` → `BaseEntity<TId>` → `AuditableEntity<TId>`
2. **EF DbContext**: Create `ChatDbContext` with interceptors and shadow properties
3. **Auditing System**: Implement `AuditSaveChangesInterceptor` with EF built-in capabilities
4. **Repository Pattern**: Generic repository with specialized conversation repository

### Phase 2: Event Sourcing Implementation  
1. **Event Store**: `ConversationEvent` entity and `IEventStore` interface
2. **Event Serialization**: JSON serialization with versioning support
3. **Aggregate Rehydration**: Replay events to reconstruct aggregate state
4. **Snapshot Strategy**: Performance optimization for large event streams

### Phase 3: CQRS Read Models
1. **Read Model Design**: `ConversationReadModel` for optimized queries
2. **Projection Handlers**: Update read models from domain events
3. **Query Optimization**: PostgreSQL-specific performance tuning
4. **Full-Text Search**: Implement search vector for content searching

### Phase 4: Clean Architecture Integration
1. **Boundary Alignment**: Ensure proper layer separation
2. **Dependency Injection**: Configure services in each layer appropriately  
3. **Application Layer**: Command/Query handlers using repositories
4. **API Layer**: Endpoints consuming application services

## 🎯 Success Criteria

### Technical Validation
- [ ] All entities inherit from proper abstraction hierarchy
- [ ] EF Shadow Properties handle auditing automatically
- [ ] Event sourcing maintains complete audit trail
- [ ] Repository pattern provides clean abstractions
- [ ] Unit of Work coordinates transactions properly
- [ ] Read models optimize query performance
- [ ] PostgreSQL features are leveraged effectively

### Architecture Compliance
- [ ] Clean Architecture boundaries respected
- [ ] Domain remains persistence-ignorant
- [ ] Infrastructure handles all external concerns
- [ ] Application layer orchestrates business logic
- [ ] Proper dependency injection configuration

## 💬 Human Validation Required

**Critical Design Decisions Requiring Approval:**

1. **Abstraction Hierarchy**: Is the `IIdentifiable<TId>` → `BaseEntity<TId>` → `AuditableEntity<TId>` progression appropriate?

2. **EF Built-in Auditing**: Do you approve of using Shadow Properties + SaveChangesInterceptor instead of explicit audit properties?

3. **Repository Pattern**: Is the combination of generic + specialized repositories suitable for your needs?

4. **Event Sourcing Scope**: Should ALL conversation changes be event-sourced, or only specific operations?

5. **CQRS Read Models**: Do you want separate read models for query optimization, or is this over-engineering?

6. **Integration Approach**: Any specific requirements for integrating with existing Chat module infrastructure?

Please provide feedback on these design decisions so I can proceed to **Phase 2: Pseudocode** with your approved approach.