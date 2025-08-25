using Axon.Modules.Chat.Application.Abstractions.Persistence;
using Axon.Modules.Chat.Application.Queries.GetConversations;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
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
    /// </summary>
    private static readonly Func<ChatReadDbContext, UserId, int, int, IAsyncEnumerable<ConversationListItem>> 
        GetConversationsForOwnerCompiled = EF.CompileAsyncQuery(
            (ChatReadDbContext context, UserId ownerId, int skip, int take) =>
                context.Set<Conversation>()
                    .Where(c => c.OwnerId == ownerId && c.Status == ConversationStatus.Active)
                    .OrderByDescending(c => c.UpdatedAt)
                    .ThenByDescending(c => c.Id)
                    .Skip(skip)
                    .Take(take)
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
    private static readonly Func<ChatReadDbContext, UserId, Task<int>> 
        CountConversationsForOwnerCompiled = EF.CompileAsyncQuery(
            (ChatReadDbContext context, UserId ownerId) =>
                context.Set<Conversation>()
                    .Where(c => c.OwnerId == ownerId && c.Status == ConversationStatus.Active)
                    .Count());

    /// <summary>
    /// Compiled query for title search with count - hot path for filtered queries
    /// </summary>
    private static readonly Func<ChatReadDbContext, UserId, string, Task<int>> 
        CountConversationsWithTitleCompiled = EF.CompileAsyncQuery(
            (ChatReadDbContext context, UserId ownerId, string titleLower) =>
                context.Set<Conversation>()
                    .Where(c => c.OwnerId == ownerId && 
                               c.Status == ConversationStatus.Active &&
                               c.Title != null && 
                               EF.Functions.Like(c.Title.ToLower(), $"%{titleLower}%"))
                    .Count());

    public ConversationReadRepository(ChatReadDbContext dbContext) : base(dbContext)
    {
        _chatDbContext = dbContext;
    }

    /// <summary>
    /// Optimized method for getting conversations by owner using compiled queries.
    /// Should be used for the most common GetConversations scenarios.
    /// </summary>
    public async Task<IReadOnlyList<ConversationListItem>> GetConversationsForOwnerOptimizedAsync(
        UserId ownerId, 
        int skip, 
        int take,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ConversationListItem>();
        await foreach (var item in GetConversationsForOwnerCompiled(_chatDbContext, ownerId, skip, take)
                          .WithCancellation(cancellationToken))
        {
            results.Add(item);
        }
        return results.AsReadOnly();
    }

    /// <summary>
    /// Optimized method for counting conversations by owner using compiled queries.
    /// </summary>
    public Task<int> CountConversationsForOwnerOptimizedAsync(
        UserId ownerId, 
        CancellationToken cancellationToken = default)
    {
        return CountConversationsForOwnerCompiled(_chatDbContext, ownerId);
    }

    /// <summary>
    /// Optimized method for counting conversations with title filter using compiled queries.
    /// </summary>
    public Task<int> CountConversationsWithTitleOptimizedAsync(
        UserId ownerId, 
        string titleContains,
        CancellationToken cancellationToken = default)
    {
        var titleLower = titleContains.Trim().ToLower();
        return CountConversationsWithTitleCompiled(_chatDbContext, ownerId, titleLower);
    }
}