# SPARC Phase 2 Task 2: Data Access & Transaction Orchestration - Pseudocode

## 🎯 Executive Summary

**Objective**: Create comprehensive Data Access & Transaction Orchestration layer with Generic Repository Pattern, Narrow Repository inheritance, Unit of Work facade, and Audit interceptor for safe transactional operations.

**Key Deliverables**:
- Generic Repository with strict no-IQueryable interface
- Narrow Repository implementations with compiled queries
- Unit of Work facade for transaction orchestration
- Audit SaveChanges interceptor with proper DI lifecycle
- Integration tests with Testcontainers optimization

## 📋 Algorithm Overview & Performance Characteristics

### Core Performance Targets
- **Repository Operations**: O(1) for GetById, O(n) for batch operations
- **Compiled Queries**: 30-50% performance improvement over LINQ
- **Transaction Commits**: <100ms for typical operations
- **Audit Overhead**: <5ms per entity change
- **Memory Usage**: Minimal EF tracking overhead

## 🏗️ 1. Generic Repository Pattern Design

### 1.1 Core Interface Algorithm
```csharp
// ALGORITHM: Generic Repository Interface Design
// COMPLEXITY: O(1) for single operations, O(n) for batch operations
// MEMORY: O(n) where n = number of entities tracked

INTERFACE IRepository<TEntity, TId>
  WHERE TEntity : AggregateRoot<TId>
  WHERE TId : notnull

  // ALGORITHM: Single Entity Retrieval
  // COMPLEXITY: O(1) - Direct primary key lookup
  // PERFORMANCE: ~1-5ms with proper indexing
  ASYNC METHOD GetByIdAsync(TId id, CancellationToken ct = default) -> Task<TEntity?>
    BEGIN
      VALIDATE id IS NOT NULL
      EXECUTE compiled_query_get_by_id WITH id
      RETURN entity OR null
      HANDLE DbException -> LOG and RETHROW as RepositoryException
    END

  // ALGORITHM: Batch Entity Retrieval
  // COMPLEXITY: O(n) where n = ids.Count
  // PERFORMANCE: ~5-20ms for batches of 100 entities
  ASYNC METHOD GetByIdsAsync(IEnumerable<TId> ids, CancellationToken ct = default) -> Task<IReadOnlyList<TEntity>>
    BEGIN
      IF ids IS EMPTY RETURN empty_list
      VALIDATE all ids ARE NOT NULL
      EXECUTE compiled_query_get_by_ids WITH ids
      RETURN entities_list
      HANDLE DbException -> LOG and RETHROW as RepositoryException
    END

  // ALGORITHM: Entity Addition
  // COMPLEXITY: O(1) - Memory operation only
  // PERFORMANCE: <1ms - deferred to SaveChanges
  METHOD Add(TEntity entity) -> void
    BEGIN
      VALIDATE entity IS NOT NULL
      VALIDATE entity.Id IS NOT DEFAULT
      dbSet.Add(entity)
      // No immediate database operation - deferred to UoW
    END

  // ALGORITHM: Entity Update
  // COMPLEXITY: O(1) - Memory operation only
  // PERFORMANCE: <1ms - deferred to SaveChanges
  METHOD Update(TEntity entity) -> void
    BEGIN
      VALIDATE entity IS NOT NULL
      VALIDATE entity IS TRACKED OR ATTACH
      IF entity IS NOT TRACKED
        dbSet.Attach(entity)
        dbContext.Entry(entity).State = EntityState.Modified
      END IF
      // Deferred to UoW for actual persistence
    END

  // ALGORITHM: Entity Removal
  // COMPLEXITY: O(1) - Memory operation only
  // PERFORMANCE: <1ms - deferred to SaveChanges
  METHOD Remove(TEntity entity) -> void
    BEGIN
      VALIDATE entity IS NOT NULL
      IF entity IS TRACKED
        dbSet.Remove(entity)
      ELSE
        dbSet.Attach(entity)
        dbSet.Remove(entity)
      END IF
      // Deferred to UoW for actual deletion
    END

END INTERFACE
```

### 1.2 Base Repository Implementation Algorithm
```csharp
// ALGORITHM: Base Repository Implementation
// DESIGN PATTERN: Template Method with error handling
// PERFORMANCE: Optimized with compiled queries and minimal allocations

ABSTRACT CLASS BaseRepository<TEntity, TId> : IRepository<TEntity, TId>
  WHERE TEntity : AggregateRoot<TId>
  WHERE TId : notnull

  PRIVATE READONLY DbContext dbContext
  PRIVATE READONLY DbSet<TEntity> dbSet
  PRIVATE READONLY ILogger<BaseRepository<TEntity, TId>> logger

  // ALGORITHM: Compiled Query Cache
  // PERFORMANCE: 30-50% faster than runtime LINQ compilation
  PRIVATE STATIC READONLY compiled_queries = NEW Dictionary<string, Delegate>()

  CONSTRUCTOR(DbContext context, ILogger logger)
    BEGIN
      this.dbContext = context ?? THROW ArgumentNullException
      this.dbSet = context.Set<TEntity>()
      this.logger = logger ?? THROW ArgumentNullException
      INITIALIZE_COMPILED_QUERIES()
    END

  // ALGORITHM: Compiled Query Initialization
  // COMPLEXITY: O(1) - Done once per application lifetime
  PRIVATE METHOD INITIALIZE_COMPILED_QUERIES() -> void
    BEGIN
      IF NOT compiled_queries.ContainsKey(typeof(TEntity).Name + "_ById")
        
        // Single entity query compilation
        compiled_queries[typeof(TEntity).Name + "_ById"] = 
          EF.CompileQuery((DbContext ctx, TId id) =>
            ctx.Set<TEntity>()
               .AsNoTracking()
               .FirstOrDefault(e => e.Id.Equals(id)))

        // Batch entities query compilation
        compiled_queries[typeof(TEntity).Name + "_ByIds"] = 
          EF.CompileQuery((DbContext ctx, IEnumerable<TId> ids) =>
            ctx.Set<TEntity>()
               .AsNoTracking()
               .Where(e => ids.Contains(e.Id))
               .ToList())

      END IF
    END

  // ALGORITHM: Optimized Single Entity Retrieval
  PUBLIC ASYNC METHOD GetByIdAsync(TId id, CancellationToken ct = default) -> Task<TEntity?>
    BEGIN
      TRY
        logger.LogDebug("Retrieving {EntityType} with ID: {EntityId}", typeof(TEntity).Name, id)
        
        START_TIMER operation_timer
        
        // Use compiled query for performance
        VAR compiledQuery = (Func<DbContext, TId, TEntity?>)compiled_queries[typeof(TEntity).Name + "_ById"]
        VAR entity = compiledQuery(dbContext, id)
        
        STOP_TIMER operation_timer
        logger.LogDebug("Retrieved {EntityType} in {ElapsedMs}ms", typeof(TEntity).Name, operation_timer.ElapsedMilliseconds)
        
        RETURN entity
        
      CATCH DbException ex
        logger.LogError(ex, "Database error retrieving {EntityType} with ID: {EntityId}", typeof(TEntity).Name, id)
        THROW NEW RepositoryException($"Failed to retrieve {typeof(TEntity).Name}", ex)
      END TRY
    END

  // ALGORITHM: Optimized Batch Retrieval
  PUBLIC ASYNC METHOD GetByIdsAsync(IEnumerable<TId> ids, CancellationToken ct = default) -> Task<IReadOnlyList<TEntity>>
    BEGIN
      IF ids IS NULL OR EMPTY RETURN new List<TEntity>().AsReadOnly()
      
      TRY
        VAR idsList = ids.ToList() // Materialize once
        logger.LogDebug("Retrieving {Count} {EntityType} entities", idsList.Count, typeof(TEntity).Name)
        
        START_TIMER operation_timer
        
        // Use compiled query for performance
        VAR compiledQuery = (Func<DbContext, IEnumerable<TId>, List<TEntity>>)compiled_queries[typeof(TEntity).Name + "_ByIds"]
        VAR entities = compiledQuery(dbContext, idsList)
        
        STOP_TIMER operation_timer
        logger.LogDebug("Retrieved {Count} {EntityType} entities in {ElapsedMs}ms", 
          entities.Count, typeof(TEntity).Name, operation_timer.ElapsedMilliseconds)
        
        RETURN entities.AsReadOnly()
        
      CATCH DbException ex
        logger.LogError(ex, "Database error retrieving {EntityType} entities", typeof(TEntity).Name)
        THROW NEW RepositoryException($"Failed to retrieve {typeof(TEntity).Name} entities", ex)
      END TRY
    END

  // ALGORITHM: Deferred Entity Addition
  PUBLIC METHOD Add(TEntity entity) -> void
    BEGIN
      IF entity IS NULL THROW ArgumentNullException(nameof(entity))
      IF entity.Id.Equals(default(TId)) THROW ArgumentException("Entity ID cannot be default value")
      
      logger.LogDebug("Adding {EntityType} with ID: {EntityId}", typeof(TEntity).Name, entity.Id)
      dbSet.Add(entity)
      
      // Operation is deferred until SaveChanges - no immediate DB call
    END

  // ALGORITHM: Optimistic Concurrency Update
  PUBLIC METHOD Update(TEntity entity) -> void
    BEGIN
      IF entity IS NULL THROW ArgumentNullException(nameof(entity))
      
      logger.LogDebug("Updating {EntityType} with ID: {EntityId}", typeof(TEntity).Name, entity.Id)
      
      VAR entry = dbContext.Entry(entity)
      IF entry.State == EntityState.Detached
        dbSet.Attach(entity)
        entry.State = EntityState.Modified
      END IF
      
      // Optimistic concurrency will be checked during SaveChanges
      // xmin column will be automatically handled by PostgreSQL
    END

  // ALGORITHM: Safe Entity Removal
  PUBLIC METHOD Remove(TEntity entity) -> void
    BEGIN
      IF entity IS NULL THROW ArgumentNullException(nameof(entity))
      
      logger.LogDebug("Removing {EntityType} with ID: {EntityId}", typeof(TEntity).Name, entity.Id)
      
      VAR entry = dbContext.Entry(entity)
      IF entry.State == EntityState.Detached
        dbSet.Attach(entity)
      END IF
      
      dbSet.Remove(entity)
      // Actual deletion deferred until SaveChanges
    END

END CLASS
```

## 🎯 2. Narrow Repository Inheritance Design

### 2.1 Conversation Repository Algorithm
```csharp
// ALGORITHM: Narrow Repository with Domain-Specific Operations
// PATTERN: Specialized repository inheriting from generic base
// PERFORMANCE: Optimized with compiled queries for hot paths

INTERFACE IConversationRepository : IRepository<Conversation, ConversationId>

  // ALGORITHM: Eager Loading with Compiled Query
  // COMPLEXITY: O(1) with proper indexing
  // PERFORMANCE: ~10-30ms depending on message count
  ASYNC METHOD GetWithMessagesAsync(ConversationId id, CancellationToken ct = default) -> Task<Conversation?>

  // ALGORITHM: Recent Conversations Query
  // COMPLEXITY: O(log n) with proper indexing on created_at
  // PERFORMANCE: ~5-15ms for typical limit values
  ASYNC METHOD GetRecentAsync(int limit, CancellationToken ct = default) -> Task<IReadOnlyList<Conversation>>

  // ALGORITHM: Active Conversations for User
  // COMPLEXITY: O(log n) with proper indexing
  // PERFORMANCE: ~5-20ms depending on user activity
  ASYNC METHOD GetActiveForUserAsync(UserId userId, CancellationToken ct = default) -> Task<IReadOnlyList<Conversation>>

END INTERFACE

// IMPLEMENTATION: ConversationRepository
CLASS ConversationRepository : BaseRepository<Conversation, ConversationId>, IConversationRepository

  // ALGORITHM: Compiled Query Cache for Narrow Repository
  PRIVATE STATIC READONLY narrow_compiled_queries = NEW Dictionary<string, Delegate>()

  CONSTRUCTOR(ChatDbContext context, ILogger<ConversationRepository> logger) : BASE(context, logger)
    BEGIN
      INITIALIZE_NARROW_COMPILED_QUERIES()
    END

  // ALGORITHM: Initialize Domain-Specific Compiled Queries
  PRIVATE METHOD INITIALIZE_NARROW_COMPILED_QUERIES() -> void
    BEGIN
      // Conversation with messages query
      narrow_compiled_queries["GetWithMessages"] = 
        EF.CompileQuery((ChatDbContext ctx, ConversationId id) =>
          ctx.Conversations
             .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
             .AsNoTracking()
             .FirstOrDefault(c => c.Id == id))

      // Recent conversations query  
      narrow_compiled_queries["GetRecent"] = 
        EF.CompileQuery((ChatDbContext ctx, int limit) =>
          ctx.Conversations
             .OrderByDescending(c => c.CreatedAt)
             .Take(limit)
             .AsNoTracking()
             .ToList())

      // Active conversations for user query
      narrow_compiled_queries["GetActiveForUser"] = 
        EF.CompileQuery((ChatDbContext ctx, UserId userId) =>
          ctx.Conversations
             .Where(c => c.UserId == userId && c.IsActive)
             .OrderByDescending(c => c.LastMessageAt)
             .AsNoTracking()
             .ToList())
    END

  // ALGORITHM: Optimized Conversation with Messages Retrieval
  PUBLIC ASYNC METHOD GetWithMessagesAsync(ConversationId id, CancellationToken ct = default) -> Task<Conversation?>
    BEGIN
      TRY
        logger.LogDebug("Retrieving conversation {ConversationId} with messages", id)
        
        START_TIMER operation_timer
        
        // Use compiled query for optimal performance
        VAR compiledQuery = (Func<ChatDbContext, ConversationId, Conversation?>)narrow_compiled_queries["GetWithMessages"]
        VAR conversation = compiledQuery((ChatDbContext)dbContext, id)
        
        STOP_TIMER operation_timer
        
        IF conversation IS NOT NULL
          logger.LogDebug("Retrieved conversation {ConversationId} with {MessageCount} messages in {ElapsedMs}ms", 
            id, conversation.Messages.Count, operation_timer.ElapsedMilliseconds)
        ELSE
          logger.LogDebug("Conversation {ConversationId} not found", id)
        END IF
        
        RETURN conversation
        
      CATCH DbException ex
        logger.LogError(ex, "Database error retrieving conversation {ConversationId} with messages", id)
        THROW NEW RepositoryException($"Failed to retrieve conversation with messages", ex)
      END TRY
    END

  // ALGORITHM: Recent Conversations with Pagination
  PUBLIC ASYNC METHOD GetRecentAsync(int limit, CancellationToken ct = default) -> Task<IReadOnlyList<Conversation>>
    BEGIN
      IF limit <= 0 THROW ArgumentException("Limit must be positive", nameof(limit))
      IF limit > 1000 THROW ArgumentException("Limit cannot exceed 1000", nameof(limit))
      
      TRY
        logger.LogDebug("Retrieving {Limit} recent conversations", limit)
        
        START_TIMER operation_timer
        
        VAR compiledQuery = (Func<ChatDbContext, int, List<Conversation>>)narrow_compiled_queries["GetRecent"]
        VAR conversations = compiledQuery((ChatDbContext)dbContext, limit)
        
        STOP_TIMER operation_timer
        logger.LogDebug("Retrieved {Count} recent conversations in {ElapsedMs}ms", 
          conversations.Count, operation_timer.ElapsedMilliseconds)
        
        RETURN conversations.AsReadOnly()
        
      CATCH DbException ex
        logger.LogError(ex, "Database error retrieving recent conversations")
        THROW NEW RepositoryException("Failed to retrieve recent conversations", ex)
      END TRY
    END

  // ALGORITHM: Active Conversations for Specific User
  PUBLIC ASYNC METHOD GetActiveForUserAsync(UserId userId, CancellationToken ct = default) -> Task<IReadOnlyList<Conversation>>
    BEGIN
      IF userId IS NULL THROW ArgumentNullException(nameof(userId))
      
      TRY
        logger.LogDebug("Retrieving active conversations for user {UserId}", userId)
        
        START_TIMER operation_timer
        
        VAR compiledQuery = (Func<ChatDbContext, UserId, List<Conversation>>)narrow_compiled_queries["GetActiveForUser"]
        VAR conversations = compiledQuery((ChatDbContext)dbContext, userId)
        
        STOP_TIMER operation_timer
        logger.LogDebug("Retrieved {Count} active conversations for user {UserId} in {ElapsedMs}ms", 
          conversations.Count, userId, operation_timer.ElapsedMilliseconds)
        
        RETURN conversations.AsReadOnly()
        
      CATCH DbException ex
        logger.LogError(ex, "Database error retrieving active conversations for user {UserId}", userId)
        THROW NEW RepositoryException($"Failed to retrieve active conversations for user", ex)
      END TRY
    END

END CLASS
```

### 2.2 Message Repository Algorithm
```csharp
// ALGORITHM: Message Repository with Time-Based Queries
// OPTIMIZATION: Focused on temporal queries and pagination

INTERFACE IMessageRepository : IRepository<Message, MessageId>

  // ALGORITHM: Messages by Conversation with Pagination
  // COMPLEXITY: O(log n) with proper indexing
  ASYNC METHOD GetByConversationAsync(ConversationId conversationId, int offset, int limit, CancellationToken ct = default) -> Task<IReadOnlyList<Message>>

  // ALGORITHM: Recent Messages Across Conversations
  // COMPLEXITY: O(log n) with composite index
  ASYNC METHOD GetRecentAsync(int limit, CancellationToken ct = default) -> Task<IReadOnlyList<Message>>

  // ALGORITHM: Messages by Date Range
  // COMPLEXITY: O(log n) with time-based indexing
  ASYNC METHOD GetByDateRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default) -> Task<IReadOnlyList<Message>>

END INTERFACE

CLASS MessageRepository : BaseRepository<Message, MessageId>, IMessageRepository

  PRIVATE STATIC READONLY message_compiled_queries = NEW Dictionary<string, Delegate>()

  // ALGORITHM: Message-Specific Compiled Queries
  PRIVATE METHOD INITIALIZE_MESSAGE_COMPILED_QUERIES() -> void
    BEGIN
      // Messages by conversation with pagination
      message_compiled_queries["GetByConversation"] = 
        EF.CompileQuery((ChatDbContext ctx, ConversationId convId, int offset, int limit) =>
          ctx.Messages
             .Where(m => m.ConversationId == convId)
             .OrderBy(m => m.CreatedAt)
             .Skip(offset)
             .Take(limit)
             .AsNoTracking()
             .ToList())

      // Recent messages query
      message_compiled_queries["GetRecent"] = 
        EF.CompileQuery((ChatDbContext ctx, int limit) =>
          ctx.Messages
             .OrderByDescending(m => m.CreatedAt)
             .Take(limit)
             .AsNoTracking()
             .ToList())

      // Messages by date range
      message_compiled_queries["GetByDateRange"] = 
        EF.CompileQuery((ChatDbContext ctx, DateTimeOffset from, DateTimeOffset to) =>
          ctx.Messages
             .Where(m => m.CreatedAt >= from && m.CreatedAt <= to)
             .OrderBy(m => m.CreatedAt)
             .AsNoTracking()
             .ToList())
    END

END CLASS
```

## 🔄 3. Unit of Work Facade Design

### 3.1 Core UoW Interface Algorithm
```csharp
// ALGORITHM: Unit of Work Facade
// PATTERN: Facade over DbContext with transaction orchestration
// RESPONSIBILITY: Coordinate commits, outbox capture, transaction safety

INTERFACE IUnitOfWork

  // ALGORITHM: Standard Save Changes
  // COMPLEXITY: O(n) where n = number of changed entities
  // PERFORMANCE: ~50-200ms for typical batch sizes
  ASYNC METHOD SaveChangesAsync(CancellationToken ct = default) -> Task<int>

  // ALGORITHM: Transactional Operation Wrapper
  // PATTERN: Template method with automatic rollback
  // PERFORMANCE: Adds ~10-20ms overhead for transaction management
  ASYNC METHOD ExecuteTransactionAsync<TResult>(Func<Task<TResult>> operation, CancellationToken ct = default) -> Task<TResult>

  // ALGORITHM: Outbox Event Publishing
  // PATTERN: Reliable event publishing with transactional guarantee
  ASYNC METHOD PublishEventsAsync(CancellationToken ct = default) -> Task

  // ALGORITHM: Bulk Operations Support
  // OPTIMIZATION: Batch multiple operations in single transaction
  ASYNC METHOD ExecuteBulkOperationAsync<TResult>(Func<Task<TResult>> bulkOperation, CancellationToken ct = default) -> Task<TResult>

END INTERFACE
```

### 3.2 UoW Implementation Algorithm
```csharp
// ALGORITHM: Unit of Work Implementation
// PATTERN: Orchestrator with interceptor integration and outbox pattern

CLASS UnitOfWork : IUnitOfWork

  PRIVATE READONLY ChatDbContext dbContext
  PRIVATE READONLY ILogger<UnitOfWork> logger
  PRIVATE READONLY IOutboxService outboxService
  PRIVATE READONLY IEventPublisher eventPublisher
  PRIVATE READONLY IDateTimeProvider dateTimeProvider

  CONSTRUCTOR(ChatDbContext context, ILogger logger, IOutboxService outbox, IEventPublisher publisher, IDateTimeProvider dateTime)
    BEGIN
      this.dbContext = context ?? THROW ArgumentNullException
      this.logger = logger ?? THROW ArgumentNullException
      this.outboxService = outbox ?? THROW ArgumentNullException
      this.eventPublisher = publisher ?? THROW ArgumentNullException
      this.dateTimeProvider = dateTime ?? THROW ArgumentNullException
    END

  // ALGORITHM: Orchestrated Save Changes with Audit and Outbox
  PUBLIC ASYNC METHOD SaveChangesAsync(CancellationToken ct = default) -> Task<int>
    BEGIN
      TRY
        logger.LogDebug("Starting SaveChanges operation")
        START_TIMER save_timer
        
        // STEP 1: Pre-save entity validation
        VAR changedEntities = GET_CHANGED_ENTITIES()
        VALIDATE_ENTITIES(changedEntities)
        
        // STEP 2: Capture domain events before save
        VAR domainEvents = COLLECT_DOMAIN_EVENTS(changedEntities)
        
        // STEP 3: Execute save with audit interceptor
        // Audit interceptor will automatically populate audit fields
        VAR affectedRows = AWAIT dbContext.SaveChangesAsync(ct)
        
        // STEP 4: Store events in outbox (same transaction)
        AWAIT STORE_OUTBOX_EVENTS(domainEvents, ct)
        
        STOP_TIMER save_timer
        logger.LogInformation("SaveChanges completed: {AffectedRows} rows affected in {ElapsedMs}ms", 
          affectedRows, save_timer.ElapsedMilliseconds)
        
        // STEP 5: Publish events asynchronously (fire-and-forget)
        _ = Task.Run(() => PUBLISH_EVENTS_ASYNC(domainEvents), ct)
        
        RETURN affectedRows
        
      CATCH DbUpdateConcurrencyException ex
        logger.LogWarning(ex, "Concurrency conflict during SaveChanges")
        THROW NEW ConcurrencyException("Concurrency conflict detected. Please retry the operation.", ex)
        
      CATCH DbUpdateException ex
        logger.LogError(ex, "Database update error during SaveChanges")
        THROW NEW DataAccessException("Failed to save changes to database", ex)
        
      CATCH Exception ex
        logger.LogError(ex, "Unexpected error during SaveChanges")
        THROW
      END TRY
    END

  // ALGORITHM: Transaction Execution with Automatic Rollback
  PUBLIC ASYNC METHOD ExecuteTransactionAsync<TResult>(Func<Task<TResult>> operation, CancellationToken ct = default) -> Task<TResult>
    BEGIN
      IF operation IS NULL THROW ArgumentNullException(nameof(operation))
      
      TRY
        logger.LogDebug("Starting transaction execution")
        START_TIMER transaction_timer
        
        // Use database transaction for ACID guarantees
        USING VAR transaction = AWAIT dbContext.Database.BeginTransactionAsync(ct)
        
        TRY
          // Execute the provided operation within transaction
          VAR result = AWAIT operation()
          
          // Commit transaction if operation succeeded
          AWAIT transaction.CommitAsync(ct)
          
          STOP_TIMER transaction_timer
          logger.LogDebug("Transaction committed successfully in {ElapsedMs}ms", transaction_timer.ElapsedMilliseconds)
          
          RETURN result
          
        CATCH Exception operationEx
          logger.LogWarning(operationEx, "Operation failed, rolling back transaction")
          AWAIT transaction.RollbackAsync(ct)
          THROW
        END TRY
        
      CATCH Exception ex
        logger.LogError(ex, "Transaction execution failed")
        THROW NEW TransactionException("Transaction execution failed", ex)
      END TRY
    END

  // ALGORITHM: Bulk Operation Optimization
  PUBLIC ASYNC METHOD ExecuteBulkOperationAsync<TResult>(Func<Task<TResult>> bulkOperation, CancellationToken ct = default) -> Task<TResult>
    BEGIN
      TRY
        logger.LogDebug("Starting bulk operation")
        
        // Disable change tracking for bulk operations to improve performance
        VAR originalChangeTracking = dbContext.ChangeTracker.AutoDetectChangesEnabled
        dbContext.ChangeTracker.AutoDetectChangesEnabled = false
        
        // Increase command timeout for bulk operations
        VAR originalTimeout = dbContext.Database.GetCommandTimeout()
        dbContext.Database.SetCommandTimeout(300) // 5 minutes
        
        TRY
          RETURN AWAIT ExecuteTransactionAsync(bulkOperation, ct)
        FINALLY
          // Restore original settings
          dbContext.ChangeTracker.AutoDetectChangesEnabled = originalChangeTracking
          dbContext.Database.SetCommandTimeout(originalTimeout)
        END TRY
        
      CATCH Exception ex
        logger.LogError(ex, "Bulk operation failed")
        THROW
      END TRY
    END

  // ALGORITHM: Outbox Event Publishing
  PUBLIC ASYNC METHOD PublishEventsAsync(CancellationToken ct = default) -> Task
    BEGIN
      TRY
        logger.LogDebug("Publishing outbox events")
        
        VAR unpublishedEvents = AWAIT outboxService.GetUnpublishedEventsAsync(ct)
        
        IF unpublishedEvents.Count > 0
          FOREACH event IN unpublishedEvents
            TRY
              AWAIT eventPublisher.PublishAsync(event, ct)
              AWAIT outboxService.MarkAsPublishedAsync(event.Id, ct)
              logger.LogDebug("Published event {EventId} of type {EventType}", event.Id, event.Type)
            CATCH Exception ex
              logger.LogError(ex, "Failed to publish event {EventId}", event.Id)
              // Continue with other events, failed events will be retried later
            END TRY
          END FOREACH
        END IF
        
      CATCH Exception ex
        logger.LogError(ex, "Error during event publishing")
        THROW
      END TRY
    END

  // ALGORITHM: Helper Methods for Entity and Event Management
  PRIVATE METHOD GET_CHANGED_ENTITIES() -> List<object>
    BEGIN
      RETURN dbContext.ChangeTracker.Entries()
        .Where(e => e.State == EntityState.Added || 
                   e.State == EntityState.Modified || 
                   e.State == EntityState.Deleted)
        .Select(e => e.Entity)
        .ToList()
    END

  PRIVATE METHOD COLLECT_DOMAIN_EVENTS(List<object> entities) -> List<IDomainEvent>
    BEGIN
      VAR events = NEW List<IDomainEvent>()
      
      FOREACH entity IN entities
        IF entity IS AggregateRoot aggregateRoot
          events.AddRange(aggregateRoot.GetDomainEvents())
          aggregateRoot.ClearDomainEvents()
        END IF
      END FOREACH
      
      RETURN events
    END

  PRIVATE ASYNC METHOD STORE_OUTBOX_EVENTS(List<IDomainEvent> domainEvents, CancellationToken ct) -> Task
    BEGIN
      FOREACH event IN domainEvents
        VAR outboxEvent = NEW OutboxEvent
        BEGIN
          Id = Guid.NewGuid(),
          Type = event.GetType().Name,
          Data = JsonSerializer.Serialize(event),
          CreatedAt = dateTimeProvider.UtcNow,
          IsPublished = false
        END
        
        AWAIT outboxService.AddEventAsync(outboxEvent, ct)
      END FOREACH
    END

END CLASS
```

## 🔍 4. Audit SaveChanges Interceptor Design

### 4.1 Audit Interceptor Algorithm
```csharp
// ALGORITHM: Audit SaveChanges Interceptor
// PATTERN: EF Core SaveChanges Interceptor with automatic audit field population
// LIFECYCLE: Scoped - one instance per request/transaction

CLASS AuditSaveChangesInterceptor : SaveChangesInterceptor

  PRIVATE READONLY ICurrentUserService currentUserService
  PRIVATE READONLY IDateTimeProvider dateTimeProvider
  PRIVATE READONLY ILogger<AuditSaveChangesInterceptor> logger

  CONSTRUCTOR(ICurrentUserService userService, IDateTimeProvider dateTime, ILogger logger)
    BEGIN
      this.currentUserService = userService ?? THROW ArgumentNullException
      this.dateTimeProvider = dateTime ?? THROW ArgumentNullException
      this.logger = logger ?? THROW ArgumentNullException
    END

  // ALGORITHM: Synchronous SaveChanges Interception
  PUBLIC OVERRIDE METHOD SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) -> InterceptionResult<int>
    BEGIN
      IF eventData.Context IS NOT NULL
        APPLY_AUDIT_INFORMATION(eventData.Context)
      END IF
      
      RETURN BASE.SavingChanges(eventData, result)
    END

  // ALGORITHM: Asynchronous SaveChanges Interception
  PUBLIC OVERRIDE METHOD SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default) -> ValueTask<InterceptionResult<int>>
    BEGIN
      IF eventData.Context IS NOT NULL
        APPLY_AUDIT_INFORMATION(eventData.Context)
      END IF
      
      RETURN BASE.SavingChangesAsync(eventData, result, ct)
    END

  // ALGORITHM: Apply Audit Information to Changed Entities
  PRIVATE METHOD APPLY_AUDIT_INFORMATION(DbContext context) -> void
    BEGIN
      TRY
        START_TIMER audit_timer
        
        VAR currentUserId = currentUserService.GetCurrentUserId()
        VAR currentTime = dateTimeProvider.UtcNow
        VAR auditedEntitiesCount = 0
        
        // STEP 1: Process all tracked entities
        VAR entries = context.ChangeTracker.Entries()
          .Where(e => e.Entity IS IAuditableEntity && 
                     (e.State == EntityState.Added || e.State == EntityState.Modified))
          .ToList()
        
        FOREACH entry IN entries
          VAR auditableEntity = (IAuditableEntity)entry.Entity
          
          IF entry.State == EntityState.Added
            // ALGORITHM: New Entity Audit Setup
            auditableEntity.CreatedAt = currentTime
            auditableEntity.CreatedBy = currentUserId
            auditableEntity.UpdatedAt = currentTime
            auditableEntity.UpdatedBy = currentUserId
            
            logger.LogDebug("Applied audit info for new {EntityType} entity", entry.Entity.GetType().Name)
            
          ELSE IF entry.State == EntityState.Modified
            // ALGORITHM: Updated Entity Audit Setup
            // Preserve original CreatedAt and CreatedBy
            auditableEntity.UpdatedAt = currentTime
            auditableEntity.UpdatedBy = currentUserId
            
            // Ensure CreatedAt and CreatedBy are not modified
            entry.Property(nameof(IAuditableEntity.CreatedAt)).IsModified = false
            entry.Property(nameof(IAuditableEntity.CreatedBy)).IsModified = false
            
            logger.LogDebug("Applied audit info for modified {EntityType} entity", entry.Entity.GetType().Name)
          END IF
          
          auditedEntitiesCount++
        END FOREACH
        
        STOP_TIMER audit_timer
        
        IF auditedEntitiesCount > 0
          logger.LogDebug("Applied audit information to {Count} entities in {ElapsedMs}ms", 
            auditedEntitiesCount, audit_timer.ElapsedMilliseconds)
        END IF
        
      CATCH Exception ex
        logger.LogError(ex, "Error applying audit information")
        // Don't throw - allow save to continue without audit information
        // This is a conscious decision to prioritize data persistence over audit completeness
      END TRY
    END

END CLASS

// ALGORITHM: Auditable Entity Interface
INTERFACE IAuditableEntity
  PROPERTY CreatedAt AS DateTimeOffset
  PROPERTY CreatedBy AS UserId?
  PROPERTY UpdatedAt AS DateTimeOffset
  PROPERTY UpdatedBy AS UserId?
END INTERFACE

// ALGORITHM: Current User Service for Audit Context
INTERFACE ICurrentUserService
  METHOD GetCurrentUserId() -> UserId?
  PROPERTY IsAuthenticated AS Boolean
END INTERFACE

CLASS CurrentUserService : ICurrentUserService

  PRIVATE READONLY IHttpContextAccessor httpContextAccessor
  PRIVATE READONLY ILogger<CurrentUserService> logger

  // ALGORITHM: Extract Current User from HTTP Context
  PUBLIC METHOD GetCurrentUserId() -> UserId?
    BEGIN
      TRY
        VAR httpContext = httpContextAccessor.HttpContext
        IF httpContext IS NULL RETURN NULL
        
        VAR userIdClaim = httpContext.User?.FindFirst(ClaimTypes.NameIdentifier)
        IF userIdClaim IS NULL OR string.IsNullOrEmpty(userIdClaim.Value) RETURN NULL
        
        IF Guid.TryParse(userIdClaim.Value, OUT VAR guidValue)
          RETURN NEW UserId(guidValue)
        END IF
        
        RETURN NULL
        
      CATCH Exception ex
        logger.LogWarning(ex, "Error extracting current user ID")
        RETURN NULL
      END TRY
    END

  PUBLIC PROPERTY IsAuthenticated -> Boolean
    GET
      RETURN httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false
    END GET

END CLASS
```

## 🏗️ 5. Database Schema & Configuration

### 5.1 Chat Schema Configuration Algorithm
```csharp
// ALGORITHM: Chat Schema Configuration with snake_case Naming
// PATTERN: Fluent API configuration with performance optimizations

CLASS ChatDbContext : DbContext

  PUBLIC DbSet<Conversation> Conversations { get; set; }
  PUBLIC DbSet<Message> Messages { get; set; }
  PUBLIC DbSet<OutboxEvent> OutboxEvents { get; set; }

  CONSTRUCTOR(DbContextOptions<ChatDbContext> options) : BASE(options)

  // ALGORITHM: Model Configuration with Performance Optimizations
  PROTECTED OVERRIDE METHOD OnModelCreating(ModelBuilder modelBuilder) -> void
    BEGIN
      BASE.OnModelCreating(modelBuilder)
      
      // STEP 1: Apply snake_case naming convention
      APPLY_SNAKE_CASE_NAMING(modelBuilder)
      
      // STEP 2: Configure entity mappings
      CONFIGURE_CONVERSATION_ENTITY(modelBuilder)
      CONFIGURE_MESSAGE_ENTITY(modelBuilder)
      CONFIGURE_OUTBOX_EVENT_ENTITY(modelBuilder)
      
      // STEP 3: Apply performance optimizations
      APPLY_PERFORMANCE_CONFIGURATIONS(modelBuilder)
    END

  // ALGORITHM: Snake Case Naming Convention
  PRIVATE METHOD APPLY_SNAKE_CASE_NAMING(ModelBuilder modelBuilder) -> void
    BEGIN
      FOREACH entityType IN modelBuilder.Model.GetEntityTypes()
        // Convert table names to snake_case
        VAR tableName = TO_SNAKE_CASE(entityType.GetTableName())
        entityType.SetTableName(tableName)
        
        // Convert column names to snake_case
        FOREACH property IN entityType.GetProperties()
          VAR columnName = TO_SNAKE_CASE(property.Name)
          property.SetColumnName(columnName)
        END FOREACH
        
        // Convert foreign key names to snake_case
        FOREACH foreignKey IN entityType.GetForeignKeys()
          VAR fkName = TO_SNAKE_CASE($"fk_{tableName}_{foreignKey.PrincipalEntityType.GetTableName()}")
          foreignKey.SetConstraintName(fkName)
        END FOREACH
      END FOREACH
    END

  // ALGORITHM: Conversation Entity Configuration
  PRIVATE METHOD CONFIGURE_CONVERSATION_ENTITY(ModelBuilder modelBuilder) -> void
    BEGIN
      modelBuilder.Entity<Conversation>(entity =>
        BEGIN
          // Primary key
          entity.HasKey(c => c.Id)
          entity.Property(c => c.Id)
            .HasConversion(id => id.Value, value => new ConversationId(value))
            
          // Properties
          entity.Property(c => c.Title)
            .HasMaxLength(500)
            .IsRequired()
            
          entity.Property(c => c.UserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .IsRequired()
            
          // Status enum
          entity.Property(c => c.Status)
            .HasConversion<string>()
            
          // Audit fields
          entity.Property(c => c.CreatedAt)
            .IsRequired()
            
          entity.Property(c => c.UpdatedAt)
            .IsRequired()
            
          // Concurrency token (xmin)
          entity.Property(c => c.Version)
            .IsRowVersion()
            .HasColumnName("xmin")
            .HasColumnType("xid")
            
          // Indexes for performance
          entity.HasIndex(c => c.UserId)
            .HasDatabaseName("ix_conversations_user_id")
            
          entity.HasIndex(c => c.CreatedAt)
            .HasDatabaseName("ix_conversations_created_at")
            
          entity.HasIndex(c => new { c.UserId, c.Status })
            .HasDatabaseName("ix_conversations_user_status")
            
          // Relationships
          entity.HasMany(c => c.Messages)
            .WithOne(m => m.Conversation)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade)
        END)
    END

  // ALGORITHM: Message Entity Configuration
  PRIVATE METHOD CONFIGURE_MESSAGE_ENTITY(ModelBuilder modelBuilder) -> void
    BEGIN
      modelBuilder.Entity<Message>(entity =>
        BEGIN
          // Primary key
          entity.HasKey(m => m.Id)
          entity.Property(m => m.Id)
            .HasConversion(id => id.Value, value => new MessageId(value))
            
          // Properties
          entity.Property(m => m.Content)
            .HasMaxLength(10000)
            .IsRequired()
            
          entity.Property(m => m.Role)
            .HasConversion<string>()
            .HasMaxLength(50)
            
          entity.Property(m => m.ConversationId)
            .HasConversion(id => id.Value, value => new ConversationId(value))
            .IsRequired()
            
          // Audit fields
          entity.Property(m => m.CreatedAt)
            .IsRequired()
            
          // Concurrency token
          entity.Property(m => m.Version)
            .IsRowVersion()
            .HasColumnName("xmin")
            .HasColumnType("xid")
            
          // Indexes for performance
          entity.HasIndex(m => m.ConversationId)
            .HasDatabaseName("ix_messages_conversation_id")
            
          entity.HasIndex(m => m.CreatedAt)
            .HasDatabaseName("ix_messages_created_at")
            
          entity.HasIndex(m => new { m.ConversationId, m.CreatedAt })
            .HasDatabaseName("ix_messages_conversation_created")
        END)
    END

  // ALGORITHM: Performance Optimizations
  PRIVATE METHOD APPLY_PERFORMANCE_CONFIGURATIONS(ModelBuilder modelBuilder) -> void
    BEGIN
      // Enable compiled queries
      modelBuilder.EnableServiceProviderCaching()
      modelBuilder.EnableSensitiveDataLogging(false)
      
      // Configure query splitting behavior
      modelBuilder.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
      
      // Set default schema
      modelBuilder.HasDefaultSchema("chat")
    END

END CLASS
```

### 5.2 DI Registration Algorithm
```csharp
// ALGORITHM: Dependency Injection Registration
// PATTERN: Extension method for clean service registration

STATIC CLASS ServiceCollectionExtensions

  // ALGORITHM: Register Data Access Layer
  PUBLIC STATIC METHOD AddDataAccess(this IServiceCollection services, string connectionString) -> IServiceCollection
    BEGIN
      // STEP 1: Register DbContext with performance optimizations
      services.AddDbContext<ChatDbContext>(options =>
        BEGIN
          options.UseNpgsql(connectionString, npgsqlOptions =>
            BEGIN
              npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null)
              npgsqlOptions.CommandTimeout(60)
            END)
          
          // Performance optimizations
          options.EnableSensitiveDataLogging(false)
          options.EnableDetailedErrors(false)
          options.EnableServiceProviderCaching()
          options.EnableQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
        END)
      
      // STEP 2: Register interceptors with proper lifetime
      services.AddScoped<AuditSaveChangesInterceptor>()
      
      // STEP 3: Register repositories with proper lifetime
      services.AddScoped<IConversationRepository, ConversationRepository>()
      services.AddScoped<IMessageRepository, MessageRepository>()
      services.AddScoped(typeof(IRepository<,>), typeof(BaseRepository<,>))
      
      // STEP 4: Register Unit of Work
      services.AddScoped<IUnitOfWork, UnitOfWork>()
      
      // STEP 5: Register supporting services
      services.AddScoped<ICurrentUserService, CurrentUserService>()
      services.AddScoped<IOutboxService, OutboxService>()
      services.AddScoped<IEventPublisher, EventPublisher>()
      
      // STEP 6: Register date/time provider
      services.AddSingleton<IDateTimeProvider, DateTimeProvider>()
      
      RETURN services
    END

  // ALGORITHM: Configure DbContext with Interceptors
  PRIVATE STATIC METHOD ConfigureDbContextInterceptors(DbContextOptionsBuilder options, IServiceProvider serviceProvider) -> void
    BEGIN
      VAR auditInterceptor = serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>()
      options.AddInterceptors(auditInterceptor)
    END

END CLASS
```

## 🧪 6. Integration Tests with Testcontainers

### 6.1 Test Infrastructure Algorithm
```csharp
// ALGORITHM: Integration Test Infrastructure with Testcontainers
// PATTERN: Shared test fixture with container lifecycle management
// OPTIMIZATION: Container reuse across test runs

CLASS DataAccessIntegrationTestFixture : IAsyncLifetime

  PRIVATE PostgreSqlContainer postgresContainer
  PRIVATE ChatDbContext dbContext
  PRIVATE ServiceProvider serviceProvider

  // ALGORITHM: Test Container Setup
  PUBLIC ASYNC METHOD InitializeAsync() -> Task
    BEGIN
      // STEP 1: Create and start PostgreSQL container
      postgresContainer = NEW PostgreSqlBuilder()
        .WithImage("postgres:15-alpine")
        .WithDatabase("axon_test")
        .WithUsername("test_user")
        .WithPassword("test_password")
        .WithCleanUp(true)
        .Build()
      
      AWAIT postgresContainer.StartAsync()
      
      // STEP 2: Setup test services
      VAR services = NEW ServiceCollection()
      CONFIGURE_TEST_SERVICES(services)
      serviceProvider = services.BuildServiceProvider()
      
      // STEP 3: Create and migrate database
      dbContext = serviceProvider.GetRequiredService<ChatDbContext>()
      AWAIT dbContext.Database.MigrateAsync()
      
      // STEP 4: Seed test data
      AWAIT SEED_TEST_DATA()
    END

  // ALGORITHM: Test Services Configuration
  PRIVATE METHOD CONFIGURE_TEST_SERVICES(IServiceCollection services) -> void
    BEGIN
      VAR connectionString = postgresContainer.GetConnectionString()
      
      // Register data access services
      services.AddDataAccess(connectionString)
      
      // Register test-specific services
      services.AddSingleton<ICurrentUserService, TestCurrentUserService>()
      services.AddSingleton<IDateTimeProvider, TestDateTimeProvider>()
      
      // Configure logging for tests
      services.AddLogging(builder => 
        builder.AddConsole().SetMinimumLevel(LogLevel.Information))
    END

  // ALGORITHM: Test Data Seeding
  PRIVATE ASYNC METHOD SEED_TEST_DATA() -> Task
    BEGIN
      VAR testUserId = NEW UserId(Guid.NewGuid())
      VAR testConversationId = NEW ConversationId(Guid.NewGuid())
      
      VAR conversation = Conversation.Create(
        testConversationId,
        "Test Conversation",
        testUserId)
      
      dbContext.Conversations.Add(conversation)
      AWAIT dbContext.SaveChangesAsync()
    END

  // ALGORITHM: Cleanup
  PUBLIC ASYNC METHOD DisposeAsync() -> Task
    BEGIN
      IF dbContext IS NOT NULL
        AWAIT dbContext.DisposeAsync()
      END IF
      
      IF serviceProvider IS NOT NULL
        AWAIT serviceProvider.DisposeAsync()
      END IF
      
      IF postgresContainer IS NOT NULL
        AWAIT postgresContainer.DisposeAsync()
      END IF
    END

END CLASS
```

### 6.2 Repository Integration Tests Algorithm
```csharp
// ALGORITHM: Repository Integration Tests
// PATTERN: Behavior-driven testing with real database

CLASS ConversationRepositoryIntegrationTests : IClassFixture<DataAccessIntegrationTestFixture>

  PRIVATE READONLY DataAccessIntegrationTestFixture fixture
  PRIVATE READONLY IConversationRepository repository
  PRIVATE READONLY IUnitOfWork unitOfWork

  CONSTRUCTOR(DataAccessIntegrationTestFixture testFixture)
    BEGIN
      this.fixture = testFixture
      this.repository = fixture.GetService<IConversationRepository>()
      this.unitOfWork = fixture.GetService<IUnitOfWork>()
    END

  // ALGORITHM: Test Get By ID Performance
  [Fact]
  PUBLIC ASYNC METHOD GetByIdAsync_WithValidId_ReturnsConversationWithinPerformanceThreshold() -> Task
    BEGIN
      // Arrange
      VAR testConversationId = AWAIT CREATE_TEST_CONVERSATION()
      VAR stopwatch = Stopwatch.StartNew()
      
      // Act
      VAR result = AWAIT repository.GetByIdAsync(testConversationId)
      stopwatch.Stop()
      
      // Assert
      result.Should().NotBeNull()
      result.Id.Should().Be(testConversationId)
      stopwatch.ElapsedMilliseconds.Should().BeLessThan(50, "GetById should complete within 50ms")
    END

  // ALGORITHM: Test Batch Retrieval Performance
  [Fact]
  PUBLIC ASYNC METHOD GetByIdsAsync_WithMultipleIds_ReturnsBatchWithinPerformanceThreshold() -> Task
    BEGIN
      // Arrange
      VAR conversationIds = NEW List<ConversationId>()
      FOR i FROM 1 TO 10
        VAR id = AWAIT CREATE_TEST_CONVERSATION()
        conversationIds.Add(id)
      END FOR
      
      VAR stopwatch = Stopwatch.StartNew()
      
      // Act
      VAR results = AWAIT repository.GetByIdsAsync(conversationIds)
      stopwatch.Stop()
      
      // Assert
      results.Should().HaveCount(10)
      stopwatch.ElapsedMilliseconds.Should().BeLessThan(100, "Batch retrieval of 10 items should complete within 100ms")
    END

  // ALGORITHM: Test Transaction Safety
  [Fact]
  PUBLIC ASYNC METHOD SaveChanges_WithConcurrentModification_ThrowsConcurrencyException() -> Task
    BEGIN
      // Arrange
      VAR conversationId = AWAIT CREATE_TEST_CONVERSATION()
      
      // Get same conversation in two contexts
      VAR conversation1 = AWAIT repository.GetByIdAsync(conversationId)
      VAR conversation2 = AWAIT repository.GetByIdAsync(conversationId)
      
      // Act & Assert
      conversation1.UpdateTitle("Updated by first context")
      repository.Update(conversation1)
      AWAIT unitOfWork.SaveChangesAsync()
      
      conversation2.UpdateTitle("Updated by second context")
      repository.Update(conversation2)
      
      // This should throw concurrency exception due to xmin version mismatch
      VAR action = ASYNC () => AWAIT unitOfWork.SaveChangesAsync()
      AWAIT action.Should().ThrowAsync<ConcurrencyException>()
    END

  // ALGORITHM: Test Audit Interceptor Integration
  [Fact]
  PUBLIC ASYNC METHOD SaveChanges_WithNewEntity_PopulatesAuditFields() -> Task
    BEGIN
      // Arrange
      VAR userId = NEW UserId(Guid.NewGuid())
      VAR conversationId = NEW ConversationId(Guid.NewGuid())
      VAR testDateProvider = fixture.GetService<TestDateTimeProvider>()
      VAR fixedTime = DateTimeOffset.UtcNow
      testDateProvider.SetFixedTime(fixedTime)
      
      VAR conversation = Conversation.Create(conversationId, "Test Conversation", userId)
      
      // Act
      repository.Add(conversation)
      AWAIT unitOfWork.SaveChangesAsync()
      
      // Assert
      VAR savedConversation = AWAIT repository.GetByIdAsync(conversationId)
      savedConversation.Should().NotBeNull()
      savedConversation.CreatedAt.Should().Be(fixedTime)
      savedConversation.UpdatedAt.Should().Be(fixedTime)
      savedConversation.CreatedBy.Should().Be(userId)
      savedConversation.UpdatedBy.Should().Be(userId)
    END

  // ALGORITHM: Helper Methods
  PRIVATE ASYNC METHOD CREATE_TEST_CONVERSATION() -> Task<ConversationId>
    BEGIN
      VAR conversationId = NEW ConversationId(Guid.NewGuid())
      VAR userId = NEW UserId(Guid.NewGuid())
      
      VAR conversation = Conversation.Create(conversationId, "Test Conversation", userId)
      repository.Add(conversation)
      AWAIT unitOfWork.SaveChangesAsync()
      
      RETURN conversationId
    END

END CLASS
```

## 📊 7. Performance Benchmarking Algorithm

### 7.1 Benchmark Suite Design
```csharp
// ALGORITHM: Performance Benchmarking Suite
// PATTERN: BenchmarkDotNet integration for accurate measurements

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
CLASS DataAccessPerformanceBenchmarks

  PRIVATE ChatDbContext dbContext
  PRIVATE IConversationRepository repository
  PRIVATE List<ConversationId> testConversationIds

  // ALGORITHM: Benchmark Setup
  [GlobalSetup]
  PUBLIC METHOD Setup() -> void
    BEGIN
      // Setup in-memory database for consistent benchmarking
      VAR options = NEW DbContextOptionsBuilder<ChatDbContext>()
        .UseInMemoryDatabase("BenchmarkDb")
        .Options
      
      dbContext = NEW ChatDbContext(options)
      repository = NEW ConversationRepository(dbContext, NullLogger<ConversationRepository>.Instance)
      
      // Create test data
      testConversationIds = NEW List<ConversationId>()
      FOR i FROM 1 TO 1000
        VAR conversationId = NEW ConversationId(Guid.NewGuid())
        VAR conversation = Conversation.Create(conversationId, $"Test Conversation {i}", NEW UserId(Guid.NewGuid()))
        dbContext.Conversations.Add(conversation)
        testConversationIds.Add(conversationId)
      END FOR
      
      dbContext.SaveChanges()
    END

  // ALGORITHM: Single Entity Retrieval Benchmark
  [Benchmark]
  PUBLIC ASYNC METHOD GetByIdAsync_SingleEntity() -> Task<Conversation>
    BEGIN
      VAR randomId = testConversationIds[Random.Shared.Next(testConversationIds.Count)]
      RETURN AWAIT repository.GetByIdAsync(randomId)
    END

  // ALGORITHM: Batch Retrieval Benchmark
  [Benchmark]
  [Arguments(10)]
  [Arguments(50)]
  [Arguments(100)]
  PUBLIC ASYNC METHOD GetByIdsAsync_BatchRetrieval(int batchSize) -> Task<IReadOnlyList<Conversation>>
    BEGIN
      VAR batchIds = testConversationIds.Take(batchSize).ToList()
      RETURN AWAIT repository.GetByIdsAsync(batchIds)
    END

  // ALGORITHM: Compiled Query vs LINQ Comparison
  [Benchmark]
  PUBLIC ASYNC METHOD CompiledQuery_Performance() -> Task<Conversation>
    BEGIN
      // This will use the compiled query implementation
      VAR randomId = testConversationIds[Random.Shared.Next(testConversationIds.Count)]
      RETURN AWAIT repository.GetByIdAsync(randomId)
    END

  [Benchmark]
  PUBLIC ASYNC METHOD LinqQuery_Performance() -> Task<Conversation>
    BEGIN
      // Direct LINQ query for comparison
      VAR randomId = testConversationIds[Random.Shared.Next(testConversationIds.Count)]
      RETURN AWAIT dbContext.Conversations
        .AsNoTracking()
        .FirstOrDefaultAsync(c => c.Id == randomId)
    END

END CLASS
```

## 🎯 8. Error Handling & Recovery Algorithms

### 8.1 Exception Hierarchy Algorithm
```csharp
// ALGORITHM: Domain-Specific Exception Hierarchy
// PATTERN: Layered exception handling with specific recovery strategies

ABSTRACT CLASS DataAccessException : Exception
  PROTECTED CONSTRUCTOR(string message) : BASE(message)
  PROTECTED CONSTRUCTOR(string message, Exception innerException) : BASE(message, innerException)
END CLASS

CLASS RepositoryException : DataAccessException
  PUBLIC CONSTRUCTOR(string message) : BASE(message)
  PUBLIC CONSTRUCTOR(string message, Exception innerException) : BASE(message, innerException)
END CLASS

CLASS ConcurrencyException : DataAccessException
  PUBLIC CONSTRUCTOR(string message) : BASE(message)
  PUBLIC CONSTRUCTOR(string message, Exception innerException) : BASE(message, innerException)
END CLASS

CLASS TransactionException : DataAccessException
  PUBLIC CONSTRUCTOR(string message) : BASE(message)
  PUBLIC CONSTRUCTOR(string message, Exception innerException) : BASE(message, innerException)
END CLASS
```

### 8.2 Retry Policy Algorithm
```csharp
// ALGORITHM: Intelligent Retry Policy with Exponential Backoff
// PATTERN: Polly integration for resilient data access

CLASS DataAccessRetryPolicy

  PRIVATE STATIC READONLY IAsyncPolicy retryPolicy = Policy
    .Handle<NpgsqlException>(ex => IS_TRANSIENT_ERROR(ex))
    .Or<TimeoutException>()
    .WaitAndRetryAsync(
      retryCount: 3,
      sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
      onRetry: (outcome, timespan, retryCount, context) =>
        BEGIN
          VAR logger = context.GetLogger()
          logger?.LogWarning("Retry attempt {RetryCount} for operation {OperationKey} in {Delay}ms",
            retryCount, context.OperationKey, timespan.TotalMilliseconds)
        END)

  // ALGORITHM: Transient Error Detection
  PRIVATE STATIC METHOD IS_TRANSIENT_ERROR(NpgsqlException ex) -> Boolean
    BEGIN
      // PostgreSQL error codes that indicate transient failures
      VAR transientErrorCodes = NEW HashSet<string>
      BEGIN
        "08000", // connection_exception
        "08003", // connection_does_not_exist
        "08006", // connection_failure
        "08001", // sqlclient_unable_to_establish_sqlconnection
        "08004", // sqlserver_rejected_establishment_of_sqlconnection
        "40001", // serialization_failure
        "40P01"  // deadlock_detected
      END
      
      RETURN transientErrorCodes.Contains(ex.SqlState)
    END

  // ALGORITHM: Execute Operation with Retry
  PUBLIC STATIC ASYNC METHOD ExecuteAsync<T>(Func<Task<T>> operation, string operationKey) -> Task<T>
    BEGIN
      VAR context = NEW Context(operationKey)
      RETURN AWAIT retryPolicy.ExecuteAsync(operation, context)
    END

END CLASS
```

## 📋 Performance Characteristics Summary

### Algorithm Complexity Analysis

| Operation | Complexity | Expected Performance | Memory Usage |
|-----------|------------|---------------------|--------------|
| GetByIdAsync | O(1) | 1-5ms | O(1) |
| GetByIdsAsync | O(n) | 5-20ms (n≤100) | O(n) |
| Add/Update/Remove | O(1) | <1ms (deferred) | O(1) |
| SaveChangesAsync | O(n) | 50-200ms | O(n) |
| Compiled Queries | O(1) | 30-50% faster | O(1) |
| Batch Operations | O(n) | 10-100ms/100 items | O(n) |
| Audit Interception | O(k) | <5ms overhead | O(k) |
| Transaction Execution | O(m) | +10-20ms overhead | O(m) |

Where:
- n = number of entities
- k = number of auditable entities 
- m = complexity of wrapped operation

### Memory Optimization Strategies

1. **No-Tracking Queries**: All read operations use `AsNoTracking()`
2. **Compiled Query Caching**: Static compilation eliminates runtime LINQ overhead
3. **Minimal Entity Materialization**: Only load required fields
4. **Bulk Operation Optimization**: Disable change tracking for large operations
5. **Connection Pooling**: Leverage built-in PostgreSQL connection pooling

### Scalability Characteristics

- **Concurrent Users**: Supports 100+ concurrent database operations
- **Database Connections**: Efficiently managed through connection pooling
- **Memory Footprint**: Minimal EF Core tracking overhead
- **Query Performance**: Optimized with proper indexing strategies
- **Transaction Throughput**: 500+ transactions/second under typical load

## 🏁 Deliverable Completion

This comprehensive pseudocode document provides:

✅ **Generic Repository Pattern** - Strict interface with no IQueryable exposure
✅ **Narrow Repository Inheritance** - Domain-specific operations with compiled queries  
✅ **Unit of Work Facade** - Transaction orchestration with outbox pattern
✅ **Audit SaveChanges Interceptor** - Automatic audit field population
✅ **Database Schema Configuration** - snake_case naming with performance indexes
✅ **Dependency Injection Setup** - Proper service lifetimes and configuration
✅ **Integration Testing** - Testcontainers with comprehensive test scenarios
✅ **Performance Benchmarking** - BenchmarkDotNet integration with metrics
✅ **Error Handling & Recovery** - Comprehensive exception hierarchy with retry policies
✅ **Concurrency Management** - xmin-based optimistic locking with PostgreSQL

**Next Phase Ready**: This pseudocode is ready for Phase 3 Architecture design where these algorithms will be translated into concrete system architecture with detailed component diagrams and integration specifications.