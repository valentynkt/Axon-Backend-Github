# Part 4: Chat Infrastructure Layer - Comprehensive Implementation Guide

## Overview
The Infrastructure Layer implements all external concerns: persistence, AI integration, caching, and observability. Everything returns `Result<T>` for consistent error handling.

## Core Principles
- **Clean Separation**: Infrastructure details isolated from domain
- **Railway-Oriented**: All operations return `Result<T>`
- **Resilience**: Polly policies for retries and circuit breakers
- **Performance**: Optimized queries with Dapper for reads
- **Observability**: OpenTelemetry integration throughout

## 1. Entity Framework Configuration

### 1.1 Database Context

```csharp
// Modules/Chat/Infrastructure/Persistence/ChatDbContext.cs
using Microsoft.EntityFrameworkCore;
using Axon.Modules.Chat.Domain.Conversation;
using Axon.Modules.Chat.Infrastructure.Persistence.Configurations;

namespace Axon.Modules.Chat.Infrastructure.Persistence;

public sealed class ChatDbContext : DbContext
{
    public DbSet<Conversation> Conversations { get; set; }
    
    public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("chat");
        
        // Apply configurations
        modelBuilder.ApplyConfiguration(new ConversationConfiguration());
        modelBuilder.ApplyConfiguration(new MessageConfiguration());
        modelBuilder.ApplyConfiguration(new ParticipantConfiguration());
        
        // Add indexes
        modelBuilder.Entity<Conversation>()
            .HasIndex(c => c.StartedAt)
            .HasDatabaseName("IX_Conversation_StartedAt");
            
        modelBuilder.Entity<Conversation>()
            .HasIndex(c => c.Status)
            .HasDatabaseName("IX_Conversation_Status")
            .HasFilter("[Status] = 'Active'");

        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Add audit information
        foreach (var entry in ChangeTracker.Entries<AggregateRoot>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.SetCreatedAt(DateTime.UtcNow);
                    break;
                case EntityState.Modified:
                    entry.Entity.SetUpdatedAt(DateTime.UtcNow);
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
```

### 1.2 Entity Configurations

```csharp
// Modules/Chat/Infrastructure/Persistence/Configurations/ConversationConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Axon.Modules.Chat.Domain.Conversation;
using Axon.Modules.Chat.Domain.Conversation.ValueObjects;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Configurations;

public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations");
        
        // Primary Key
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasConversion(
                id => id.Value,
                value => ConversationId.Create(value).Value)
            .HasColumnName("Id");

        // Value Objects
        builder.Property(c => c.Title)
            .HasConversion(
                title => title.Value,
                value => ConversationTitle.Create(value).Value)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.StartedBy)
            .HasConversion(
                userId => userId.Value,
                value => UserId.Create(value).Value)
            .IsRequired();

        // Enums
        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // Dates
        builder.Property(c => c.StartedAt).IsRequired();
        builder.Property(c => c.LastMessageAt);
        builder.Property(c => c.ArchivedAt);
        
        // Optional fields
        builder.Property(c => c.ArchiveReason).HasMaxLength(500);

        // Complex types
        builder.OwnsOne(c => c.TokenUsage, token =>
        {
            token.Property(t => t.InputTokens).HasColumnName("TokenUsage_Input");
            token.Property(t => t.OutputTokens).HasColumnName("TokenUsage_Output");
            token.Property(t => t.TotalTokens).HasColumnName("TokenUsage_Total");
            token.Property(t => t.EstimatedCost)
                .HasColumnName("TokenUsage_EstimatedCost")
                .HasPrecision(10, 4);
        });

        // JSON column for metadata
        builder.Property(c => c.Metadata)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions)null))
            .HasColumnType("nvarchar(max)");

        // Relationships
        builder.HasMany(c => c.Messages)
            .WithOne()
            .HasForeignKey("ConversationId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Participants)
            .WithOne()
            .HasForeignKey("ConversationId")
            .OnDelete(DeleteBehavior.Cascade);

        // Ignore domain events (handled separately)
        builder.Ignore(c => c.DomainEvents);
    }
}

// Modules/Chat/Infrastructure/Persistence/Configurations/MessageConfiguration.cs
public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages");
        
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .HasConversion(
                id => id.Value,
                value => MessageId.Create(value).Value);

        builder.Property(m => m.ConversationId)
            .HasConversion(
                id => id.Value,
                value => ConversationId.Create(value).Value)
            .IsRequired();

        builder.Property(m => m.UserId)
            .HasConversion(
                id => id.Value,
                value => UserId.Create(value).Value)
            .IsRequired();

        builder.Property(m => m.Content)
            .HasConversion(
                content => content.Value,
                value => MessageContent.Create(value).Value)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(m => m.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.SentAt).IsRequired();
        builder.Property(m => m.EditedAt);

        // JSON columns
        builder.Property(m => m.Attachments)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null))
            .HasColumnType("nvarchar(max)");

        builder.Property(m => m.Metadata)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions)null))
            .HasColumnType("nvarchar(max)");

        // Indexes
        builder.HasIndex(m => m.ConversationId);
        builder.HasIndex(m => m.UserId);
        builder.HasIndex(m => m.SentAt);
    }
}

// Modules/Chat/Infrastructure/Persistence/Configurations/ParticipantConfiguration.cs
public sealed class ParticipantConfiguration : IEntityTypeConfiguration<Participant>
{
    public void Configure(EntityTypeBuilder<Participant> builder)
    {
        builder.ToTable("Participants");
        
        builder.HasKey(p => new { p.ConversationId, p.UserId });

        builder.Property(p => p.UserId)
            .HasConversion(
                id => id.Value,
                value => UserId.Create(value).Value);

        builder.Property(p => p.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.JoinedAt).IsRequired();
        builder.Property(p => p.LeftAt);

        // Indexes
        builder.HasIndex(p => p.UserId);
        builder.HasIndex(p => new { p.ConversationId, p.LeftAt })
            .HasFilter("[LeftAt] IS NULL");
    }
}
```

## 2. Repository Implementations

### 2.1 Write Repository

```csharp
// Modules/Chat/Infrastructure/Persistence/Repositories/ConversationWriteRepository.cs
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Core.Results;
using Axon.Modules.Chat.Domain.Conversation;
using Axon.Modules.Chat.Domain.Conversation.Repositories;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Repositories;

public sealed class ConversationWriteRepository : IConversationWriteRepository
{
    private readonly ChatDbContext _context;
    private readonly ILogger<ConversationWriteRepository> _logger;

    public ConversationWriteRepository(
        ChatDbContext context,
        ILogger<ConversationWriteRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<Unit>> AddAsync(
        Conversation conversation, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.Conversations.AddAsync(conversation, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            
            _logger.LogDebug(
                "Conversation {ConversationId} added successfully",
                conversation.Id.Value);
            
            return Result.Success(Unit.Value);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex,
                "Error adding conversation {ConversationId}",
                conversation.Id.Value);
            
            if (IsDuplicateKeyException(ex))
                return Result<Unit>.Failure(
                    Error.Conflict("Conversation.DuplicateId", 
                        $"Conversation with ID {conversation.Id.Value} already exists"));
            
            return Result<Unit>.Failure(
                Error.Failure("Conversation.AddFailed", 
                    "Failed to add conversation to database"));
        }
    }

    public async Task<Result<Conversation>> GetByIdAsync(
        ConversationId id, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var conversation = await _context.Conversations
                .Include(c => c.Messages)
                .Include(c => c.Participants)
                .AsSplitQuery()
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (conversation == null)
            {
                return Result<Conversation>.Failure(
                    ConversationErrors.ConversationNotFound(id.Value));
            }

            return Result.Success(conversation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving conversation {ConversationId}",
                id.Value);
            
            return Result<Conversation>.Failure(
                Error.Failure("Conversation.RetrieveFailed",
                    "Failed to retrieve conversation from database"));
        }
    }

    public async Task<Result<Unit>> UpdateAsync(
        Conversation conversation, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _context.Conversations.Update(conversation);
            
            // Handle concurrency
            _context.Entry(conversation).Property("RowVersion").IsModified = false;
            
            await _context.SaveChangesAsync(cancellationToken);
            
            _logger.LogDebug(
                "Conversation {ConversationId} updated successfully",
                conversation.Id.Value);
            
            return Result.Success(Unit.Value);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex,
                "Concurrency conflict updating conversation {ConversationId}",
                conversation.Id.Value);
            
            return Result<Unit>.Failure(
                Error.Conflict("Conversation.ConcurrencyConflict",
                    "The conversation was modified by another user"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error updating conversation {ConversationId}",
                conversation.Id.Value);
            
            return Result<Unit>.Failure(
                Error.Failure("Conversation.UpdateFailed",
                    "Failed to update conversation in database"));
        }
    }

    public async Task<Result<Unit>> DeleteAsync(
        ConversationId id, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (conversation == null)
            {
                return Result<Unit>.Failure(
                    ConversationErrors.ConversationNotFound(id.Value));
            }

            _context.Conversations.Remove(conversation);
            await _context.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation(
                "Conversation {ConversationId} deleted",
                id.Value);
            
            return Result.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error deleting conversation {ConversationId}",
                id.Value);
            
            return Result<Unit>.Failure(
                Error.Failure("Conversation.DeleteFailed",
                    "Failed to delete conversation from database"));
        }
    }

    private bool IsDuplicateKeyException(DbUpdateException ex)
    {
        return ex.InnerException?.Message.Contains("duplicate key", 
            StringComparison.OrdinalIgnoreCase) ?? false;
    }
}
```

### 2.2 Read Repository with Dapper

```csharp
// Modules/Chat/Infrastructure/Persistence/Repositories/ConversationReadRepository.cs
using Dapper;
using Microsoft.Data.SqlClient;
using BuildingBlocks.Core.Results;
using Axon.Modules.Chat.Domain.Conversation.Repositories;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Repositories;

public sealed class ConversationReadRepository : IConversationReadRepository
{
    private readonly string _connectionString;
    private readonly ILogger<ConversationReadRepository> _logger;

    public ConversationReadRepository(
        IConfiguration configuration,
        ILogger<ConversationReadRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("ChatDb") 
            ?? throw new InvalidOperationException("ChatDb connection string not configured");
        _logger = logger;
    }

    public async Task<Result<ConversationReadModel>> GetByIdAsync(
        ConversationId id, 
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT 
                c.Id, c.Title, c.Status, c.StartedBy, c.StartedAt,
                c.LastMessageAt, c.TokenUsage_Total as TotalTokens,
                m.Content as LastMessage,
                COUNT(DISTINCT m.Id) as MessageCount,
                COUNT(DISTINCT CASE WHEN m.IsRead = 0 THEN m.Id END) as UnreadCount
            FROM chat.Conversations c
            LEFT JOIN chat.Messages m ON c.Id = m.ConversationId
            WHERE c.Id = @Id
            GROUP BY c.Id, c.Title, c.Status, c.StartedBy, c.StartedAt, 
                     c.LastMessageAt, c.TokenUsage_Total, m.Content
            
            SELECT 
                p.UserId, p.Role, p.JoinedAt
            FROM chat.Participants p
            WHERE p.ConversationId = @Id AND p.LeftAt IS NULL";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            using var multi = await connection.QueryMultipleAsync(
                sql, 
                new { Id = id.Value },
                commandTimeout: 5);

            var conversation = await multi.ReadSingleOrDefaultAsync<ConversationReadModel>();
            if (conversation == null)
            {
                return Result<ConversationReadModel>.Failure(
                    ConversationErrors.ConversationNotFound(id.Value));
            }

            conversation.Participants = (await multi.ReadAsync<ParticipantReadModel>()).ToList();

            return Result.Success(conversation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving conversation read model {ConversationId}",
                id.Value);
            
            return Result<ConversationReadModel>.Failure(
                Error.Failure("Conversation.ReadFailed",
                    "Failed to retrieve conversation"));
        }
    }

    public async Task<Result<IEnumerable<ConversationReadModel>>> GetByUserIdAsync(
        UserId userId, 
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT DISTINCT
                c.Id, c.Title, c.Status, c.StartedBy, c.StartedAt,
                c.LastMessageAt,
                (SELECT TOP 1 m.Content 
                 FROM chat.Messages m 
                 WHERE m.ConversationId = c.Id 
                 ORDER BY m.SentAt DESC) as LastMessage,
                (SELECT COUNT(*) 
                 FROM chat.Messages m2 
                 WHERE m2.ConversationId = c.Id) as MessageCount,
                (SELECT COUNT(*) 
                 FROM chat.Messages m3 
                 WHERE m3.ConversationId = c.Id AND m3.IsRead = 0) as UnreadCount
            FROM chat.Conversations c
            LEFT JOIN chat.Participants p ON c.Id = p.ConversationId
            WHERE (c.StartedBy = @UserId OR p.UserId = @UserId)
                AND c.Status = 'Active'
            ORDER BY c.LastMessageAt DESC";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var conversations = await connection.QueryAsync<ConversationReadModel>(
                sql,
                new { UserId = userId.Value },
                commandTimeout: 10);

            return Result.Success(conversations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving conversations for user {UserId}",
                userId.Value);
            
            return Result<IEnumerable<ConversationReadModel>>.Failure(
                Error.Failure("Conversations.ReadFailed",
                    "Failed to retrieve user conversations"));
        }
    }

    public async Task<Result<PagedResult<ConversationReadModel>>> GetPagedAsync(
        ISpecification<ConversationReadModel> specification, 
        CancellationToken cancellationToken = default)
    {
        var (whereClause, parameters) = BuildWhereClause(specification);
        var orderByClause = BuildOrderByClause(specification);
        var pagingClause = BuildPagingClause(specification);

        var countSql = $@"
            SELECT COUNT(DISTINCT c.Id)
            FROM chat.Conversations c
            LEFT JOIN chat.Participants p ON c.Id = p.ConversationId
            {whereClause}";

        var dataSql = $@"
            SELECT DISTINCT
                c.Id, c.Title, c.Status, c.StartedBy, c.StartedAt,
                c.LastMessageAt,
                (SELECT TOP 1 m.Content 
                 FROM chat.Messages m 
                 WHERE m.ConversationId = c.Id 
                 ORDER BY m.SentAt DESC) as LastMessage,
                (SELECT COUNT(*) 
                 FROM chat.Messages m2 
                 WHERE m2.ConversationId = c.Id) as MessageCount
            FROM chat.Conversations c
            LEFT JOIN chat.Participants p ON c.Id = p.ConversationId
            {whereClause}
            {orderByClause}
            {pagingClause}";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            
            var totalCount = await connection.QuerySingleAsync<int>(
                countSql, 
                parameters,
                commandTimeout: 5);

            var items = await connection.QueryAsync<ConversationReadModel>(
                dataSql,
                parameters,
                commandTimeout: 10);

            var result = new PagedResult<ConversationReadModel>
            {
                Items = items.ToList(),
                TotalCount = totalCount,
                PageNumber = specification.PageNumber,
                PageSize = specification.PageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)specification.PageSize)
            };

            return Result.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged conversations");
            
            return Result<PagedResult<ConversationReadModel>>.Failure(
                Error.Failure("Conversations.PagedReadFailed",
                    "Failed to retrieve paged conversations"));
        }
    }

    public async Task<Result<bool>> ExistsAsync(
        ConversationId id, 
        CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT COUNT(1) FROM chat.Conversations WHERE Id = @Id";

        try
        {
            using var connection = new SqlConnection(_connectionString);
            var exists = await connection.QuerySingleAsync<int>(
                sql,
                new { Id = id.Value },
                commandTimeout: 2) > 0;

            return Result.Success(exists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error checking conversation existence {ConversationId}",
                id.Value);
            
            return Result<bool>.Failure(
                Error.Failure("Conversation.ExistenceCheckFailed",
                    "Failed to check conversation existence"));
        }
    }

    private (string whereClause, DynamicParameters parameters) BuildWhereClause(
        ISpecification<ConversationReadModel> specification)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        foreach (var criteria in specification.Criteria)
        {
            // Convert expression to SQL
            // This is simplified - real implementation would use expression visitor
            conditions.Add(criteria.ToSql());
        }

        var whereClause = conditions.Any() 
            ? $"WHERE {string.Join(" AND ", conditions)}" 
            : string.Empty;

        return (whereClause, parameters);
    }

    private string BuildOrderByClause(ISpecification<ConversationReadModel> specification)
    {
        if (specification.OrderBy == null)
            return "ORDER BY c.LastMessageAt DESC";

        // Convert expression to SQL column name
        return $"ORDER BY {specification.OrderBy.ToSql()} {(specification.OrderByDescending ? "DESC" : "ASC")}";
    }

    private string BuildPagingClause(ISpecification<ConversationReadModel> specification)
    {
        if (!specification.IsPagingEnabled)
            return string.Empty;

        var offset = (specification.PageNumber - 1) * specification.PageSize;
        return $"OFFSET {offset} ROWS FETCH NEXT {specification.PageSize} ROWS ONLY";
    }
}
```

## 3. OpenAI Integration

### 3.1 AI Service Implementation

```csharp
// Modules/Chat/Infrastructure/AI/OpenAiService.cs
using OpenAI;
using OpenAI.Chat;
using BuildingBlocks.Core.Results;
using Axon.Modules.Chat.Application.Services;
using Polly;
using Polly.CircuitBreaker;

namespace Axon.Modules.Chat.Infrastructure.AI;

public sealed class OpenAiService : IAiService
{
    private readonly OpenAIClient _client;
    private readonly IAsyncPolicy<ChatCompletion> _retryPolicy;
    private readonly ILogger<OpenAiService> _logger;
    private readonly IMetrics _metrics;

    public OpenAiService(
        IConfiguration configuration,
        ILogger<OpenAiService> logger,
        IMetrics metrics)
    {
        var apiKey = configuration["OpenAI:ApiKey"] 
            ?? throw new InvalidOperationException("OpenAI API key not configured");
        
        _client = new OpenAIClient(apiKey);
        _logger = logger;
        _metrics = metrics;
        
        // Configure Polly policies
        _retryPolicy = Policy<ChatCompletion>
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<OpenAIException>(ex => ex.StatusCode >= 500)
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "Retry {RetryCount} after {Delay}ms for OpenAI request",
                        retryCount, timespan.TotalMilliseconds);
                })
            .WrapAsync(Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(
                    5,
                    TimeSpan.FromMinutes(1),
                    onBreak: (result, duration) =>
                    {
                        _logger.LogError(
                            "Circuit breaker opened for {Duration}",
                            duration);
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("Circuit breaker reset");
                    }));
    }

    public async Task<Result<AiResponse>> GenerateResponseAsync(
        ConversationContext context,
        AiModel model,
        Dictionary<string, object>? parameters,
        CancellationToken cancellationToken)
    {
        using var activity = Activity.StartActivity("OpenAI.GenerateResponse");
        activity?.SetTag("ai.model", model.ToString());
        activity?.SetTag("ai.message_count", context.Messages.Count);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var messages = BuildChatMessages(context);
            var modelName = GetOpenAiModelName(model);
            
            var chatRequest = new ChatCompletionCreateRequest
            {
                Model = modelName,
                Messages = messages,
                MaxTokens = context.MaxTokens,
                Temperature = parameters?.GetValueOrDefault("temperature") as float? ?? 0.7f,
                TopP = parameters?.GetValueOrDefault("top_p") as float? ?? 1.0f,
                PresencePenalty = parameters?.GetValueOrDefault("presence_penalty") as float? ?? 0.0f,
                FrequencyPenalty = parameters?.GetValueOrDefault("frequency_penalty") as float? ?? 0.0f,
                User = parameters?.GetValueOrDefault("user_id") as string
            };

            var completion = await _retryPolicy.ExecuteAsync(async () =>
                await _client.Chat.CreateCompletionAsync(chatRequest, cancellationToken));

            stopwatch.Stop();

            var response = completion.Choices.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrEmpty(response))
            {
                return Result<AiResponse>.Failure(
                    Error.Failure("AI.EmptyResponse", "AI returned empty response"));
            }

            // Record metrics
            _metrics.RecordAiRequestDuration(model.ToString(), stopwatch.ElapsedMilliseconds);
            _metrics.IncrementAiRequestCount(model.ToString(), "success");
            _metrics.RecordTokenUsage(
                model.ToString(),
                completion.Usage?.PromptTokens ?? 0,
                completion.Usage?.CompletionTokens ?? 0);

            activity?.SetTag("ai.tokens.prompt", completion.Usage?.PromptTokens);
            activity?.SetTag("ai.tokens.completion", completion.Usage?.CompletionTokens);
            activity?.SetStatus(ActivityStatusCode.Ok);

            _logger.LogInformation(
                "Generated AI response using {Model} in {ElapsedMs}ms with {Tokens} tokens",
                model, stopwatch.ElapsedMilliseconds, completion.Usage?.TotalTokens);

            return Result.Success(new AiResponse
            {
                Content = response,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                Metadata = new Dictionary<string, object>
                {
                    ["model"] = completion.Model,
                    ["prompt_tokens"] = completion.Usage?.PromptTokens ?? 0,
                    ["completion_tokens"] = completion.Usage?.CompletionTokens ?? 0,
                    ["finish_reason"] = completion.Choices.FirstOrDefault()?.FinishReason ?? "unknown"
                }
            });
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "Circuit breaker is open for OpenAI");
            activity?.SetStatus(ActivityStatusCode.Error, "Circuit breaker open");
            _metrics.IncrementAiRequestCount(model.ToString(), "circuit_breaker");
            
            return Result<AiResponse>.Failure(
                Error.Unavailable("AI.ServiceUnavailable", 
                    "AI service is temporarily unavailable"));
        }
        catch (OpenAIException ex) when (ex.StatusCode == 429)
        {
            _logger.LogWarning(ex, "Rate limit exceeded for OpenAI");
            activity?.SetStatus(ActivityStatusCode.Error, "Rate limited");
            _metrics.IncrementAiRequestCount(model.ToString(), "rate_limited");
            
            return Result<AiResponse>.Failure(
                Error.TooManyRequests("AI.RateLimited", 
                    "AI service rate limit exceeded"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating AI response");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _metrics.IncrementAiRequestCount(model.ToString(), "error");
            
            return Result<AiResponse>.Failure(
                Error.Failure("AI.GenerationFailed", 
                    "Failed to generate AI response"));
        }
    }

    private List<ChatMessage> BuildChatMessages(ConversationContext context)
    {
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, context.SystemPrompt)
        };

        foreach (var message in context.Messages)
        {
            var role = message.Role.ToLower() switch
            {
                "user" => ChatRole.User,
                "assistant" => ChatRole.Assistant,
                "system" => ChatRole.System,
                _ => ChatRole.User
            };

            messages.Add(new ChatMessage(role, message.Content));
        }

        return messages;
    }

    private string GetOpenAiModelName(AiModel model)
    {
        return model switch
        {
            AiModel.GPT4 => "gpt-4-turbo-preview",
            AiModel.GPT35Turbo => "gpt-3.5-turbo",
            _ => "gpt-4-turbo-preview"
        };
    }
}
```

### 3.2 Content Moderation Service

```csharp
// Modules/Chat/Infrastructure/AI/ContentModerationService.cs
namespace Axon.Modules.Chat.Infrastructure.AI;

public interface IContentModerationService
{
    Task<ModerationResult> CheckContentAsync(string content, CancellationToken cancellationToken = default);
}

public sealed class ContentModerationService : IContentModerationService
{
    private readonly OpenAIClient _client;
    private readonly ILogger<ContentModerationService> _logger;

    public ContentModerationService(
        IConfiguration configuration,
        ILogger<ContentModerationService> logger)
    {
        var apiKey = configuration["OpenAI:ApiKey"] 
            ?? throw new InvalidOperationException("OpenAI API key not configured");
        
        _client = new OpenAIClient(apiKey);
        _logger = logger;
    }

    public async Task<ModerationResult> CheckContentAsync(
        string content, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.Moderations.CreateModerationAsync(
                new ModerationCreateRequest
                {
                    Input = content,
                    Model = "text-moderation-latest"
                },
                cancellationToken);

            var result = response.Results.FirstOrDefault();
            if (result == null)
            {
                return new ModerationResult { IsHarmful = false };
            }

            var categories = new List<string>();
            if (result.Categories.Hate) categories.Add("hate");
            if (result.Categories.HateThreatening) categories.Add("hate/threatening");
            if (result.Categories.SelfHarm) categories.Add("self-harm");
            if (result.Categories.Sexual) categories.Add("sexual");
            if (result.Categories.SexualMinors) categories.Add("sexual/minors");
            if (result.Categories.Violence) categories.Add("violence");
            if (result.Categories.ViolenceGraphic) categories.Add("violence/graphic");

            return new ModerationResult
            {
                IsHarmful = result.Flagged,
                Categories = categories,
                Scores = new Dictionary<string, float>
                {
                    ["hate"] = result.CategoryScores.Hate,
                    ["hate/threatening"] = result.CategoryScores.HateThreatening,
                    ["self-harm"] = result.CategoryScores.SelfHarm,
                    ["sexual"] = result.CategoryScores.Sexual,
                    ["sexual/minors"] = result.CategoryScores.SexualMinors,
                    ["violence"] = result.CategoryScores.Violence,
                    ["violence/graphic"] = result.CategoryScores.ViolenceGraphic
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking content moderation");
            
            // Fail open - don't block on moderation errors
            return new ModerationResult { IsHarmful = false };
        }
    }
}

public sealed class ModerationResult
{
    public bool IsHarmful { get; init; }
    public List<string> Categories { get; init; } = new();
    public Dictionary<string, float> Scores { get; init; } = new();
}
```

## 4. Caching Implementation

### 4.1 Redis Cache Service

```csharp
// Modules/Chat/Infrastructure/Caching/RedisCacheService.cs
using StackExchange.Redis;
using System.Text.Json;
using BuildingBlocks.Core.Results;
using Axon.Modules.Chat.Application.Services;

namespace Axon.Modules.Chat.Infrastructure.Caching;

public sealed class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public RedisCacheService(
        IConnectionMultiplexer redis,
        ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _database = redis.GetDatabase();
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };
    }

    public async Task<Option<T>> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        try
        {
            var value = await _database.StringGetAsync(key);
            
            if (!value.HasValue)
            {
                _logger.LogDebug("Cache miss for key {Key}", key);
                return Option<T>.None();
            }

            var deserialized = JsonSerializer.Deserialize<T>(value!, _jsonOptions);
            if (deserialized == null)
            {
                _logger.LogWarning("Failed to deserialize cache value for key {Key}", key);
                return Option<T>.None();
            }

            _logger.LogDebug("Cache hit for key {Key}", key);
            return Option<T>.Some(deserialized);
        }
        catch (RedisException ex)
        {
            _logger.LogError(ex, "Redis error getting key {Key}", key);
            return Option<T>.None();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache key {Key}", key);
            return Option<T>.None();
        }
    }

    public async Task SetAsync<T>(
        string key, 
        T value, 
        TimeSpan expiry, 
        CancellationToken cancellationToken)
    {
        try
        {
            var serialized = JsonSerializer.Serialize(value, _jsonOptions);
            
            await _database.StringSetAsync(
                key, 
                serialized, 
                expiry,
                When.Always,
                CommandFlags.FireAndForget);
                
            _logger.LogDebug("Set cache key {Key} with expiry {Expiry}", key, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache key {Key}", key);
            // Don't throw - caching errors shouldn't break the application
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await _database.KeyDeleteAsync(key, CommandFlags.FireAndForget);
            _logger.LogDebug("Removed cache key {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache key {Key}", key);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken)
    {
        try
        {
            var endpoints = _redis.GetEndPoints();
            var server = _redis.GetServer(endpoints.First());
            
            var keys = server.Keys(pattern: $"{prefix}*", pageSize: 1000);
            
            foreach (var key in keys)
            {
                await _database.KeyDeleteAsync(key, CommandFlags.FireAndForget);
            }
            
            _logger.LogDebug("Removed cache keys with prefix {Prefix}", prefix);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache keys with prefix {Prefix}", prefix);
        }
    }
}
```

## 5. Search Implementation

### 5.1 Elasticsearch Search Service

```csharp
// Modules/Chat/Infrastructure/Search/ElasticsearchSearchService.cs
using Nest;
using BuildingBlocks.Core.Results;
using Axon.Modules.Chat.Application.Services;

namespace Axon.Modules.Chat.Infrastructure.Search;

public sealed class ElasticsearchSearchService : IConversationSearchService
{
    private readonly IElasticClient _client;
    private readonly ILogger<ElasticsearchSearchService> _logger;
    private const string IndexName = "conversations";

    public ElasticsearchSearchService(
        IElasticClient client,
        ILogger<ElasticsearchSearchService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<Result<ConversationSearchResponse>> SearchAsync(
        ConversationSearchRequest request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var searchDescriptor = new SearchDescriptor<ConversationDocument>()
                .Index(IndexName)
                .Size(request.MaxResults)
                .Query(q => BuildQuery(q, request))
                .Highlight(h => h
                    .Fields(
                        f => f.Field(doc => doc.Title),
                        f => f.Field(doc => doc.Messages)
                    )
                    .PreTags("<mark>")
                    .PostTags("</mark>"))
                .Aggregations(a => a
                    .Terms("status", t => t.Field(f => f.Status.Keyword))
                    .DateHistogram("timeline", d => d
                        .Field(f => f.LastMessageAt)
                        .CalendarInterval(DateInterval.Day)));

            var response = await _client.SearchAsync<ConversationDocument>(
                searchDescriptor,
                cancellationToken);

            if (!response.IsValid)
            {
                _logger.LogError(
                    "Elasticsearch query failed: {Error}",
                    response.ServerError?.Error);
                
                return Result<ConversationSearchResponse>.Failure(
                    Error.Failure("Search.Failed", "Search query failed"));
            }

            stopwatch.Stop();

            var results = response.Documents.Select((doc, index) => 
            {
                var hit = response.Hits.ElementAt(index);
                return new ConversationSearchResult
                {
                    Id = doc.Id,
                    Title = doc.Title,
                    Status = doc.Status,
                    MatchedContent = GetMatchedContent(hit),
                    Highlights = GetHighlights(hit),
                    Score = (float)(hit.Score ?? 0)
                };
            }).ToList();

            var facets = ExtractFacets(response.Aggregations);

            return Result.Success(new ConversationSearchResponse
            {
                Query = request.SearchTerm,
                TotalHits = (int)response.Total,
                Results = results,
                Facets = facets,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching conversations");
            
            return Result<ConversationSearchResponse>.Failure(
                Error.Failure("Search.Error", "An error occurred during search"));
        }
    }

    private QueryContainer BuildQuery(
        QueryContainerDescriptor<ConversationDocument> q,
        ConversationSearchRequest request)
    {
        var must = new List<QueryContainer>();

        // User access filter
        must.Add(q.Bool(b => b
            .Should(
                s => s.Term(t => t.Field(f => f.StartedBy).Value(request.UserId.Value)),
                s => s.Term(t => t.Field(f => f.ParticipantIds).Value(request.UserId.Value))
            )));

        // Search term
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            must.Add(q.MultiMatch(m => m
                .Query(request.SearchTerm)
                .Fields(f => f
                    .Field(ff => ff.Title, boost: 2)
                    .Field(ff => ff.Messages)
                )
                .Type(TextQueryType.BestFields)
                .Fuzziness(Fuzziness.Auto)));
        }

        // Status filter
        if (request.StatusFilters?.Any() == true)
        {
            must.Add(q.Terms(t => t
                .Field(f => f.Status.Keyword)
                .Terms(request.StatusFilters.Select(s => s.ToString()))));
        }

        // Date range filter
        if (request.DateFrom.HasValue || request.DateTo.HasValue)
        {
            must.Add(q.DateRange(d => d
                .Field(f => f.LastMessageAt)
                .GreaterThanOrEquals(request.DateFrom)
                .LessThanOrEquals(request.DateTo)));
        }

        return q.Bool(b => b.Must(must.ToArray()));
    }

    private string GetMatchedContent(IHit<ConversationDocument> hit)
    {
        if (hit.Highlight?.Any() == true)
        {
            return hit.Highlight.Values.First().First();
        }

        return hit.Source.Messages?.FirstOrDefault()?.Substring(0, Math.Min(200, hit.Source.Messages.First().Length)) ?? "";
    }

    private List<string> GetHighlights(IHit<ConversationDocument> hit)
    {
        if (hit.Highlight?.Any() != true)
            return new List<string>();

        return hit.Highlight.SelectMany(h => h.Value).ToList();
    }

    private Dictionary<string, List<FacetValue>> ExtractFacets(IAggregate aggregations)
    {
        var facets = new Dictionary<string, List<FacetValue>>();

        if (aggregations?.GetTerms("status") is BucketAggregate statusAgg)
        {
            facets["status"] = statusAgg.Items.OfType<KeyedBucket<string>>()
                .Select(b => new FacetValue
                {
                    Value = b.Key,
                    Count = b.DocCount ?? 0
                })
                .ToList();
        }

        return facets;
    }
}

public sealed class ConversationDocument
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid StartedBy { get; set; }
    public List<Guid> ParticipantIds { get; set; } = new();
    public DateTime LastMessageAt { get; set; }
    public List<string> Messages { get; set; } = new();
}

public sealed class FacetValue
{
    public string Value { get; set; } = string.Empty;
    public long Count { get; set; }
}
```

## 6. Event Bus Implementation

### 6.1 In-Memory Event Bus

```csharp
// Modules/Chat/Infrastructure/Events/InMemoryEventBus.cs
using BuildingBlocks.Core.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Axon.Modules.Chat.Infrastructure.Events;

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) 
        where TEvent : IDomainEvent;
}

public sealed class InMemoryEventBus : IEventBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InMemoryEventBus> _logger;

    public InMemoryEventBus(
        IServiceProvider serviceProvider,
        ILogger<InMemoryEventBus> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(
        TEvent @event, 
        CancellationToken cancellationToken = default) 
        where TEvent : IDomainEvent
    {
        using var scope = _serviceProvider.CreateScope();
        
        var handlers = scope.ServiceProvider
            .GetServices<IDomainEventHandler<TEvent>>()
            .ToList();

        if (!handlers.Any())
        {
            _logger.LogDebug(
                "No handlers registered for event {EventType}",
                typeof(TEvent).Name);
            return;
        }

        var tasks = handlers.Select(async handler =>
        {
            try
            {
                await handler.HandleAsync(@event, cancellationToken);
                
                _logger.LogDebug(
                    "Successfully handled {EventType} with {HandlerType}",
                    typeof(TEvent).Name,
                    handler.GetType().Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error handling {EventType} with {HandlerType}",
                    typeof(TEvent).Name,
                    handler.GetType().Name);
                    
                // Don't rethrow - one handler failure shouldn't affect others
            }
        });

        await Task.WhenAll(tasks);
    }
}
```

## 7. Observability

### 7.1 OpenTelemetry Configuration

```csharp
// Modules/Chat/Infrastructure/Observability/ObservabilityExtensions.cs
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;

namespace Axon.Modules.Chat.Infrastructure.Observability;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddChatObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var serviceName = "Axon.Chat";
        var serviceVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

        // Add OpenTelemetry
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName, serviceVersion)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["environment"] = configuration["Environment"] ?? "Development",
                    ["deployment.environment"] = configuration["Environment"] ?? "Development"
                }))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.Filter = httpContext => !httpContext.Request.Path.StartsWithSegments("/health");
                })
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation(options =>
                {
                    options.SetDbStatementForText = true;
                    options.SetDbStatementForStoredProcedure = true;
                })
                .AddSqlClientInstrumentation(options =>
                {
                    options.SetDbStatementForText = true;
                    options.RecordException = true;
                })
                .AddRedisInstrumentation()
                .AddSource("Axon.Chat.*")
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317");
                }))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("Axon.Chat.*")
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(configuration["OpenTelemetry:Endpoint"] ?? "http://localhost:4317");
                }));

        // Add custom metrics
        services.AddSingleton<IMetrics, ChatMetrics>();

        return services;
    }
}

// Modules/Chat/Infrastructure/Observability/ChatMetrics.cs
public interface IMetrics
{
    void RecordRequestDuration(string operation, long milliseconds);
    void IncrementRequestCount(string operation, string status);
    void IncrementErrorCount(string operation);
    void RecordAiRequestDuration(string model, long milliseconds);
    void IncrementAiRequestCount(string model, string status);
    void RecordTokenUsage(string model, int promptTokens, int completionTokens);
}

public sealed class ChatMetrics : IMetrics
{
    private readonly Meter _meter;
    private readonly Counter<long> _requestCounter;
    private readonly Histogram<long> _requestDuration;
    private readonly Counter<long> _errorCounter;
    private readonly Counter<long> _aiRequestCounter;
    private readonly Histogram<long> _aiRequestDuration;
    private readonly Counter<long> _tokenCounter;

    public ChatMetrics()
    {
        _meter = new Meter("Axon.Chat.Metrics", "1.0.0");
        
        _requestCounter = _meter.CreateCounter<long>(
            "chat.requests.total",
            description: "Total number of requests");
            
        _requestDuration = _meter.CreateHistogram<long>(
            "chat.request.duration",
            unit: "ms",
            description: "Request duration in milliseconds");
            
        _errorCounter = _meter.CreateCounter<long>(
            "chat.errors.total",
            description: "Total number of errors");
            
        _aiRequestCounter = _meter.CreateCounter<long>(
            "chat.ai.requests.total",
            description: "Total number of AI requests");
            
        _aiRequestDuration = _meter.CreateHistogram<long>(
            "chat.ai.request.duration",
            unit: "ms",
            description: "AI request duration in milliseconds");
            
        _tokenCounter = _meter.CreateCounter<long>(
            "chat.ai.tokens.total",
            description: "Total number of tokens used");
    }

    public void RecordRequestDuration(string operation, long milliseconds)
    {
        _requestDuration.Record(milliseconds, 
            new KeyValuePair<string, object?>("operation", operation));
    }

    public void IncrementRequestCount(string operation, string status)
    {
        _requestCounter.Add(1,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("status", status));
    }

    public void IncrementErrorCount(string operation)
    {
        _errorCounter.Add(1,
            new KeyValuePair<string, object?>("operation", operation));
    }

    public void RecordAiRequestDuration(string model, long milliseconds)
    {
        _aiRequestDuration.Record(milliseconds,
            new KeyValuePair<string, object?>("model", model));
    }

    public void IncrementAiRequestCount(string model, string status)
    {
        _aiRequestCounter.Add(1,
            new KeyValuePair<string, object?>("model", model),
            new KeyValuePair<string, object?>("status", status));
    }

    public void RecordTokenUsage(string model, int promptTokens, int completionTokens)
    {
        _tokenCounter.Add(promptTokens,
            new KeyValuePair<string, object?>("model", model),
            new KeyValuePair<string, object?>("type", "prompt"));
            
        _tokenCounter.Add(completionTokens,
            new KeyValuePair<string, object?>("model", model),
            new KeyValuePair<string, object?>("type", "completion"));
    }
}
```

## 8. Module Registration

```csharp
// Modules/Chat/Infrastructure/InfrastructureModule.cs
namespace Axon.Modules.Chat.Infrastructure;

public static class InfrastructureModule
{
    public static IServiceCollection AddChatInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddDbContext<ChatDbContext>(options =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("ChatDb"),
                sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(ChatDbContext).Assembly.FullName);
                    sqlOptions.EnableRetryOnFailure(3);
                    sqlOptions.CommandTimeout(30);
                });
                
            if (configuration.GetValue<bool>("EnableSensitiveDataLogging"))
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

        // Repositories
        services.AddScoped<IConversationWriteRepository, ConversationWriteRepository>();
        services.AddScoped<IConversationReadRepository, ConversationReadRepository>();

        // Redis
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var connectionString = configuration.GetConnectionString("Redis") 
                ?? "localhost:6379";
            
            var options = ConfigurationOptions.Parse(connectionString);
            options.AbortOnConnectFail = false;
            options.ConnectRetry = 3;
            options.ConnectTimeout = 5000;
            
            return ConnectionMultiplexer.Connect(options);
        });
        
        services.AddSingleton<ICacheService, RedisCacheService>();

        // Elasticsearch
        services.AddSingleton<IElasticClient>(sp =>
        {
            var uri = new Uri(configuration["Elasticsearch:Uri"] ?? "http://localhost:9200");
            var settings = new ConnectionSettings(uri)
                .DefaultIndex("conversations")
                .EnableApiVersioningHeader()
                .ThrowExceptions();
                
            return new ElasticClient(settings);
        });
        
        services.AddScoped<IConversationSearchService, ElasticsearchSearchService>();

        // AI Services
        services.AddScoped<IAiService, OpenAiService>();
        services.AddScoped<IContentModerationService, ContentModerationService>();
        
        // Domain Services Implementation
        services.AddScoped<IMessageValidator, MessageValidator>();
        services.AddScoped<IAiResponseProcessor, AiResponseProcessor>();
        services.AddScoped<IArchivePolicy, ArchivePolicy>();

        // Event Bus
        services.AddSingleton<IEventBus, InMemoryEventBus>();

        // Observability
        services.AddChatObservability(configuration);

        return services;
    }
}
```

## Summary

This Infrastructure Layer implementation provides:

1. **Entity Framework Configuration**: Complete mappings with value object conversions
2. **Repository Pattern**: Separate read/write with Dapper optimization for reads
3. **OpenAI Integration**: Full implementation with Polly resilience policies
4. **Redis Caching**: Distributed caching with proper error handling
5. **Elasticsearch**: Full-text search with faceting and highlighting
6. **Event Bus**: In-memory implementation ready for upgrade
7. **Observability**: Complete OpenTelemetry integration
8. **Resilience**: Circuit breakers, retries, and fallbacks throughout

The infrastructure layer is fully railway-oriented with all operations returning `Result<T>` and includes comprehensive logging and metrics.