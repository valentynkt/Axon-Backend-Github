using Ardalis.Specification;
using Axon.Modules.Chat.Application.Abstractions.Persistence;
using Axon.Modules.Chat.Application.Queries.GetConversations;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.Specifications;
using Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;
using BuildingBlocks.Infrastructure.Persistence.Read;
using BuildingBlocks.Primitives.Ids;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Repositories;

/// <summary>
/// Read-only repository implementation for conversation queries using Entity Framework Core.
/// Supports specification-based queries through Ardalis.Specification integration.
/// Includes compiled queries for optimal performance on hot paths.
/// </summary>
internal sealed class ConversationReadRepository : EfSpecificationReadRepository<Conversation>, IConversationReadRepository
{
    private readonly ChatReadDbContext _chatDbContext;

    /// <summary>
    /// Compiled query for fetching conversations by owner with projection - hot path optimization
    /// SQLite-compatible: Using DateTime conversion for ordering to avoid DateTimeOffset ORDER BY issues
    /// </summary>
    private static readonly Func<ChatReadDbContext, AxonUserId, IAsyncEnumerable<ConversationListItem>>
        GetConversationsForOwnerCompiled = EF.CompileAsyncQuery(
            (ChatReadDbContext context, AxonUserId ownerId) =>
                context.Set<Conversation>()
                    .Where(c => c.OwnerId == ownerId && c.Status == ConversationStatus.Active)
                    .Select(c => new ConversationListItem(
                        c.Id.Value,
                        c.Title ?? string.Empty,
                        c.CreatedAt.DateTime,
                        c.UpdatedAt.HasValue ? c.UpdatedAt.Value.DateTime : c.CreatedAt.DateTime,
                        c.LastAiResponseId.HasValue ? c.LastAiResponseId.Value.Value : null))
                    .AsNoTracking());

    /// <summary>
    /// Compiled query for counting conversations by owner - optimized for count operations
    /// </summary>
    private static readonly Func<ChatReadDbContext, AxonUserId, Task<int>> 
        CountConversationsForOwnerCompiled = EF.CompileAsyncQuery(
            (ChatReadDbContext context, AxonUserId ownerId) =>
                context.Set<Conversation>()
                    .Where(c => c.OwnerId == ownerId && c.Status == ConversationStatus.Active)
                    .Count());

    /// <summary>
    /// Compiled query for title search with count - hot path for filtered queries
    /// EF Core compatible: Using string concatenation instead of interpolation to avoid translation issues
    /// </summary>
    private static readonly Func<ChatReadDbContext, AxonUserId, string, Task<int>>
        CountConversationsWithTitleCompiled = EF.CompileAsyncQuery(
            (ChatReadDbContext context, AxonUserId ownerId, string titleLower) =>
                context.Set<Conversation>()
                    .Where(c => c.OwnerId == ownerId &&
                               c.Status == ConversationStatus.Active &&
                               c.Title != null &&
                               EF.Functions.Like(c.Title.ToLower(), "%" + titleLower + "%"))
                    .Count());

    public ConversationReadRepository(ChatReadDbContext dbContext) : base(dbContext)
    {
        _chatDbContext = dbContext;
    }

    /// <summary>
    /// Optimized method for getting conversations by owner.
    /// Should be used for the most common GetConversations scenarios.
    /// Note: Temporarily using regular queries instead of compiled queries due to SQLite compatibility issues.
    /// </summary>
    public async Task<IReadOnlyList<ConversationListItem>> GetConversationsForOwnerOptimizedAsync(
        AxonUserId ownerId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        // SQLite doesn't support DateTimeOffset ordering, so we order in memory after projection
        var results = await _chatDbContext.Set<Conversation>()
            .Where(c => c.OwnerId == ownerId && c.Status == ConversationStatus.Active)
            .Select(c => new ConversationListItem(
                c.Id.Value,
                c.Title ?? string.Empty,
                c.CreatedAt.DateTime,
                c.UpdatedAt.HasValue ? c.UpdatedAt.Value.DateTime : c.CreatedAt.DateTime,
                c.LastAiResponseId.HasValue ? c.LastAiResponseId.Value.Value : null))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Order and paginate in memory
        var orderedResults = results
            .OrderByDescending(c => c.CreatedAtUtc)
            .ThenByDescending(c => c.ConversationId)
            .Skip(skip)
            .Take(take)
            .ToList();

        return orderedResults.AsReadOnly();
    }

    /// <summary>
    /// Optimized method for counting conversations by owner.
    /// Note: Temporarily using regular queries instead of compiled queries due to SQLite compatibility issues.
    /// </summary>
    public Task<int> CountConversationsForOwnerOptimizedAsync(
        AxonUserId ownerId,
        CancellationToken cancellationToken = default)
    {
        return _chatDbContext.Set<Conversation>()
            .Where(c => c.OwnerId == ownerId && c.Status == ConversationStatus.Active)
            .CountAsync(cancellationToken);
    }

    /// <summary>
    /// Optimized method for counting conversations with title filter.
    /// Note: Temporarily using regular queries instead of compiled queries due to SQLite compatibility issues.
    /// </summary>
    public Task<int> CountConversationsWithTitleOptimizedAsync(
        AxonUserId ownerId,
        string titleContains,
        CancellationToken cancellationToken = default)
    {
        var titleLower = titleContains.Trim().ToLower();

        // Return 0 for empty or whitespace-only search strings
        if (string.IsNullOrWhiteSpace(titleLower))
            return Task.FromResult(0);

        return _chatDbContext.Set<Conversation>()
            .Where(c => c.OwnerId == ownerId &&
                       c.Status == ConversationStatus.Active &&
                       c.Title != null &&
                       EF.Functions.Like(c.Title.ToLower(), "%" + titleLower + "%"))
            .CountAsync(cancellationToken);
    }

    // Note: RecentlyUpdatedByOwnerSpec now uses PostProcessingAction for date filtering
    // to handle SQLite DateTimeOffset limitations, so no repository override is needed

    /// <summary>
    /// Override GetByIdAsync to explicitly include Messages navigation property.
    /// The base implementation from Ardalis.Specification doesn't automatically include navigation properties.
    /// </summary>
    public override async Task<Conversation?> GetByIdAsync<TId>(TId id, CancellationToken cancellationToken = default)
    {
        return await _chatDbContext.Set<Conversation>()
            .Include("_messages")
            .AsNoTracking()
            .FirstOrDefaultAsync(c => EF.Property<object>(c, "Id").Equals(id), cancellationToken);
    }

}