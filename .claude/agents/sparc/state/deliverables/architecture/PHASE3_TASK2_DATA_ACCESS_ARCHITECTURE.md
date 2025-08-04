# 🗄️ AXON BACKEND - PHASE 3 TASK 2: DATA ACCESS & REPOSITORY ARCHITECTURE

## 📋 EXECUTIVE SUMMARY AND ARCHITECTURE OVERVIEW

This document specifies the comprehensive Data Access and Repository Architecture for the Axon Backend conversation persistence system, implementing Clean Architecture principles with Entity Framework Core, PostgreSQL optimizations, and advanced concurrency patterns. The architecture follows SPARC v3 specifications with backing fields pattern for audit tracking and PostgreSQL system column optimizations.

### 🎯 ARCHITECTURAL OBJECTIVES

- **Clean Repository Abstraction**: Generic repository pattern with compiled query optimization
- **PostgreSQL Native Integration**: System xmin concurrency and tsvector full-text search
- **Backing Fields Pattern**: Audit interceptor with direct EF Core backing field mapping
- **Performance Optimization**: Connection pooling, compiled queries, and query splitting
- **Transaction Management**: Unit of Work facade with distributed transaction support
- **Concurrency Control**: PostgreSQL xmin system column for optimistic concurrency

### 🏛️ ARCHITECTURAL PRINCIPLES

- **Dependency Inversion**: Repository interfaces defined in Application layer
- **Single Responsibility**: Specialized repositories for domain-specific operations  
- **Performance First**: Compiled queries and connection optimization
- **Infrastructure Isolation**: EF Core concerns contained in Infrastructure layer
- **Domain Purity**: No EF Core dependencies in Domain layer

## 🏗️ CORE COMPONENTS ARCHITECTURE

### 1. ChatDbContext Design Specification

```csharp
/// <summary>
/// EF Core DbContext for Chat module with PostgreSQL optimizations and backing fields pattern
/// Implements Shadow Properties for audit tracking without domain pollution
/// </summary>
public sealed class ChatDbContext : DbContext
{
    private readonly AuditInterceptor _auditInterceptor;
    private readonly DomainEventInterceptor _domainEventInterceptor;

    public ChatDbContext(
        DbContextOptions<ChatDbContext> options, 
        AuditInterceptor auditInterceptor,
        DomainEventInterceptor domainEventInterceptor) : base(options)
    {
        _auditInterceptor = auditInterceptor;
        _domainEventInterceptor = domainEventInterceptor;
    }

    // Entity sets with typed IDs
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Register interceptors for backing fields pattern
        optionsBuilder.AddInterceptors(_auditInterceptor, _domainEventInterceptor);
        
        // PostgreSQL connection optimization
        optionsBuilder.UseNpgsql(options =>
        {
            options.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
            options.CommandTimeout(30);
            options.UseNodaTime(); // For enhanced timestamp handling
        });

        // Performance optimizations
        optionsBuilder.EnableSensitiveDataLogging(false);
        optionsBuilder.EnableServiceProviderCaching();
        optionsBuilder.EnableThreadSafetyChecks(false); // For production performance
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Apply PostgreSQL snake_case naming convention
        modelBuilder.UseSnakeCaseNamingConvention();
        
        // Apply all entity configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChatDbContext).Assembly);
        
        // Configure audit backing fields for all IAuditable entities
        ConfigureAuditBackingFields(modelBuilder);
        
        // PostgreSQL specific optimizations
        ConfigurePostgreSqlOptimizations(modelBuilder);
    }

    /// <summary>
    /// A4: Configures backing fields pattern for audit tracking (consistent approach)
    /// Maps directly to backing fields (_createdAtUtc, _updatedAtUtc, etc.) for performance
    /// Uses .HasField() mapping consistently instead of mixing with shadow properties
    /// </summary>
    private static void ConfigureAuditBackingFields(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IAuditable).IsAssignableFrom(entityType.ClrType))
            {
                // Map to backing fields for direct EF Core access (performance optimization)
                // A4: Consistent backing fields approach with .HasField() mapping
                modelBuilder.Entity(entityType.ClrType)
                    .Property("_createdAtUtc")
                    .HasField("_createdAtUtc")
                    .HasColumnName("created_at_utc")
                    // A5: PostgreSQL timestamptz with now() default for timezone consistency
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("now()")
                    .ValueGeneratedOnAdd()
                    .HasComment("UTC timestamp when entity was created");
                
                modelBuilder.Entity(entityType.ClrType)
                    .Property("_updatedAtUtc")
                    .HasField("_updatedAtUtc")
                    .HasColumnName("updated_at_utc")
                    .HasColumnType("timestamp with time zone")
                    .HasComment("UTC timestamp when entity was last updated");
                
                modelBuilder.Entity(entityType.ClrType)
                    .Property("_createdBy")
                    .HasField("_createdBy")
                    .HasColumnName("created_by")
                    .HasMaxLength(100)
                    .HasComment("Identifier of who created this entity");
                
                modelBuilder.Entity(entityType.ClrType)
                    .Property("_updatedBy")
                    .HasField("_updatedBy")
                    .HasColumnName("updated_by")
                    .HasMaxLength(100)
                    .HasComment("Identifier of who last updated this entity");
                
                // A6: PostgreSQL xmin optimistic concurrency (official Npgsql pattern)
                modelBuilder.Entity(entityType.ClrType)
                    .Property<uint>("xmin")
                    .IsRowVersion()
                    .HasColumnName("xmin")
                    .HasColumnType("xid")
                    .HasComment("PostgreSQL system column for optimistic concurrency");
                
                // Performance indexes for audit queries
                modelBuilder.Entity(entityType.ClrType)
                    .HasIndex("_createdAtUtc")
                    .HasDatabaseName($"ix_{entityType.GetTableName()}_created_at_utc");
                
                modelBuilder.Entity(entityType.ClrType)
                    .HasIndex("_updatedAtUtc")
                    .HasDatabaseName($"ix_{entityType.GetTableName()}_updated_at_utc");
            }
        }
    }

    /// <summary>
    /// Configures PostgreSQL specific optimizations including generated tsvector columns
    /// and GIN indexes for full-text search performance
    /// </summary>
    private static void ConfigurePostgreSqlOptimizations(ModelBuilder modelBuilder)
    {
        // Enable required PostgreSQL extensions
        modelBuilder.HasPostgresExtension("pg_trgm"); // Trigram matching for fuzzy search
        modelBuilder.HasPostgresExtension("unaccent"); // Accent-insensitive search

        // Configure generated tsvector column for Message full-text search
        modelBuilder.Entity<Message>()
            .Property<string>("search_vector")
            .HasColumnType("tsvector")
            .HasComputedColumnSql("to_tsvector('english', content)", stored: true)
            .HasComment("Generated tsvector for full-text search on message content");

        // GIN index for full-text search performance
        modelBuilder.Entity<Message>()
            .HasIndex("search_vector")
            .HasMethod("gin")
            .HasDatabaseName("ix_messages_search_vector");

        // Additional composite indexes for conversation queries
        modelBuilder.Entity<Message>()
            .HasIndex(nameof(Message.ConversationId), "_createdAtUtc")
            .HasDatabaseName("ix_messages_conversation_created");

        // A3: Per-query splitting instead of global SplitQuery setting
        // Use .AsSplitQuery() only on specific heavy includes, not globally
        modelBuilder.Entity<Conversation>()
            .Navigation(c => c.MessagesOrdered)
            .EnableLazyLoading(false); // Explicit loading only
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Interceptors will handle audit field updates and domain event publishing
        return await base.SaveChangesAsync(cancellationToken);
    }
}
```

### 2. Entity Framework Core Configuration Design

```csharp
/// <summary>
/// EF Core configuration for Conversation aggregate with PostgreSQL optimizations
/// </summary>
public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations");
        
        // Primary key with strong typed ID conversion
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasConversion(
                id => id.Value,
                value => ConversationId.From(value))
            .ValueGeneratedNever()
            .HasComment("Unique identifier for the conversation");

        // Value object properties
        builder.Property(c => c.Title)
            .HasMaxLength(200)
            .IsRequired()
            .HasComment("Human-readable title for the conversation");

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasComment("Current status of the conversation");

        builder.Property(c => c.CompletedAt)
            .HasColumnType("timestamp with time zone")
            .HasComment("UTC timestamp when conversation was completed");

        // Navigation properties with explicit loading
        builder.HasMany<Message>()
            .WithOne()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_messages_conversation_id");

        // Performance indexes
        builder.HasIndex(c => c.Status)
            .HasFilter("status = 'Active'") // Partial index for active conversations
            .HasDatabaseName("ix_conversations_active_status");

        builder.HasIndex(c => c.CompletedAt)
            .HasDatabaseName("ix_conversations_completed_at");

        // Check constraints for data integrity
        builder.HasCheckConstraint("ck_conversations_title_length", 
            "LENGTH(title) > 0 AND LENGTH(title) <= 200");
    }
}

/// <summary>
/// EF Core configuration for Message entity with full-text search optimization
/// </summary>
public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");
        
        // Primary key with strong typed ID conversion
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .HasConversion(
                id => id.Value,
                value => MessageId.From(value))
            .ValueGeneratedNever()
            .HasComment("Unique identifier for the message");

        // Foreign key to conversation
        builder.Property(m => m.ConversationId)
            .HasConversion(
                id => id.Value,
                value => ConversationId.From(value))
            .HasComment("Foreign key to the conversation this message belongs to");

        // Message content with full-text search optimization
        builder.Property(m => m.Content)
            .HasMaxLength(100_000) // 100KB limit per SPARC specification
            .IsRequired()
            .HasComment("The content of the message");

        // Value objects
        builder.Property(m => m.Role)
            .HasConversion(
                role => role.Value,
                value => MessageRole.From(value))
            .HasMaxLength(20)
            .HasComment("The role of the message sender");

        builder.Property(m => m.Sequence)
            .HasComment("Sequence number for message ordering within conversation");

        // A11: JSONB mapping with Dictionary<string, JsonElement> for type preservation
        builder.Property(m => m.Metadata)
            .HasConversion(
                dict => dict == null ? null : JsonSerializer.Serialize(dict, (JsonSerializerOptions?)null),
                json => json == null ? null : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, (JsonSerializerOptions?)null))
            .HasColumnType("jsonb")
            .HasComment("Optional metadata associated with the message");

        // Performance indexes
        builder.HasIndex(m => m.ConversationId)
            .HasDatabaseName("ix_messages_conversation_id");

        builder.HasIndex(m => m.Role)
            .HasDatabaseName("ix_messages_role");

        builder.HasIndex(m => new { m.ConversationId, m.Sequence })
            .IsUnique()
            .HasDatabaseName("ix_messages_conversation_sequence");

        // Check constraints
        builder.HasCheckConstraint("ck_messages_sequence_positive", "sequence > 0");
        builder.HasCheckConstraint("ck_messages_content_length", "LENGTH(content) > 0");
    }
}
```

## 🔍 AUDIT INTERCEPTOR IMPLEMENTATION STRATEGY

### AuditSaveChangesInterceptor with Backing Fields Pattern

```csharp
/// <summary>
/// EF Core interceptor for automatic audit field population using backing fields pattern
/// Maps directly to private backing fields in AuditableEntity for optimal performance
/// </summary>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public AuditSaveChangesInterceptor(
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider)
    {
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Updates audit fields using direct backing field mapping for maximum performance
    /// This approach eliminates property accessor overhead and maintains encapsulation
    /// </summary>
    private void UpdateAuditFields(DbContext? context)
    {
        if (context == null) return;

        var currentUserId = _currentUserService.GetCurrentUserId() ?? "system";
        var utcNow = _dateTimeProvider.UtcNow;

        var auditableEntries = context.ChangeTracker
            .Entries<IAuditable>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified)
            .ToList();

        foreach (var entry in auditableEntries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    // Set creation audit fields via backing field mapping
                    entry.Property("_createdAtUtc").CurrentValue = utcNow;
                    entry.Property("_createdBy").CurrentValue = currentUserId;
                    break;

                case EntityState.Modified:
                    // Set update audit fields via backing field mapping
                    entry.Property("_updatedAtUtc").CurrentValue = utcNow;
                    entry.Property("_updatedBy").CurrentValue = currentUserId;
                    
                    // Prevent modification of creation audit fields
                    entry.Property("_createdAtUtc").IsModified = false;
                    entry.Property("_createdBy").IsModified = false;
                    break;
            }
        }
    }
}

/// <summary>
/// Domain event interceptor for automatic event publishing during save operations
/// Ensures domain events are published within the same transaction as entity changes
/// </summary>
public sealed class DomainEventInterceptor : SaveChangesInterceptor
{
    private readonly IDomainEventPublisher _domainEventPublisher;
    private readonly ILogger<DomainEventInterceptor> _logger;

    public DomainEventInterceptor(
        IDomainEventPublisher domainEventPublisher,
        ILogger<DomainEventInterceptor> logger)
    {
        _domainEventPublisher = domainEventPublisher;
        _logger = logger;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await PublishDomainEventsAsync(eventData.Context, cancellationToken);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Publishes domain events from aggregate roots within the transaction boundary
    /// </summary>
    private async Task PublishDomainEventsAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context == null) return;

        var aggregateRoots = context.ChangeTracker
            .Entries<AggregateRoot<ConversationId>>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        var domainEventsToPublish = new List<IDomainEvent>();

        foreach (var aggregateRoot in aggregateRoots)
        {
            domainEventsToPublish.AddRange(aggregateRoot.DomainEvents);
            aggregateRoot.ClearDomainEvents();
        }

        foreach (var domainEvent in domainEventsToPublish)
        {
            _logger.LogDebug("Publishing domain event: {EventType} for aggregate: {AggregateId}",
                domainEvent.GetType().Name, domainEvent.AggregateId);
                
            await _domainEventPublisher.PublishAsync(domainEvent, cancellationToken);
        }
    }
}
```

## 🗃️ REPOSITORY PATTERN IMPLEMENTATION

### Generic Repository Base Class with Compiled Query Integration

```csharp
/// <summary>
/// Generic repository base class with EF Core compiled queries for optimal performance
/// Encapsulates IQueryable to maintain Clean Architecture boundaries
/// </summary>
/// <typeparam name="TEntity">The entity type implementing IIdentifiable</typeparam>
/// <typeparam name="TId">The strongly-typed identifier</typeparam>
public abstract class Repository<TEntity, TId> : IRepository<TEntity, TId>
    where TEntity : class, IIdentifiable<TId>
    where TId : notnull
{
    protected readonly ChatDbContext Context;
    protected readonly DbSet<TEntity> DbSet;

    protected Repository(ChatDbContext context)
    {
        Context = context;
        DbSet = context.Set<TEntity>();
    }

    // A8: EF8 compiled queries pattern - compile to IQueryable/IAsyncEnumerable, materialize outside
        private static readonly Func<ChatDbContext, TId, IQueryable<TEntity>> GetByIdCompiledQuery =
            EF.CompileQuery((ChatDbContext context, TId id) =>
                context.Set<TEntity>().Where(e => e.Id.Equals(id)));

    private static readonly Func<ChatDbContext, TId, Task<bool>> ExistsCompiledQuery =
        EF.CompileAsyncQuery((ChatDbContext context, TId id) =>
            context.Set<TEntity>().Any(e => e.Id.Equals(id)));

    private static readonly Func<ChatDbContext, Task<int>> CountCompiledQuery =
        EF.CompileAsyncQuery((ChatDbContext context) =>
            context.Set<TEntity>().Count());

    public virtual async Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        // A8: Materialize outside the compiled query delegate
        return await GetByIdCompiledQuery(Context, id).FirstOrDefaultAsync(cancellationToken);
    }

    public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        var entry = await DbSet.AddAsync(entity, cancellationToken);
        return entry.Entity;
    }

    public virtual Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        DbSet.Update(entity);
        return Task.CompletedTask;
    }

    public virtual async Task<bool> DeleteAsync(TId id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        if (entity == null)
            return false;

        DbSet.Remove(entity);
        return true;
    }

    public virtual async Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default)
    {
        return await ExistsCompiledQuery(Context, id);
    }

    public virtual async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return await CountCompiledQuery(Context);
    }

    /// <summary>
    /// Creates paginated results with efficient counting strategy
    /// </summary>
    protected static async Task<PagedResult<T>> CreatePagedResultAsync<T>(
        IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, totalCount, page, pageSize);
    }

    /// <summary>
    /// Executes queries with proper connection management
    /// </summary>
    /// <summary>
    /// A9: Repository boundaries - keep IQueryable internal, use AsNoTracking for reads
    /// </summary>
    protected async Task<TResult> ExecuteQueryAsync<TResult>(
        Func<IQueryable<TEntity>, IQueryable<TResult>> queryBuilder,
        CancellationToken cancellationToken = default)
    {
        // A9: AsNoTracking for reads, track only when updating
        var query = queryBuilder(DbSet.AsNoTracking());
        return await query.FirstOrDefaultAsync(cancellationToken);
    }
}
```

### Narrow Repository Inheritance Pattern with Domain-Specific Methods

```csharp
/// <summary>
/// Conversation repository interface with domain-specific query methods
/// </summary>
public interface IConversationRepository : IRepository<Conversation, ConversationId>
{
    Task<IReadOnlyList<Conversation>> GetActiveConversationsAsync(
        int limit = 50, 
        CancellationToken cancellationToken = default);
        
    Task<PagedResult<Conversation>> GetByUserAsync(
        string userId, 
        int page, 
        int pageSize, 
        CancellationToken cancellationToken = default);
        
    Task<Conversation?> GetWithMessagesAsync(
        ConversationId id, 
        CancellationToken cancellationToken = default);
        
    Task<IReadOnlyList<Conversation>> SearchByContentAsync(
        string searchTerm, 
        int limit = 20, 
        CancellationToken cancellationToken = default);
        
    Task<ConversationStatistics> GetStatisticsAsync(
        DateTime? fromDate = null, 
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Specialized conversation repository with compiled queries for domain operations
/// </summary>
public sealed class ConversationRepository : Repository<Conversation, ConversationId>, IConversationRepository
{
    public ConversationRepository(ChatDbContext context) : base(context) { }

    // Domain-specific compiled queries
    private static readonly Func<ChatDbContext, int, Task<List<Conversation>>> GetActiveConversationsCompiledQuery =
        EF.CompileAsyncQuery((ChatDbContext context, int limit) =>
            context.Conversations
                .Where(c => c.Status == ConversationStatus.Active)
                .OrderByDescending(c => EF.Property<DateTime>(c, "_updatedAtUtc"))
                .Take(limit)
                .ToList());

    private static readonly Func<ChatDbContext, ConversationId, Task<Conversation?>> GetWithMessagesCompiledQuery =
        EF.CompileAsyncQuery((ChatDbContext context, ConversationId id) =>
            context.Conversations
                .Include(c => c.MessagesOrdered)
                .FirstOrDefault(c => c.Id == id));

    public async Task<IReadOnlyList<Conversation>> GetActiveConversationsAsync(
        int limit = 50, 
        CancellationToken cancellationToken = default)
    {
        var conversations = await GetActiveConversationsCompiledQuery(Context, limit);
        return conversations.AsReadOnly();
    }

    public async Task<PagedResult<Conversation>> GetByUserAsync(
        string userId, 
        int page, 
        int pageSize, 
        CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .Where(c => EF.Property<string>(c, "_createdBy") == userId)
            .OrderByDescending(c => EF.Property<DateTime>(c, "_createdAtUtc"));

        return await CreatePagedResultAsync(query, page, pageSize, cancellationToken);
    }

    public async Task<Conversation?> GetWithMessagesAsync(
        ConversationId id, 
        CancellationToken cancellationToken = default)
    {
        return await GetWithMessagesCompiledQuery(Context, id);
    }

    public async Task<IReadOnlyList<Conversation>> SearchByContentAsync(
        string searchTerm, 
        int limit = 20, 
        CancellationToken cancellationToken = default)
    {
        // PostgreSQL full-text search using generated tsvector
        var conversations = await Context.Conversations
            .Where(c => Context.Messages
                .Where(m => m.ConversationId == c.Id)
                .Any(m => EF.Functions.ToTsVector("english", m.Content)
                    .Matches(EF.Functions.PlainToTsQuery("english", searchTerm))))
            .Take(limit)
            .ToListAsync(cancellationToken);

        return conversations.AsReadOnly();
    }

    public async Task<ConversationStatistics> GetStatisticsAsync(
        DateTime? fromDate = null, 
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsQueryable();
        
        if (fromDate.HasValue)
        {
            query = query.Where(c => EF.Property<DateTime>(c, "_createdAtUtc") >= fromDate.Value);
        }

        var statistics = await query
            .GroupBy(c => 1)
            .Select(g => new ConversationStatistics
            {
                TotalConversations = g.Count(),
                ActiveConversations = g.Count(c => c.Status == ConversationStatus.Active),
                CompletedConversations = g.Count(c => c.Status == ConversationStatus.Completed),
                AverageMessagesPerConversation = g.Average(c => c.MessageCount),
                FromDate = fromDate ?? DateTime.MinValue,
                GeneratedAt = DateTime.UtcNow
            })
            .FirstOrDefaultAsync(cancellationToken);

        return statistics ?? new ConversationStatistics
        {
            FromDate = fromDate ?? DateTime.MinValue,
            GeneratedAt = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Message repository interface with specialized query methods
/// </summary>
public interface IMessageRepository : IRepository<Message, MessageId>
{
    Task<IReadOnlyList<Message>> GetByConversationAsync(
        ConversationId conversationId, 
        CancellationToken cancellationToken = default);
        
    Task<PagedResult<Message>> GetPagedByConversationAsync(
        ConversationId conversationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
        
    Task<IReadOnlyList<Message>> SearchFullTextAsync(
        string searchTerm,
        int limit = 50,
        CancellationToken cancellationToken = default);
        
    Task<Message?> GetLatestByConversationAsync(
        ConversationId conversationId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Specialized message repository with full-text search capabilities
/// </summary>
public sealed class MessageRepository : Repository<Message, MessageId>, IMessageRepository
{
    public MessageRepository(ChatDbContext context) : base(context) { }

    // Compiled queries for message operations
    private static readonly Func<ChatDbContext, ConversationId, Task<List<Message>>> GetByConversationCompiledQuery =
        EF.CompileAsyncQuery((ChatDbContext context, ConversationId conversationId) =>
            context.Messages
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.Sequence)
                .ToList());

    private static readonly Func<ChatDbContext, ConversationId, Task<Message?>> GetLatestByConversationCompiledQuery =
        EF.CompileAsyncQuery((ChatDbContext context, ConversationId conversationId) =>
            context.Messages
                .Where(m => m.ConversationId == conversationId)
                .OrderByDescending(m => m.Sequence)
                .FirstOrDefault());

    public async Task<IReadOnlyList<Message>> GetByConversationAsync(
        ConversationId conversationId, 
        CancellationToken cancellationToken = default)
    {
        var messages = await GetByConversationCompiledQuery(Context, conversationId);
        return messages.AsReadOnly();
    }

    public async Task<PagedResult<Message>> GetPagedByConversationAsync(
        ConversationId conversationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.Sequence);

        return await CreatePagedResultAsync(query, page, pageSize, cancellationToken);
    }

    public async Task<IReadOnlyList<Message>> SearchFullTextAsync(
        string searchTerm,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        // PostgreSQL full-text search using generated tsvector column
        var messages = await DbSet
            .Where(m => EF.Property<string>(m, "search_vector") != null &&
                       EF.Functions.ToTsVector("english", m.Content)
                           .Matches(EF.Functions.PlainToTsQuery("english", searchTerm)))
            .OrderByDescending(m => EF.Property<DateTime>(m, "_createdAtUtc"))
            .Take(limit)
            .ToListAsync(cancellationToken);

        return messages.AsReadOnly();
    }

    public async Task<Message?> GetLatestByConversationAsync(
        ConversationId conversationId,
        CancellationToken cancellationToken = default)
    {
        return await GetLatestByConversationCompiledQuery(Context, conversationId);
    }
}
```

## 🔄 UNIT OF WORK FACADE DESIGN

### EfUnitOfWork with Transaction Orchestration

```csharp
/// <summary>
/// Unit of Work interface for transaction boundary management
/// </summary>
public interface IUnitOfWork : IDisposable
{
    // Repository access
    IConversationRepository Conversations { get; }
    IMessageRepository Messages { get; }

    // Transaction management
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    
    // Distributed transaction support
    Task<IDbContextTransaction> BeginDistributedTransactionAsync(CancellationToken cancellationToken = default);
    
    // Bulk operations
    Task<int> BulkInsertAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class;
    Task<int> BulkUpdateAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class;
    
    // Connection management
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
    Task EnsureCreatedAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// EF Core implementation of Unit of Work with comprehensive transaction management
/// </summary>
public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly ChatDbContext _context;
    private readonly ILogger<EfUnitOfWork> _logger;
    private IDbContextTransaction? _currentTransaction;
    private bool _disposed;

    public EfUnitOfWork(
        ChatDbContext context,
        IConversationRepository conversations,
        IMessageRepository messages,
        ILogger<EfUnitOfWork> logger)
    {
        _context = context;
        _logger = logger;
        Conversations = conversations;
        Messages = messages;
    }

    public IConversationRepository Conversations { get; }
    public IMessageRepository Messages { get; }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _context.SaveChangesAsync(cancellationToken);
            
            _logger.LogDebug("Saved {ChangeCount} changes to database", result);
            return result;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency conflict occurred during save operation");
            throw new ConversationConcurrencyException("A concurrency conflict occurred. The operation was not completed.", ex);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database update exception occurred during save operation");
            throw new ConversationPersistenceException("An error occurred while saving changes to the database.", ex);
        }
    }

    public async Task BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, 
        CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            throw new InvalidOperationException("A transaction is already in progress.");
        }

        _currentTransaction = await _context.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
        _logger.LogDebug("Started database transaction with isolation level: {IsolationLevel}", isolationLevel);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            throw new InvalidOperationException("No transaction is currently in progress.");
        }

        try
        {
            await _currentTransaction.CommitAsync(cancellationToken);
            _logger.LogDebug("Committed database transaction");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to commit database transaction");
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            _logger.LogWarning("Attempted to rollback transaction, but no transaction is in progress");
            return;
        }

        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
            _logger.LogDebug("Rolled back database transaction");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rollback database transaction");
            throw;
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public async Task<IDbContextTransaction> BeginDistributedTransactionAsync(CancellationToken cancellationToken = default)
    {
        // For distributed transactions across multiple bounded contexts
        var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        _logger.LogDebug("Started distributed transaction: {TransactionId}", transaction.TransactionId);
        return transaction;
    }

    public async Task<int> BulkInsertAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class
    {
        var entityList = entities.ToList();
        await _context.Set<T>().AddRangeAsync(entityList, cancellationToken);
        var result = await SaveChangesAsync(cancellationToken);
        
        _logger.LogDebug("Bulk inserted {Count} entities of type {EntityType}", entityList.Count, typeof(T).Name);
        return result;
    }

    public async Task<int> BulkUpdateAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class
    {
        var entityList = entities.ToList();
        _context.Set<T>().UpdateRange(entityList);
        var result = await SaveChangesAsync(cancellationToken);
        
        _logger.LogDebug("Bulk updated {Count} entities of type {EntityType}", entityList.Count, typeof(T).Name);
        return result;
    }

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to database");
            return false;
        }
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        var created = await _context.Database.EnsureCreatedAsync(cancellationToken);
        if (created)
        {
            _logger.LogInformation("Database was created");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _currentTransaction?.Dispose();
        _context.Dispose();
        _disposed = true;
        
        _logger.LogDebug("Unit of Work disposed");
    }
}
```

## 🔧 CONNECTION MANAGEMENT AND PERFORMANCE OPTIMIZATION

### Connection Pooling and Configuration Strategy

```csharp
/// <summary>
/// PostgreSQL connection configuration with advanced pooling and performance optimizations
/// </summary>
public static class PostgreSqlConnectionConfiguration
{
    public static IServiceCollection AddPostgreSqlConnection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection string is required");

        // A1: Use DbContextPool for better connection management and performance
        services.AddDbContextPool<ChatDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                // Connection resilience (A12: Configurable resilience)
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);

                // Performance optimizations (A12: Configurable timeouts)
                npgsqlOptions.CommandTimeout(30);
                
                // A5: Enhanced timestamp handling with NodaTime
                npgsqlOptions.UseNodaTime();
                
                // A10: PostgreSQL version targeting for optimal index support
                npgsqlOptions.SetPostgresVersion(new Version(15, 0));
            });

            // A2: Remove invalid/fragile toggles - EnableServiceProviderCaching() doesn't exist in EF8
            // A2: Avoid EnableThreadSafetyChecks(false) unless profiled benefits are proven
            options.EnableSensitiveDataLogging(false); // Security best practice
            options.ConfigureWarnings(warnings =>
            {
                warnings.Ignore(CoreEventId.NavigationIncludeIgnoredWarning);
                warnings.Ignore(RelationalEventId.MultipleCollectionIncludeWarning);
            });
        });

            // A2: Removed invalid/fragile toggles - these are already configured above
        });

        // A1: Connection pool configuration via connection string
        // Configure Npgsql pool via connection string parameters:
        // Max Pool Size=100;Min Pool Size=5;Connection Idle Lifetime=900;Connection Pruning Interval=600
        services.Configure<NpgsqlConnectionPoolingOptions>(options =>
        {
            options.MaxPoolSize = 100;
            options.MinPoolSize = 5;
            options.ConnectionIdleLifetime = TimeSpan.FromMinutes(15);
            options.ConnectionPruningInterval = TimeSpan.FromMinutes(10);
        });

        return services;
    }
}

/// <summary>
/// Query optimization extensions for repository implementations
/// </summary>
public static class QueryOptimizationExtensions
{
    /// <summary>
        /// A3: Per-query performance optimizations - use AsSplitQuery only when needed
        /// </summary>
        public static IQueryable<T> WithPerformanceOptimizations<T>(this IQueryable<T> query, bool splitQuery = false) where T : class
        {
            var optimized = query.AsNoTracking(); // Disable change tracking for read-only scenarios
            
            // A3: Use AsSplitQuery only on specific heavy includes, not globally
            return splitQuery ? optimized.AsSplitQuery() : optimized;
        }

    /// <summary>
    /// Applies tracking optimizations for update scenarios
    /// </summary>
    public static IQueryable<T> WithTrackingOptimizations<T>(this IQueryable<T> query) where T : class
    {
        return query.AsNoTrackingWithIdentityResolution(); // Optimized tracking
    }

    /// <summary>
    /// Configures query timeout for long-running operations
    /// </summary>
    public static IQueryable<T> WithTimeout<T>(this IQueryable<T> query, TimeSpan timeout) where T : class
    {
        return query.TagWith($"Timeout: {timeout.TotalSeconds}s");
    }
}
```

## 🔐 CONCURRENCY AND DATA CONSISTENCY

### PostgreSQL xmin Optimistic Concurrency Implementation

```csharp
/// <summary>
/// PostgreSQL xmin-based concurrency conflict resolution strategy
/// </summary>
public sealed class PostgreSqlConcurrencyStrategy
{
    private readonly ILogger<PostgreSqlConcurrencyStrategy> _logger;

    public PostgreSqlConcurrencyStrategy(ILogger<PostgreSqlConcurrencyStrategy> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Handles concurrency conflicts using PostgreSQL xmin system column
    /// Provides automatic retry with exponential backoff
    /// </summary>
    public async Task<TResult> ExecuteWithConcurrencyRetryAsync<TResult>(
        Func<Task<TResult>> operation,
        int maxRetries = 3,
        CancellationToken cancellationToken = default)
    {
        var attempt = 0;
        var baseDelay = TimeSpan.FromMilliseconds(100);

        while (attempt < maxRetries)
        {
            try
            {
                return await operation();
            }
            catch (DbUpdateConcurrencyException ex) when (attempt < maxRetries - 1)
            {
                attempt++;
                var delay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
                
                _logger.LogWarning(ex, 
                    "Concurrency conflict occurred on attempt {Attempt}/{MaxRetries}. Retrying in {Delay}ms",
                    attempt, maxRetries, delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
                
                // Refresh entity states for retry
                foreach (var entry in ex.Entries)
                {
                    await entry.ReloadAsync(cancellationToken);
                }
            }
        }

        // Final attempt without catch
        return await operation();
    }

    /// <summary>
    /// Resolves concurrency conflicts by refreshing entity from database
    /// </summary>
    public async Task<bool> ResolveConcurrencyConflictAsync(
        DbContext context,
        EntityEntry conflictedEntry,
        ConcurrencyResolutionStrategy strategy = ConcurrencyResolutionStrategy.DatabaseWins,
        CancellationToken cancellationToken = default)
    {
        try
        {
            switch (strategy)
            {
                case ConcurrencyResolutionStrategy.DatabaseWins:
                    await conflictedEntry.ReloadAsync(cancellationToken);
                    return true;

                case ConcurrencyResolutionStrategy.ClientWins:
                    conflictedEntry.OriginalValues.SetValues(conflictedEntry.CurrentValues);
                    return true;

                case ConcurrencyResolutionStrategy.Merge:
                    return await MergeConcurrencyConflictAsync(conflictedEntry, cancellationToken);

                default:
                    throw new ArgumentOutOfRangeException(nameof(strategy), strategy, null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve concurrency conflict for entity {EntityType}",
                conflictedEntry.Entity.GetType().Name);
            return false;
        }
    }

    private async Task<bool> MergeConcurrencyConflictAsync(
        EntityEntry conflictedEntry,
        CancellationToken cancellationToken)
    {
        // Store current values
        var currentValues = conflictedEntry.CurrentValues.Clone();
        
        // Reload from database
        await conflictedEntry.ReloadAsync(cancellationToken);
        
        // Apply merge logic (domain-specific)
        // For audit fields, always use database values
        // For business data, use custom merge rules
        
        var mergedSuccessfully = ApplyMergeRules(conflictedEntry, currentValues);
        
        _logger.LogDebug("Concurrency conflict merge {Result} for entity {EntityType}",
            mergedSuccessfully ? "succeeded" : "failed",
            conflictedEntry.Entity.GetType().Name);
            
        return mergedSuccessfully;
    }

    private bool ApplyMergeRules(EntityEntry entry, PropertyValues currentValues)
    {
        // Domain-specific merge rules
        // This would be customized based on business requirements
        
        foreach (var property in entry.Properties)
        {
            var currentValue = currentValues[property.Metadata.Name];
            var databaseValue = entry.Property(property.Metadata.Name).CurrentValue;
            
            // Skip audit fields - always use database values
            if (IsAuditField(property.Metadata.Name))
                continue;
                
            // Apply business-specific merge logic
            var mergedValue = ApplyPropertyMergeRule(property.Metadata.Name, currentValue, databaseValue);
            entry.Property(property.Metadata.Name).CurrentValue = mergedValue;
        }
        
        return true;
    }

    private static bool IsAuditField(string propertyName)
    {
        return propertyName is "_createdAtUtc" or "_updatedAtUtc" or "_createdBy" or "_updatedBy" or "xmin";
    }

    private object? ApplyPropertyMergeRule(string propertyName, object? currentValue, object? databaseValue)
    {
        // Default: use current value (client wins for business data)
        // Override this method for specific business rules
        return currentValue;
    }
}

/// <summary>
/// Concurrency resolution strategies for conflict handling
/// </summary>
public enum ConcurrencyResolutionStrategy
{
    DatabaseWins,  // Reload from database (lose local changes)
    ClientWins,    // Keep local changes (overwrite database)
    Merge          // Attempt to merge changes intelligently
}
```

## 🔗 INTEGRATION POINTS AND DEPENDENCIES

### Service Registration and Dependency Injection Configuration

```csharp
/// <summary>
/// Complete service registration for data access layer with performance optimizations
/// </summary>
public static class DataAccessServiceRegistration
{
    public static IServiceCollection AddChatDataAccess(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database context with connection pooling
        services.AddPostgreSqlConnection(configuration);
        
        // Interceptors for backing fields pattern
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<DomainEventInterceptor>();
        
        // Repository implementations
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        
        // Unit of Work pattern
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        
        // Concurrency management
        services.AddScoped<PostgreSqlConcurrencyStrategy>();
        
        // Supporting services
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<IDomainEventPublisher, MediatRDomainEventPublisher>();
        
        // Health checks
        services.AddHealthChecks()
            .AddDbContextCheck<ChatDbContext>("chat-database")
            .AddNpgSql(configuration.GetConnectionString("DefaultConnection")!, name: "postgresql");
        
        // Performance monitoring
        services.AddSingleton<IConnectionPoolMetrics, NpgsqlConnectionPoolMetrics>();
        
        return services;
    }

    public static IApplicationBuilder UseChatDataAccess(this IApplicationBuilder app)
    {
        // Ensure database is created and migrations are applied
        using var scope = app.ApplicationServices.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        
        // A10: Apply pending migrations (use CONCURRENTLY for large indexes in production)
        context.Database.Migrate();
        
        // Seed initial data if needed
        SeedInitialData(context);
        
        return app;
    }

    private static void SeedInitialData(ChatDbContext context)
    {
        // Add any required seed data
        // This would typically be minimal for chat domain
        
        if (!context.Database.CanConnect())
        {
            throw new InvalidOperationException("Cannot connect to the database");
        }
        
        // Log successful connection
        var logger = context.GetService<ILogger<ChatDbContext>>();
        logger?.LogInformation("Successfully connected to chat database");
    }
}

/// <summary>
/// Supporting service implementations for data access layer
/// </summary>
public interface ICurrentUserService
{
    string? GetCurrentUserId();
    string? GetCurrentUserName();
}

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetCurrentUserId()
    {
        return _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue("sub");
    }

    public string? GetCurrentUserName()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.Name;
    }
}

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
    DateOnly Today { get; }
}

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
}

/// <summary>
/// Domain event publisher using MediatR for in-process messaging
/// </summary>
public sealed class MediatRDomainEventPublisher : IDomainEventPublisher
{
    private readonly IMediator _mediator;
    private readonly ILogger<MediatRDomainEventPublisher> _logger;

    public MediatRDomainEventPublisher(IMediator mediator, ILogger<MediatRDomainEventPublisher> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Publishing domain event: {EventType}", domainEvent.GetType().Name);
        await _mediator.Publish(domainEvent, cancellationToken);
    }

    public async Task PublishAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        var events = domainEvents.ToList();
        _logger.LogDebug("Publishing {EventCount} domain events", events.Count);
        
        foreach (var domainEvent in events)
        {
            await PublishAsync(domainEvent, cancellationToken);
        }
    }
}
```

## 📊 ARCHITECTURE VALIDATION AND SUCCESS CRITERIA

### Data Access Architecture Quality Gates

✅ **PostgreSQL Native Integration**: System xmin concurrency and generated tsvector columns  
✅ **Backing Fields Pattern**: Direct EF Core mapping to private fields for audit tracking  
✅ **Generic Repository Pattern**: Compiled query integration with IQueryable containment  
✅ **Specialized Repository Implementation**: Domain-specific methods with performance optimization  
✅ **Unit of Work Facade**: Transaction orchestration with distributed transaction support  
✅ **Connection Management**: Advanced pooling and resilience configuration  
✅ **Concurrency Control**: PostgreSQL xmin with automatic conflict resolution  
✅ **Performance Optimization**: Compiled queries, connection pooling, and query splitting  

### Performance Benchmarks and Metrics

- **Query Execution Time**: < 50ms for single entity lookups with compiled queries
- **Batch Operation Performance**: > 1000 entities/second for bulk inserts
- **Connection Pool Efficiency**: < 5ms connection acquisition time
- **Full-Text Search Performance**: < 100ms for tsvector GIN index queries
- **Concurrency Conflict Resolution**: < 200ms average resolution time
- **Transaction Overhead**: < 10ms additional latency for Unit of Work operations

### Security and Data Integrity Validation

- **SQL Injection Prevention**: 100% parameterized queries via EF Core
- **Audit Trail Completeness**: All IAuditable entities tracked via backing fields
- **Concurrency Safety**: PostgreSQL xmin system column for optimistic locking
- **Data Consistency**: ACID transaction guarantees with distributed transaction support
- **Access Control**: Repository pattern prevents direct DbContext access from application layer

---

## 🚀 IMPLEMENTATION ROADMAP

### Phase 1: Core Infrastructure (Week 1)
- Implement ChatDbContext with backing fields configuration
- Create AuditSaveChangesInterceptor with direct field mapping
- Configure PostgreSQL connection with advanced pooling

### Phase 2: Repository Pattern (Week 2)
- Implement generic Repository<TEntity, TId> base class
- Create ConversationRepository and MessageRepository with compiled queries
- Implement EfUnitOfWork with transaction management

### Phase 3: Performance Optimization (Week 3)
- Configure PostgreSQL tsvector and GIN indexes
- Implement query optimization extensions
- Add connection pooling metrics and monitoring

### Phase 4: Concurrency and Testing (Week 4)
- Implement PostgreSQL xmin concurrency strategy
- Create comprehensive integration tests
- Performance benchmarking and optimization

**This comprehensive Data Access & Repository Architecture provides the foundation for high-performance, maintainable, and scalable data persistence in the Axon Backend system, following Clean Architecture principles and SPARC v3 specifications.**