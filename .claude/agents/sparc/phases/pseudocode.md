# 🧠 SPARC PHASE 2: COMPREHENSIVE PSEUDOCODE & ALGORITHM DESIGN

## 🎯 PHASE 2 MISSION CRITICAL: EVENT SOURCING + CQRS + AUDITING ALGORITHMS

**HUMAN APPROVAL STATUS**: ✅ ALL DESIGN DECISIONS APPROVED
- ✅ Abstraction hierarchy: IIdentifiable<TId> → BaseEntity<TId> → AuditableEntity<TId>
- ✅ EF Shadow Properties + SaveChangesInterceptor auditing 
- ✅ Repository pattern: Generic + Specialized with Unit of Work
- ✅ COMPREHENSIVE EVENT SOURCING: ALL conversation changes tracked
- ✅ CQRS read models for query optimization
- ✅ NO backward compatibility constraints - full modernization approved

---

## 📋 ALGORITHM 1: COMPREHENSIVE EVENT SOURCING CORE

### 1.1 Event Store Append Algorithm

```pseudocode
ALGORITHM: AppendEventToEventStore
INPUT: aggregateId (Guid), domainEvent (IDomainEvent), expectedVersion (long), userId (string)
OUTPUT: AppendResult (success: bool, newVersion: long, eventId: Guid)

BEGIN
    // 1. Validate inputs
    IF aggregateId.IsEmpty() OR domainEvent IS NULL:
        RETURN AppendResult.Failure("Invalid aggregate ID or domain event")
    
    // 2. Serialize event with metadata
    eventMetadata = CREATE EventMetadata:
        EventId = Guid.NewGuid()
        EventType = domainEvent.GetType().AssemblyQualifiedName
        AggregateType = "Conversation" // or infer from aggregate
        CorrelationId = domainEvent.CorrelationId ?? Guid.NewGuid()
        CausationId = domainEvent.CausationId ?? Guid.NewGuid()
        UserId = userId
        Timestamp = DateTime.UtcNow
        Version = expectedVersion + 1
    
    eventData = JsonSerializer.Serialize(domainEvent, JsonSerializerOptions.Web)
    
    // 3. Create ConversationEvent entity
    conversationEvent = CREATE ConversationEvent:
        Id = eventMetadata.EventId
        AggregateId = aggregateId
        EventType = eventMetadata.EventType
        EventData = eventData
        EventMetadata = JsonSerializer.Serialize(eventMetadata)
        Version = eventMetadata.Version
        OccurredAt = eventMetadata.Timestamp
        UserId = userId
        StreamPosition = 0 // Will be set by database sequence
    
    // 4. Optimistic concurrency check and append
    TRANSACTION BEGIN (IsolationLevel.Serializable)
    TRY:
        // Check current version for optimistic concurrency
        currentVersion = SELECT MAX(Version) FROM ConversationEvents 
                        WHERE AggregateId = aggregateId
        
        IF currentVersion != expectedVersion:
            ROLLBACK TRANSACTION
            RETURN AppendResult.ConcurrencyFailure(currentVersion, expectedVersion)
        
        // Insert new event
        INSERT conversationEvent INTO ConversationEvents
        
        // Update aggregate snapshot metadata (optional)
        UPSERT AggregateSnapshots SET:
            LastEventVersion = eventMetadata.Version,
            LastModified = eventMetadata.Timestamp
        WHERE AggregateId = aggregateId
        
        COMMIT TRANSACTION
        
        // 5. Publish event asynchronously (outside transaction)
        PUBLISH domainEvent TO event bus
        
        RETURN AppendResult.Success(eventMetadata.Version, eventMetadata.EventId)
        
    CATCH ConcurrencyException:
        ROLLBACK TRANSACTION
        RETURN AppendResult.ConcurrencyFailure("Concurrent modification detected")
        
    CATCH Exception ex:
        ROLLBACK TRANSACTION
        LOG ERROR "Event append failed" WITH aggregateId, eventType, ex
        RETURN AppendResult.Failure(ex.Message)
END
```

### 1.2 Aggregate Rehydration Algorithm

```pseudocode
ALGORITHM: RehydrateAggregateFromEventStore
INPUT: aggregateId (Guid), maxVersion (long?) = null
OUTPUT: TAggregate (fully reconstructed aggregate) OR null

BEGIN
    // 1. Check for recent snapshot first (performance optimization)
    snapshot = SELECT TOP 1 * FROM AggregateSnapshots 
              WHERE AggregateId = aggregateId 
              AND (maxVersion IS NULL OR Version <= maxVersion)
              ORDER BY Version DESC
    
    startFromVersion = 0
    aggregate = null
    
    IF snapshot IS NOT NULL AND snapshot.Version > 50: // Snapshot threshold
        // Deserialize from snapshot
        aggregate = JsonSerializer.Deserialize<TAggregate>(snapshot.Data)
        startFromVersion = snapshot.Version
    ELSE:
        // Create new aggregate instance
        aggregate = TAggregate.CreateEmpty(aggregateId)
    
    // 2. Load events from event store
    eventsQuery = SELECT * FROM ConversationEvents 
                 WHERE AggregateId = aggregateId 
                 AND Version > startFromVersion
    
    IF maxVersion IS NOT NULL:
        eventsQuery = eventsQuery AND Version <= maxVersion
    
    events = eventsQuery ORDER BY Version ASC
    
    // 3. Replay events to reconstruct state
    FOR EACH eventRecord IN events:
        TRY:
            // Deserialize domain event
            eventType = Type.GetType(eventRecord.EventType)
            domainEvent = JsonSerializer.Deserialize(eventRecord.EventData, eventType)
            
            // Apply event to aggregate (state transition)
            aggregate.ApplyEvent(domainEvent, fromHistory: true)
            
            // Update aggregate version
            aggregate.SetVersion(eventRecord.Version)
            
        CATCH Exception ex:
            LOG ERROR "Failed to apply event during rehydration" WITH 
                aggregateId, eventRecord.Version, eventRecord.EventType, ex
            THROW RehydrationException("Event replay failed at version " + eventRecord.Version)
    
    // 4. Mark aggregate as loaded from history
    aggregate.MarkAsLoadedFromHistory()
    
    RETURN aggregate
END
```

### 1.3 Snapshot Creation Algorithm

```pseudocode
ALGORITHM: CreateSnapshotIfNeeded
INPUT: aggregateId (Guid), currentVersion (long), aggregate (TAggregate)
OUTPUT: SnapshotResult (created: bool, snapshotVersion: long)

BEGIN
    CONST SNAPSHOT_FREQUENCY = 100 // Create snapshot every 100 events
    CONST MIN_EVENTS_FOR_SNAPSHOT = 20 // Minimum events before first snapshot
    
    // 1. Check if snapshot is needed
    lastSnapshot = SELECT TOP 1 * FROM AggregateSnapshots 
                  WHERE AggregateId = aggregateId 
                  ORDER BY Version DESC
    
    eventsSinceSnapshot = currentVersion - (lastSnapshot?.Version ?? 0)
    
    IF eventsSinceSnapshot < SNAPSHOT_FREQUENCY AND currentVersion > MIN_EVENTS_FOR_SNAPSHOT:
        RETURN SnapshotResult.NotNeeded()
    
    // 2. Create snapshot
    TRY:
        snapshotData = JsonSerializer.Serialize(aggregate, new JsonSerializerOptions {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        })
        
        snapshot = CREATE AggregateSnapshot:
            Id = Guid.NewGuid()
            AggregateId = aggregateId
            AggregateType = typeof(TAggregate).Name
            Version = currentVersion
            Data = snapshotData
            CreatedAt = DateTime.UtcNow
            CompressedSize = Compress(snapshotData).Length // Optional compression
        
        // 3. Save snapshot
        TRANSACTION BEGIN
            INSERT snapshot INTO AggregateSnapshots
            
            // Optional: Clean up old snapshots (keep last 3)
            oldSnapshots = SELECT * FROM AggregateSnapshots 
                          WHERE AggregateId = aggregateId 
                          ORDER BY Version DESC 
                          OFFSET 3
            
            DELETE FROM AggregateSnapshots WHERE Id IN oldSnapshots.Select(s => s.Id)
            
        COMMIT TRANSACTION
        
        LOG INFO "Snapshot created" WITH aggregateId, currentVersion, snapshotData.Length
        
        RETURN SnapshotResult.Created(currentVersion)
        
    CATCH Exception ex:
        LOG ERROR "Snapshot creation failed" WITH aggregateId, currentVersion, ex
        RETURN SnapshotResult.Failed(ex.Message)
END
```

---

## 📋 ALGORITHM 2: DOMAIN EVENT PUBLISHING & PROJECTION HANDLING

### 2.1 Domain Event Publishing Algorithm

```pseudocode
ALGORITHM: PublishDomainEventsAfterSaveChanges
INPUT: dbContext (ChatModuleDbContext), changeTracker (ChangeTracker)
OUTPUT: PublishResult (eventsPublished: int, failures: List<string>)

BEGIN
    publishedCount = 0
    failures = LIST<string>()
    
    // 1. Collect all aggregates with unpublished domain events
    aggregatesWithEvents = LIST<IAggregateRoot>()
    
    FOR EACH entry IN changeTracker.Entries():
        IF entry.Entity IMPLEMENTS IAggregateRoot:
            aggregate = CAST entry.Entity TO IAggregateRoot
            IF aggregate.DomainEvents.Any():
                aggregatesWithEvents.Add(aggregate)
    
    // 2. Publish each domain event
    FOR EACH aggregate IN aggregatesWithEvents:
        domainEvents = aggregate.DomainEvents.ToList() // Snapshot the events
        
        FOR EACH domainEvent IN domainEvents:
            TRY:
                // Add metadata to event
                domainEvent.SetMetadata(
                    AggregateId: aggregate.Id,
                    AggregateVersion: aggregate.Version,
                    UserId: GetCurrentUserId(),
                    Timestamp: DateTime.UtcNow,
                    CorrelationId: GetCorrelationId(),
                    CausationId: GetCausationId()
                )
                
                // Publish via MediatR
                AWAIT mediator.Publish(domainEvent)
                
                publishedCount++
                
                LOG DEBUG "Domain event published" WITH 
                    eventType: domainEvent.GetType().Name,
                    aggregateId: aggregate.Id,
                    version: aggregate.Version
                
            CATCH Exception ex:
                errorMessage = $"Failed to publish {domainEvent.GetType().Name}: {ex.Message}"
                failures.Add(errorMessage)
                
                LOG ERROR "Domain event publishing failed" WITH 
                    eventType: domainEvent.GetType().Name,
                    aggregateId: aggregate.Id,
                    exception: ex
        
        // 3. Clear domain events after publishing (success or failure)
        aggregate.ClearDomainEvents()
    
    RETURN PublishResult(publishedCount, failures)
END
```

### 2.2 CQRS Read Model Projection Handlers

```pseudocode
ALGORITHM: HandleMessageAddedEvent
INPUT: messageAddedEvent (MessageAddedEvent)
OUTPUT: ProjectionResult (success: bool, readModelId: Guid)

IMPLEMENTS INotificationHandler<MessageAddedEvent>

BEGIN
    TRY:
        // 1. Load or create conversation read model
        readModel = AWAIT readModelRepository.GetByIdAsync(messageAddedEvent.ConversationId)
        
        IF readModel IS NULL:
            // Create new read model from event
            readModel = CREATE ConversationReadModel:
                Id = messageAddedEvent.ConversationId
                CreatedAt = messageAddedEvent.OccurredAt
                CreatedBy = messageAddedEvent.UserId
                Title = messageAddedEvent.Content.Substring(0, Math.Min(100, messageAddedEvent.Content.Length))
                Status = ConversationStatus.Active
                MessageCount = 0
                LastMessageAt = messageAddedEvent.OccurredAt
                LastMessageBy = messageAddedEvent.UserId
                IsActive = true
                TotalTokensUsed = 0
                AverageResponseTime = TimeSpan.Zero
                SearchVector = "" // Will be updated below
                Tags = LIST<string>()
            
            readModelRepository.Add(readModel)
        
        // 2. Update read model with new message data
        readModel.MessageCount++
        readModel.LastMessageAt = messageAddedEvent.OccurredAt
        readModel.LastMessageBy = messageAddedEvent.UserId
        readModel.TotalTokensUsed += messageAddedEvent.TokensUsed
        
        // Update search vector for full-text search (PostgreSQL specific)
        readModel.UpdateSearchVector(messageAddedEvent.Content)
        
        // Calculate average response time if this is an assistant message
        IF messageAddedEvent.Role == MessageRole.Assistant AND readModel.MessageCount > 1:
            // Simple moving average calculation
            totalResponseTime = readModel.AverageResponseTime.TotalMilliseconds * (readModel.MessageCount - 1)
            newAverage = (totalResponseTime + messageAddedEvent.ResponseTime.TotalMilliseconds) / readModel.MessageCount
            readModel.AverageResponseTime = TimeSpan.FromMilliseconds(newAverage)
        
        // 3. Update conversation title if this is the first message
        IF readModel.MessageCount == 1:
            readModel.Title = ExtractTitleFromMessage(messageAddedEvent.Content)
        
        // 4. Extract and update tags from message content
        newTags = ExtractTagsFromMessage(messageAddedEvent.Content)
        FOR EACH tag IN newTags:
            IF NOT readModel.Tags.Contains(tag):
                readModel.Tags.Add(tag)
        
        // 5. Save changes
        AWAIT readModelRepository.SaveChangesAsync()
        
        LOG DEBUG "ConversationReadModel updated" WITH 
            conversationId: messageAddedEvent.ConversationId,
            messageCount: readModel.MessageCount,
            tokensUsed: messageAddedEvent.TokensUsed
        
        RETURN ProjectionResult.Success(readModel.Id)
        
    CATCH Exception ex:
        LOG ERROR "MessageAddedEvent projection failed" WITH 
            conversationId: messageAddedEvent.ConversationId,
            messageId: messageAddedEvent.MessageId,
            exception: ex
        
        RETURN ProjectionResult.Failure(ex.Message)
END

ALGORITHM: HandleConversationCompletedEvent
INPUT: completedEvent (ConversationCompletedEvent)
OUTPUT: ProjectionResult (success: bool, readModelId: Guid)

IMPLEMENTS INotificationHandler<ConversationCompletedEvent>

BEGIN
    TRY:
        // 1. Load conversation read model
        readModel = AWAIT readModelRepository.GetByIdAsync(completedEvent.ConversationId)
        
        IF readModel IS NULL:
            LOG WARNING "ConversationReadModel not found for completion event" WITH 
                conversationId: completedEvent.ConversationId
            RETURN ProjectionResult.NotFound()
        
        // 2. Update completion data
        readModel.Status = ConversationStatus.Completed
        readModel.CompletedAt = completedEvent.CompletedAt
        readModel.IsActive = false
        readModel.FinalMessageCount = completedEvent.MessageCount
        readModel.TotalDuration = completedEvent.Duration
        readModel.TotalTokensUsed = completedEvent.TotalTokensUsed
        readModel.TotalToolExecutionTime = completedEvent.TotalToolExecutionTime
        
        // 3. Calculate final statistics
        IF readModel.MessageCount > 0:
            readModel.AverageMessagesPerMinute = readModel.MessageCount / Math.Max(1, readModel.TotalDuration.TotalMinutes)
            readModel.AverageTokensPerMessage = readModel.TotalTokensUsed / readModel.MessageCount
        
        // 4. Update search vector with completion summary
        IF NOT string.IsNullOrEmpty(completedEvent.Summary):
            readModel.UpdateSearchVector(completedEvent.Summary)
            readModel.Summary = completedEvent.Summary
        
        // 5. Save changes
        AWAIT readModelRepository.SaveChangesAsync()
        
        LOG INFO "Conversation completed" WITH 
            conversationId: completedEvent.ConversationId,
            duration: completedEvent.Duration,
            messageCount: completedEvent.MessageCount,
            tokensUsed: completedEvent.TotalTokensUsed
        
        RETURN ProjectionResult.Success(readModel.Id)
        
    CATCH Exception ex:
        LOG ERROR "ConversationCompletedEvent projection failed" WITH 
            conversationId: completedEvent.ConversationId,
            exception: ex
        
        RETURN ProjectionResult.Failure(ex.Message)
END
```

---

## 📋 ALGORITHM 3: EF SHADOW PROPERTIES AUDITING

### 3.1 SaveChangesInterceptor Auditing Algorithm

```pseudocode
ALGORITHM: AuditSaveChangesInterceptor.SavingChanges
INPUT: eventData (DbContextEventData), result (InterceptionResult<int>)
OUTPUT: InterceptionResult<int> (continue processing or modified result)

INHERITS SaveChangesInterceptor

BEGIN
    dbContext = eventData.Context
    changeTracker = dbContext.ChangeTracker
    currentUser = GetCurrentUserFromContext() // From HTTP context or DI
    timestamp = DateTime.UtcNow
    
    // 1. Get all auditable entities that have changed
    auditableEntries = changeTracker.Entries()
                      .Where(e => e.Entity IMPLEMENTS IAuditable)
                      .Where(e => e.State IN [Added, Modified, Deleted])
                      .ToList()
    
    IF auditableEntries.Count == 0:
        RETURN result // No auditable changes, continue normally
    
    // 2. Apply audit information to each entity
    FOR EACH entry IN auditableEntries:
        entity = entry.Entity
        entityType = entry.Context.Model.FindEntityType(entity.GetType())
        
        SWITCH entry.State:
            CASE Added:
                // Set creation audit fields
                entry.Property("CreatedAt").CurrentValue = timestamp
                entry.Property("CreatedBy").CurrentValue = currentUser.Id
                entry.Property("UpdatedAt").CurrentValue = timestamp
                entry.Property("UpdatedBy").CurrentValue = currentUser.Id
                
                // Initialize soft delete fields as null
                entry.Property("DeletedAt").CurrentValue = null
                entry.Property("DeletedBy").CurrentValue = null
                
                LOG DEBUG "Audit fields set for new entity" WITH 
                    entityType: entity.GetType().Name,
                    entityId: GetEntityId(entity),
                    userId: currentUser.Id
                
            CASE Modified:
                // Preserve creation audit fields, update modification fields
                entry.Property("UpdatedAt").CurrentValue = timestamp
                entry.Property("UpdatedBy").CurrentValue = currentUser.Id
                
                // Check if this is actually a soft delete
                deletedAtProperty = entry.Property("DeletedAt")
                IF deletedAtProperty.IsModified AND deletedAtProperty.CurrentValue IS NOT NULL:
                    entry.Property("DeletedBy").CurrentValue = currentUser.Id
                
                LOG DEBUG "Audit fields updated for modified entity" WITH 
                    entityType: entity.GetType().Name,
                    entityId: GetEntityId(entity),
                    userId: currentUser.Id
                
            CASE Deleted:
                // Convert hard delete to soft delete
                entry.State = EntityState.Modified
                
                // Set soft delete audit fields
                entry.Property("DeletedAt").CurrentValue = timestamp
                entry.Property("DeletedBy").CurrentValue = currentUser.Id
                entry.Property("UpdatedAt").CurrentValue = timestamp
                entry.Property("UpdatedBy").CurrentValue = currentUser.Id
                
                LOG INFO "Hard delete converted to soft delete" WITH 
                    entityType: entity.GetType().Name,
                    entityId: GetEntityId(entity),
                    userId: currentUser.Id
    
    // 3. Validate audit fields are properly configured
    FOR EACH entry IN auditableEntries:
        ValidateAuditFields(entry, entityType)
    
    // 4. Continue with normal SaveChanges processing
    RETURN result
    
CATCH Exception ex:
    LOG ERROR "Audit interceptor failed" WITH 
        entitiesCount: auditableEntries.Count,
        userId: currentUser?.Id,
        exception: ex
    
    // Don't block SaveChanges due to audit failures
    RETURN result
END

ALGORITHM: ValidateAuditFields
INPUT: entityEntry (EntityEntry), entityType (IEntityType)
OUTPUT: void (throws exception if validation fails)

BEGIN
    // Validate required audit fields are present
    requiredFields = ["CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy"]
    
    FOR EACH fieldName IN requiredFields:
        property = entityEntry.Property(fieldName)
        IF property.CurrentValue IS NULL AND entityEntry.State != EntityState.Added:
            THROW AuditException($"Required audit field {fieldName} is null for {entityType.Name}")
    
    // Validate CreatedAt <= UpdatedAt
    createdAt = entityEntry.Property("CreatedAt").CurrentValue AS DateTime
    updatedAt = entityEntry.Property("UpdatedAt").CurrentValue AS DateTime
    
    IF createdAt > updatedAt:
        THROW AuditException($"CreatedAt ({createdAt}) cannot be after UpdatedAt ({updatedAt})")
    
    // Validate soft delete consistency
    deletedAt = entityEntry.Property("DeletedAt").CurrentValue AS DateTime?
    deletedBy = entityEntry.Property("DeletedBy").CurrentValue AS string
    
    IF (deletedAt IS NULL) != (deletedBy IS NULL):
        THROW AuditException("DeletedAt and DeletedBy must both be set or both be null")
END
```

### 3.2 Shadow Properties Configuration Algorithm

```pseudocode
ALGORITHM: ConfigureAuditShadowProperties
INPUT: entityTypeBuilder (EntityTypeBuilder<T>), databaseProvider (string)
OUTPUT: void (configures shadow properties)

WHERE T : IAuditable

BEGIN
    // 1. Configure CreatedAt shadow property
    entityTypeBuilder.Property<DateTime>("CreatedAt")
        .IsRequired()
        .HasColumnName("created_at")
        .HasComment("Timestamp when the entity was created")
    
    // Set database-specific default value
    SWITCH databaseProvider:
        CASE "PostgreSQL":
            entityTypeBuilder.Property<DateTime>("CreatedAt")
                .HasDefaultValueSql("NOW()")
        CASE "SqlServer":
            entityTypeBuilder.Property<DateTime>("CreatedAt")
                .HasDefaultValueSql("GETUTCDATE()")
        CASE "SQLite":
            entityTypeBuilder.Property<DateTime>("CreatedAt")
                .HasDefaultValueSql("datetime('now')")
    
    // 2. Configure CreatedBy shadow property
    entityTypeBuilder.Property<string>("CreatedBy")
        .IsRequired()
        .HasMaxLength(256)
        .HasColumnName("created_by")
        .HasComment("User ID who created the entity")
    
    // 3. Configure UpdatedAt shadow property
    entityTypeBuilder.Property<DateTime>("UpdatedAt")
        .IsRequired()
        .HasColumnName("updated_at")
        .HasComment("Timestamp when the entity was last updated")
    
    // 4. Configure UpdatedBy shadow property
    entityTypeBuilder.Property<string>("UpdatedBy")
        .IsRequired()
        .HasMaxLength(256)
        .HasColumnName("updated_by")
        .HasComment("User ID who last updated the entity")
    
    // 5. Configure soft delete shadow properties
    entityTypeBuilder.Property<DateTime?>("DeletedAt")
        .HasColumnName("deleted_at")
        .HasComment("Timestamp when the entity was soft deleted (null if not deleted)")
    
    entityTypeBuilder.Property<string>("DeletedBy")
        .HasMaxLength(256)
        .HasColumnName("deleted_by")
        .HasComment("User ID who soft deleted the entity")
    
    // 6. Create database indexes for audit queries
    entityTypeBuilder.HasIndex("CreatedAt", "CreatedBy")
        .HasDatabaseName($"IX_{typeof(T).Name}_Audit_Created")
    
    entityTypeBuilder.HasIndex("UpdatedAt", "UpdatedBy")
        .HasDatabaseName($"IX_{typeof(T).Name}_Audit_Updated")
    
    entityTypeBuilder.HasIndex("DeletedAt")
        .HasDatabaseName($"IX_{typeof(T).Name}_SoftDelete")
        .HasFilter("deleted_at IS NOT NULL") // Partial index for non-deleted entities
    
    // 7. Configure global query filter for soft delete
    entityTypeBuilder.HasQueryFilter(e => EF.Property<DateTime?>(e, "DeletedAt") == null)
    
    LOG DEBUG "Audit shadow properties configured" WITH 
        entityType: typeof(T).Name,
        databaseProvider: databaseProvider
END
```

---

## 📋 ALGORITHM 4: REPOSITORY PATTERN WITH UNIT OF WORK

### 4.1 Generic Repository Implementation Algorithm

```pseudocode
ALGORITHM: GenericRepository<TEntity, TId>.GetByIdAsync
INPUT: id (TId), cancellationToken (CancellationToken)
OUTPUT: Task<TEntity?> (entity or null if not found)

WHERE TEntity : BaseEntity<TId>
WHERE TId : IEquatable<TId>

BEGIN
    TRY:
        // 1. Check if entity implements soft delete
        IF TEntity IMPLEMENTS IAuditable:
            // Query with soft delete filter (handled by global query filter)
            entity = AWAIT dbSet
                .Where(e => e.Id.Equals(id))
                .FirstOrDefaultAsync(cancellationToken)
        ELSE:
            // Simple lookup for non-auditable entities
            entity = AWAIT dbSet.FindAsync(new object[] { id }, cancellationToken)
        
        // 2. Log access for auditing
        IF entity IS NOT NULL:
            LOG DEBUG "Entity retrieved" WITH 
                entityType: typeof(TEntity).Name,
                entityId: id,
                found: true
        ELSE:
            LOG DEBUG "Entity not found" WITH 
                entityType: typeof(TEntity).Name,
                entityId: id,
                found: false
        
        RETURN entity
        
    CATCH Exception ex:
        LOG ERROR "Failed to retrieve entity by ID" WITH 
            entityType: typeof(TEntity).Name,
            entityId: id,
            exception: ex
        THROW RepositoryException($"Failed to retrieve {typeof(TEntity).Name} with ID {id}", ex)
END

ALGORITHM: GenericRepository<TEntity, TId>.GetAllAsync
INPUT: specification (ISpecification<TEntity>)?, cancellationToken (CancellationToken)
OUTPUT: Task<IReadOnlyList<TEntity>>

BEGIN
    TRY:
        query = dbSet.AsQueryable()
        
        // 1. Apply specification if provided
        IF specification IS NOT NULL:
            query = specification.Apply(query)
        
        // 2. Apply default ordering by ID
        query = query.OrderBy(e => e.Id)
        
        // 3. Execute query
        entities = AWAIT query.ToListAsync(cancellationToken)
        
        LOG DEBUG "Entities retrieved" WITH 
            entityType: typeof(TEntity).Name,
            count: entities.Count,
            hasSpecification: specification IS NOT NULL
        
        RETURN entities.AsReadOnly()
        
    CATCH Exception ex:
        LOG ERROR "Failed to retrieve entities" WITH 
            entityType: typeof(TEntity).Name,
            specificationName: specification?.GetType().Name,
            exception: ex
        THROW RepositoryException($"Failed to retrieve {typeof(TEntity).Name} entities", ex)
END

ALGORITHM: GenericRepository<TEntity, TId>.AddAsync
INPUT: entity (TEntity), cancellationToken (CancellationToken)
OUTPUT: Task<TEntity> (added entity with generated ID if applicable)

BEGIN
    TRY:
        // 1. Validate entity
        IF entity IS NULL:
            THROW ArgumentNullException(nameof(entity))
        
        // 2. Set ID if it's a new entity with Guid ID
        IF typeof(TId) == typeof(Guid) AND entity.Id.Equals(default(TId)):
            entity.Id = (TId)(object)Guid.NewGuid()
        
        // 3. Add to DbSet
        entityEntry = dbSet.Add(entity)
        
        // 4. Log addition
        LOG DEBUG "Entity added to context" WITH 
            entityType: typeof(TEntity).Name,
            entityId: entity.Id
        
        RETURN entity
        
    CATCH Exception ex:
        LOG ERROR "Failed to add entity" WITH 
            entityType: typeof(TEntity).Name,
            entityId: entity?.Id,
            exception: ex
        THROW RepositoryException($"Failed to add {typeof(TEntity).Name}", ex)
END

ALGORITHM: GenericRepository<TEntity, TId>.UpdateAsync
INPUT: entity (TEntity), cancellationToken (CancellationToken)
OUTPUT: Task<TEntity>

BEGIN
    TRY:
        // 1. Validate entity
        IF entity IS NULL:
            THROW ArgumentNullException(nameof(entity))
        
        // 2. Check if entity exists
        existingEntity = AWAIT GetByIdAsync(entity.Id, cancellationToken)
        IF existingEntity IS NULL:
            THROW EntityNotFoundException($"{typeof(TEntity).Name} with ID {entity.Id} not found")
        
        // 3. Update entity
        dbContext.Entry(existingEntity).CurrentValues.SetValues(entity)
        
        // 4. Handle domain events if entity is an aggregate root
        IF entity IMPLEMENTS IAggregateRoot:
            aggregateRoot = entity AS IAggregateRoot
            // Copy domain events from new entity to existing entity
            existingAggregateRoot = existingEntity AS IAggregateRoot
            existingAggregateRoot.ClearDomainEvents()
            FOR EACH domainEvent IN aggregateRoot.DomainEvents:
                existingAggregateRoot.AddDomainEvent(domainEvent)
        
        LOG DEBUG "Entity updated" WITH 
            entityType: typeof(TEntity).Name,
            entityId: entity.Id
        
        RETURN existingEntity
        
    CATCH Exception ex:
        LOG ERROR "Failed to update entity" WITH 
            entityType: typeof(TEntity).Name,
            entityId: entity?.Id,
            exception: ex
        THROW RepositoryException($"Failed to update {typeof(TEntity).Name}", ex)
END
```

### 4.2 Specialized Repository Implementation Algorithm

```pseudocode
ALGORITHM: ConversationRepository.GetWithMessagesAsync
INPUT: conversationId (Guid), cancellationToken (CancellationToken)
OUTPUT: Task<Conversation?> (conversation with messages loaded)

INHERITS GenericRepository<Conversation, Guid>

BEGIN
    TRY:
        // 1. Load conversation with all related data
        conversation = AWAIT dbSet
            .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
            .Include(c => c.ToolExecutions)
            .Where(c => c.Id == conversationId)
            .FirstOrDefaultAsync(cancellationToken)
        
        // 2. Load additional data if needed
        IF conversation IS NOT NULL:
            // Load message counts for statistics
            messageCount = conversation.Messages.Count
            lastMessageAt = conversation.Messages.LastOrDefault()?.CreatedAt
            
            // Update conversation statistics (optional)
            conversation.UpdateStatistics(messageCount, lastMessageAt)
        
        LOG DEBUG "Conversation loaded with messages" WITH 
            conversationId: conversationId,
            found: conversation IS NOT NULL,
            messageCount: conversation?.Messages.Count ?? 0
        
        RETURN conversation
        
    CATCH Exception ex:
        LOG ERROR "Failed to load conversation with messages" WITH 
            conversationId: conversationId,
            exception: ex
        THROW RepositoryException($"Failed to load conversation {conversationId} with messages", ex)
END

ALGORITHM: ConversationRepository.GetActiveConversationsAsync
INPUT: userId (string), limit (int), cancellationToken (CancellationToken)
OUTPUT: Task<IReadOnlyList<Conversation>> (active conversations for user)

BEGIN
    TRY:
        // 1. Query active conversations for user
        conversations = AWAIT dbSet
            .Where(c => c.CreatedBy == userId)
            .Where(c => c.Status == ConversationStatus.Active)
            .OrderByDescending(c => c.LastMessageAt)
            .Take(limit)
            .ToListAsync(cancellationToken)
        
        LOG DEBUG "Active conversations retrieved" WITH 
            userId: userId,
            count: conversations.Count,
            limit: limit
        
        RETURN conversations.AsReadOnly()
        
    CATCH Exception ex:
        LOG ERROR "Failed to retrieve active conversations" WITH 
            userId: userId,
            limit: limit,
            exception: ex
        THROW RepositoryException($"Failed to retrieve active conversations for user {userId}", ex)
END

ALGORITHM: ConversationRepository.GetConversationStatisticsAsync
INPUT: conversationId (Guid), cancellationToken (CancellationToken)
OUTPUT: Task<ConversationStatistics> (aggregated conversation metrics)

BEGIN
    TRY:
        // 1. Load conversation with statistics data
        conversation = AWAIT dbSet
            .Include(c => c.Messages)
            .Include(c => c.ToolExecutions)
            .Where(c => c.Id == conversationId)
            .FirstOrDefaultAsync(cancellationToken)
        
        IF conversation IS NULL:
            THROW EntityNotFoundException($"Conversation {conversationId} not found")
        
        // 2. Calculate statistics
        statistics = CREATE ConversationStatistics:
            ConversationId = conversationId
            MessageCount = conversation.Messages.Count
            TotalTokensUsed = conversation.Messages.Sum(m => m.TokensUsed)
            AverageTokensPerMessage = conversation.Messages.Count > 0 ? 
                conversation.Messages.Average(m => m.TokensUsed) : 0
            
            ToolExecutionCount = conversation.ToolExecutions.Count
            TotalToolExecutionTime = conversation.ToolExecutions.Sum(te => te.ExecutionTime.TotalMilliseconds)
            AverageToolExecutionTime = conversation.ToolExecutions.Count > 0 ?
                conversation.ToolExecutions.Average(te => te.ExecutionTime.TotalMilliseconds) : 0
            
            FirstMessageAt = conversation.Messages.Min(m => m.CreatedAt)
            LastMessageAt = conversation.Messages.Max(m => m.CreatedAt)
            Duration = conversation.Messages.Count > 1 ?
                conversation.Messages.Max(m => m.CreatedAt) - conversation.Messages.Min(m => m.CreatedAt) :
                TimeSpan.Zero
        
        LOG DEBUG "Conversation statistics calculated" WITH 
            conversationId: conversationId,
            messageCount: statistics.MessageCount,
            totalTokens: statistics.TotalTokensUsed
        
        RETURN statistics
        
    CATCH Exception ex:
        LOG ERROR "Failed to calculate conversation statistics" WITH 
            conversationId: conversationId,
            exception: ex
        THROW RepositoryException($"Failed to calculate statistics for conversation {conversationId}", ex)
END
```

### 4.3 Unit of Work Implementation Algorithm

```pseudocode
ALGORITHM: UnitOfWork.SaveChangesAsync
INPUT: cancellationToken (CancellationToken)
OUTPUT: Task<int> (number of entities affected)

BEGIN
    changeCount = 0
    
    TRY:
        // 1. Begin transaction if not already in one
        IF dbContext.Database.CurrentTransaction IS NULL:
            USING transaction = AWAIT dbContext.Database.BeginTransactionAsync(cancellationToken):
                changeCount = AWAIT SaveChangesInternal(cancellationToken)
                AWAIT transaction.CommitAsync(cancellationToken)
        ELSE:
            // Already in transaction, just save
            changeCount = AWAIT SaveChangesInternal(cancellationToken)
        
        LOG INFO "Unit of Work saved successfully" WITH 
            changeCount: changeCount,
            hasTransaction: dbContext.Database.CurrentTransaction IS NOT NULL
        
        RETURN changeCount
        
    CATCH Exception ex:
        LOG ERROR "Unit of Work save failed" WITH 
            exception: ex,
            hasTransaction: dbContext.Database.CurrentTransaction IS NOT NULL
        
        // Transaction will be rolled back automatically by USING statement
        THROW UnitOfWorkException("Failed to save changes", ex)
END

ALGORITHM: UnitOfWork.SaveChangesInternal
INPUT: cancellationToken (CancellationToken)
OUTPUT: Task<int> (number of entities affected)

BEGIN
    // 1. Collect aggregates with domain events before saving
    aggregatesWithEvents = dbContext.ChangeTracker.Entries()
        .Where(e => e.Entity IMPLEMENTS IAggregateRoot)
        .Where(e => ((IAggregateRoot)e.Entity).DomainEvents.Any())
        .Select(e => (IAggregateRoot)e.Entity)
        .ToList()
    
    // 2. Save changes to database (triggers audit interceptor)
    changeCount = AWAIT dbContext.SaveChangesAsync(cancellationToken)
    
    // 3. Append domain events to event store
    FOR EACH aggregate IN aggregatesWithEvents:
        FOR EACH domainEvent IN aggregate.DomainEvents:
            AWAIT eventStore.AppendEventAsync(
                aggregate.Id,
                domainEvent,
                aggregate.Version,
                GetCurrentUserId(),
                cancellationToken)
    
    // 4. Publish domain events (for read model projections)
    publishResult = AWAIT PublishDomainEventsAfterSaveChanges(dbContext, dbContext.ChangeTracker)
    
    IF publishResult.Failures.Any():
        LOG WARNING "Some domain events failed to publish" WITH 
            publishedCount: publishResult.EventsPublished,
            failureCount: publishResult.Failures.Count,
            failures: publishResult.Failures
    
    // 5. Create snapshots if needed
    FOR EACH aggregate IN aggregatesWithEvents:
        AWAIT eventStore.CreateSnapshotIfNeeded(aggregate.Id, aggregate.Version, aggregate)
    
    RETURN changeCount
END

ALGORITHM: UnitOfWork.GetRepository<TEntity, TId>
INPUT: none
OUTPUT: IRepository<TEntity, TId>

WHERE TEntity : BaseEntity<TId>
WHERE TId : IEquatable<TId>

BEGIN
    repositoryType = typeof(IRepository<TEntity, TId>)
    
    // 1. Check if repository is already cached
    IF repositories.ContainsKey(repositoryType):
        RETURN repositories[repositoryType] AS IRepository<TEntity, TId>
    
    // 2. Create new repository instance
    repository = serviceProvider.GetService<IRepository<TEntity, TId>>()
    
    IF repository IS NULL:
        // Fall back to generic repository
        repository = CREATE GenericRepository<TEntity, TId>(dbContext, logger)
    
    // 3. Cache repository for reuse
    repositories[repositoryType] = repository
    
    LOG DEBUG "Repository created" WITH 
        entityType: typeof(TEntity).Name,
        repositoryType: repository.GetType().Name
    
    RETURN repository
END
```

---

## 📋 ALGORITHM 5: TRANSACTION COORDINATION & ERROR HANDLING

### 5.1 Distributed Transaction Coordination Algorithm

```pseudocode
ALGORITHM: TransactionCoordinator.ExecuteInTransactionAsync
INPUT: operations (List<Func<Task>>), isolationLevel (IsolationLevel)
OUTPUT: Task<TransactionResult>

BEGIN
    transactionResult = CREATE TransactionResult:
        Success = false
        OperationsCompleted = 0
        Errors = LIST<string>()
        TransactionId = Guid.NewGuid()
    
    TRY:
        // 1. Begin distributed transaction
        USING transactionScope = CREATE TransactionScope(
            TransactionScopeOption.Required,
            NEW TransactionOptions {
                IsolationLevel = isolationLevel,
                Timeout = TimeSpan.FromMinutes(5)
            },
            TransactionScopeAsyncFlowOption.Enabled):
            
            // 2. Execute all operations in sequence
            FOR index = 0; index < operations.Count; index++:
                operation = operations[index]
                
                TRY:
                    AWAIT operation()
                    transactionResult.OperationsCompleted++
                    
                    LOG DEBUG "Transaction operation completed" WITH 
                        transactionId: transactionResult.TransactionId,
                        operationIndex: index,
                        operationsCompleted: transactionResult.OperationsCompleted
                
                CATCH Exception operationEx:
                    errorMessage = $"Operation {index} failed: {operationEx.Message}"
                    transactionResult.Errors.Add(errorMessage)
                    
                    LOG ERROR "Transaction operation failed" WITH 
                        transactionId: transactionResult.TransactionId,
                        operationIndex: index,
                        exception: operationEx
                    
                    THROW TransactionOperationException(errorMessage, operationEx)
            
            // 3. Complete transaction
            transactionScope.Complete()
            transactionResult.Success = true
            
            LOG INFO "Distributed transaction completed successfully" WITH 
                transactionId: transactionResult.TransactionId,
                operationsCompleted: transactionResult.OperationsCompleted
        
        RETURN transactionResult
        
    CATCH Exception ex:
        transactionResult.Errors.Add($"Transaction failed: {ex.Message}")
        
        LOG ERROR "Distributed transaction failed" WITH 
            transactionId: transactionResult.TransactionId,
            operationsCompleted: transactionResult.OperationsCompleted,
            exception: ex
        
        RETURN transactionResult
END
```

### 5.2 Retry Policy Algorithm with Exponential Backoff

```pseudocode
ALGORITHM: RetryPolicy.ExecuteWithRetryAsync
INPUT: operation (Func<Task<T>>), maxRetries (int), baseDelay (TimeSpan)
OUTPUT: Task<T>

BEGIN
    lastException = null
    
    FOR attempt = 0; attempt <= maxRetries; attempt++:
        TRY:
            // 1. Execute operation
            result = AWAIT operation()
            
            IF attempt > 0:
                LOG INFO "Operation succeeded after retry" WITH 
                    attempt: attempt,
                    maxRetries: maxRetries
            
            RETURN result
            
        CATCH Exception ex WHEN IsRetryableException(ex):
            lastException = ex
            
            IF attempt == maxRetries:
                LOG ERROR "Operation failed after all retries" WITH 
                    attempt: attempt,
                    maxRetries: maxRetries,
                    exception: ex
                BREAK // Exit loop and throw
            
            // 2. Calculate delay with exponential backoff and jitter
            delay = baseDelay.TotalMilliseconds * Math.Pow(2, attempt)
            jitter = Random.NextDouble() * 0.1 * delay // 10% jitter
            totalDelay = TimeSpan.FromMilliseconds(delay + jitter)
            
            LOG WARNING "Operation failed, retrying" WITH 
                attempt: attempt,
                maxRetries: maxRetries,
                delay: totalDelay,
                exception: ex
            
            // 3. Wait before retry
            AWAIT Task.Delay(totalDelay)
        
        CATCH Exception ex WHEN NOT IsRetryableException(ex):
            // Non-retryable exception, fail immediately
            LOG ERROR "Operation failed with non-retryable exception" WITH 
                attempt: attempt,
                exception: ex
            THROW
    
    // If we get here, all retries were exhausted
    THROW RetryExhaustedException($"Operation failed after {maxRetries} retries", lastException)
END

ALGORITHM: IsRetryableException
INPUT: exception (Exception)
OUTPUT: bool (true if exception is retryable)

BEGIN
    SWITCH exception:
        CASE TimeoutException:
            RETURN true
        CASE HttpRequestException httpEx:
            RETURN httpEx.Message.Contains("timeout") OR 
                   httpEx.Message.Contains("connection") OR
                   httpEx.Message.Contains("503") OR
                   httpEx.Message.Contains("502")
        CASE SqlException sqlEx:
            // SQL Server transient errors
            transientErrorNumbers = [2, 53, 121, 232, 997, 1205, 1222]
            RETURN transientErrorNumbers.Contains(sqlEx.Number)
        CASE DbUpdateConcurrencyException:
            RETURN false // Should be handled specifically, not retried
        CASE ArgumentException:
            RETURN false // Invalid arguments shouldn't be retried
        DEFAULT:
            RETURN false // Conservative approach - don't retry unknown exceptions
END
```

---

## 📋 ALGORITHM 6: PERFORMANCE OPTIMIZATION & CACHING

### 6.1 Query Optimization Algorithm

```pseudocode
ALGORITHM: OptimizeReadModelQueries
INPUT: queryType (string), filters (Dictionary<string, object>), pagination (PaginationOptions)
OUTPUT: IQueryable<ConversationReadModel> (optimized query)

BEGIN
    baseQuery = conversationReadModelDbSet.AsQueryable()
    
    // 1. Apply global filters first (most selective)
    IF filters.ContainsKey("userId"):
        userId = filters["userId"] AS string
        baseQuery = baseQuery.Where(c => c.CreatedBy == userId)
    
    IF filters.ContainsKey("isActive"):
        isActive = filters["isActive"] AS bool
        baseQuery = baseQuery.Where(c => c.IsActive == isActive)
    
    // 2. Apply query-specific optimizations
    SWITCH queryType:
        CASE "RecentConversations":
            // Optimize for recent conversations query
            query = baseQuery
                .Where(c => c.LastMessageAt >= DateTime.UtcNow.AddDays(-30)) // Recent filter
                .OrderByDescending(c => c.LastMessageAt)
                .ThenByDescending(c => c.CreatedAt)
            
            // Use covering index hint if available
            IF DatabaseProvider == "SqlServer":
                query = query.TagWith("USE INDEX(IX_ConversationReadModel_Recent)")
            
        CASE "SearchConversations":
            searchText = filters["searchText"] AS string
            IF NOT string.IsNullOrEmpty(searchText):
                // PostgreSQL full-text search
                IF DatabaseProvider == "PostgreSQL":
                    query = baseQuery
                        .Where(c => c.SearchVector.Matches(EF.Functions.PlainToTsQuery(searchText)))
                        .OrderByDescending(c => c.SearchVector.Rank(EF.Functions.PlainToTsQuery(searchText)))
                ELSE:
                    // Fallback to LIKE search with optimization
                    query = baseQuery
                        .Where(c => c.Title.Contains(searchText) OR c.Summary.Contains(searchText))
                        .OrderByDescending(c => c.LastMessageAt)
            ELSE:
                query = baseQuery.OrderByDescending(c => c.LastMessageAt)
        
        CASE "ActiveConversations":
            query = baseQuery
                .Where(c => c.IsActive == true)
                .Where(c => c.LastMessageAt >= DateTime.UtcNow.AddHours(-24)) // Active in last 24h
                .OrderByDescending(c => c.LastMessageAt)
        
        CASE "CompletedConversations":
            query = baseQuery
                .Where(c => c.IsActive == false)
                .Where(c => c.CompletedAt != null)
                .OrderByDescending(c => c.CompletedAt)
        
        DEFAULT:
            query = baseQuery.OrderByDescending(c => c.CreatedAt)
    
    // 3. Apply pagination
    IF pagination.Skip > 0:
        query = query.Skip(pagination.Skip)
    
    IF pagination.Take > 0:
        query = query.Take(Math.Min(pagination.Take, 100)) // Max 100 per page
    
    // 4. Add query tracking optimization
    IF queryType.StartsWith("Search") OR queryType.Contains("Read"):
        query = query.AsNoTracking() // Read-only queries don't need tracking
    
    LOG DEBUG "Query optimized" WITH 
        queryType: queryType,
        filterCount: filters.Count,
        skip: pagination.Skip,
        take: pagination.Take
    
    RETURN query
END
```

### 6.2 Distributed Caching Algorithm

```pseudocode
ALGORITHM: DistributedCache.GetOrSetAsync
INPUT: key (string), factory (Func<Task<T>>), expiration (TimeSpan)
OUTPUT: Task<T> (cached or fresh value)

BEGIN
    cacheKey = $"axon:chat:{key}"
    
    TRY:
        // 1. Try to get from cache first
        cachedBytes = AWAIT distributedCache.GetAsync(cacheKey)
        
        IF cachedBytes IS NOT NULL:
            // Deserialize cached value
            cachedJson = Encoding.UTF8.GetString(cachedBytes)
            cachedValue = JsonSerializer.Deserialize<T>(cachedJson)
            
            LOG DEBUG "Cache hit" WITH 
                key: cacheKey,
                dataSize: cachedBytes.Length
            
            RETURN cachedValue
        
        // 2. Cache miss - execute factory function
        LOG DEBUG "Cache miss, executing factory" WITH key: cacheKey
        
        freshValue = AWAIT factory()
        
        // 3. Store in cache for future use
        AWAIT SetCacheValueAsync(cacheKey, freshValue, expiration)
        
        RETURN freshValue
        
    CATCH Exception ex:
        LOG ERROR "Cache operation failed, executing factory directly" WITH 
            key: cacheKey,
            exception: ex
        
        // Fall back to direct execution if cache fails
        RETURN AWAIT factory()
END

ALGORITHM: DistributedCache.SetCacheValueAsync
INPUT: cacheKey (string), value (T), expiration (TimeSpan)
OUTPUT: Task

BEGIN
    TRY:
        // 1. Serialize value
        jsonValue = JsonSerializer.Serialize(value, new JsonSerializerOptions {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        })
        
        valueBytes = Encoding.UTF8.GetBytes(jsonValue)
        
        // 2. Set cache options
        cacheOptions = CREATE DistributedCacheEntryOptions:
            AbsoluteExpirationRelativeToNow = expiration
            SlidingExpiration = expiration / 2 // Refresh cache on access
        
        // 3. Store in cache
        AWAIT distributedCache.SetAsync(cacheKey, valueBytes, cacheOptions)
        
        LOG DEBUG "Value cached" WITH 
            key: cacheKey,
            dataSize: valueBytes.Length,
            expiration: expiration
        
    CATCH Exception ex:
        LOG WARNING "Failed to cache value" WITH 
            key: cacheKey,
            exception: ex
        // Don't throw - caching failures shouldn't break the application
END

ALGORITHM: DistributedCache.InvalidatePatternAsync
INPUT: pattern (string)
OUTPUT: Task (invalidates all keys matching pattern)

BEGIN
    TRY:
        // This requires Redis or another cache that supports pattern-based invalidation
        IF distributedCache SUPPORTS pattern invalidation:
            // Redis example: delete all keys matching pattern
            searchPattern = $"axon:chat:{pattern}*"
            AWAIT distributedCache.RemoveByPatternAsync(searchPattern)
            
            LOG INFO "Cache pattern invalidated" WITH pattern: searchPattern
        ELSE:
            LOG WARNING "Cache does not support pattern invalidation" WITH pattern: pattern
        
    CATCH Exception ex:
        LOG ERROR "Failed to invalidate cache pattern" WITH 
            pattern: pattern,
            exception: ex
END

ALGORITHM: DistributedCache.GetConversationSummaryAsync
INPUT: conversationId (Guid)
OUTPUT: Task<ConversationSummary?> (cached conversation summary)

BEGIN
    cacheKey = $"conversation-summary:{conversationId}"
    
    summary = AWAIT GetOrSetAsync(cacheKey, async () => {
        // Factory function to create summary if not cached
        conversation = AWAIT conversationRepository.GetWithMessagesAsync(conversationId)
        
        IF conversation IS NULL:
            RETURN null
        
        RETURN CREATE ConversationSummary:
            Id = conversation.Id
            Title = conversation.Messages.FirstOrDefault()?.Content.Substring(0, 100)
            MessageCount = conversation.Messages.Count
            LastMessageAt = conversation.Messages.LastOrDefault()?.CreatedAt
            TotalTokensUsed = conversation.Messages.Sum(m => m.TokensUsed)
            Status = conversation.Status
            CreatedAt = conversation.CreatedAt
            CreatedBy = conversation.CreatedBy
    }, TimeSpan.FromMinutes(15)) // Cache for 15 minutes
    
    RETURN summary
END
```

---

## 🎯 PHASE 2 COMPLETION STATUS

### ✅ ALGORITHMS COMPLETED:
1. **Event Sourcing Core**: ✅ Append, Rehydrate, Snapshot algorithms
2. **Domain Event Publishing**: ✅ Publishing and projection handling algorithms  
3. **EF Shadow Properties Auditing**: ✅ SaveChangesInterceptor and configuration algorithms
4. **Repository Pattern**: ✅ Generic and specialized repository algorithms
5. **Unit of Work**: ✅ Transaction coordination and event management algorithms
6. **Transaction Coordination**: ✅ Distributed transactions and retry policies
7. **Performance Optimization**: ✅ Query optimization and distributed caching algorithms

### 📊 COMPLEXITY ANALYSIS:
- **Event Sourcing Operations**: O(1) append, O(n) rehydration where n = event count
- **Read Model Projections**: O(1) per event with eventual consistency
- **Repository Operations**: O(1) for single entity, O(log n) for queries with indexes
- **Caching Layer**: O(1) cache operations with distributed consistency
- **Transaction Coordination**: O(k) where k = number of operations in transaction

### 🎯 SUCCESS CRITERIA MET:
- ✅ Complete event sourcing algorithms for ALL conversation changes
- ✅ Domain event publishing with projection handling
- ✅ EF Shadow Properties auditing with interceptors
- ✅ Repository pattern with Unit of Work coordination
- ✅ CQRS read model projection algorithms
- ✅ Performance optimization strategies
- ✅ Transaction coordination algorithms

---

## 🤝 HUMAN CHECKPOINT: PSEUDOCODE VALIDATION REQUIRED

**COMPREHENSIVE PSEUDOCODE DOCUMENT READY FOR HUMAN REVIEW**

### 📋 PHASE 2 DELIVERABLES SUMMARY:
- **Event Sourcing Algorithms**: Complete append/rehydrate/snapshot logic
- **Domain Event System**: Publishing and projection handling patterns
- **Auditing Framework**: EF Shadow Properties with SaveChangesInterceptor
- **Repository Layer**: Generic + Specialized with Unit of Work coordination
- **CQRS Projections**: Read model optimization and query patterns
- **Performance Layer**: Caching, query optimization, and transaction management
- **Error Handling**: Retry policies and distributed transaction coordination

### 🔍 HUMAN VALIDATION REQUIRED:
Please review all pseudocode algorithms and confirm:

1. **✅ APPROVE**: All algorithms are comprehensive and ready for Phase 3 Architecture
2. **🔄 REFINE**: Specify which algorithms need improvement or clarification
3. **❌ REVISION**: Provide detailed feedback for algorithmic redesign

**Your approval ensures Phase 3 Architecture will implement exactly the right solution patterns.**

Ready to proceed to **SPARC Phase 3: Architecture Design** upon your approval!