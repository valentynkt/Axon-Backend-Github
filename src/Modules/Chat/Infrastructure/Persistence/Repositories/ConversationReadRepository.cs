using Axon.BuildingBlocks.Core.Pagination;
using Axon.BuildingBlocks.Postgres;
using Axon.Modules.Chat.Domain.Conversation;
using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using Axon.Modules.Chat.Domain.Conversations;
using Axon.Modules.Chat.Domain.Conversations.Enums;
using Axon.Modules.Chat.Domain.Conversations.ValueObjects;
using BuildingBlocks.Core.Diagnostics;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Results;
using MassTransit.Internals;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Repositories;

/// <summary>
/// Conversation read repository implementation for CQRS query operations
/// </summary>
public sealed class ConversationReadRepository : PostgresReadRepository<Conversation, ConversationId>, IConversationReadRepository
{
    public ConversationReadRepository(ChatReadDbContext context) : base(context)
    {
    }

    public async Task<Result<Conversation?>> GetByIdWithMessagesAsync(ConversationId id, CancellationToken cancellationToken = default)
    {
        try
        {
            // Use compiled query for performance
            var conversations = ChatReadDbContext.CompiledQueries.GetConversationById(ReadContext, id);
            var conversation = await conversations.FirstOrDefaultAsync(cancellationToken);
            
            return Result<Conversation?>.Success(conversation);
        }
        catch (Exception ex)
        {
            return Result<Conversation?>.Failure(Error.Persistence(ex.Message));
        }
    }

    public async Task<Result<PagedResult<Conversation>>> GetByUserAsync(
        string userId, 
        int pageNumber, 
        int pageSize, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var skip = (pageNumber - 1) * pageSize;
            
            // Use compiled query for performance
            var conversations = ChatReadDbContext.CompiledQueries.GetConversationsByUser(ReadContext, userId, skip, pageSize);
            var items = await conversations.ToListAsync(cancellationToken);
            
            // Get total count for pagination
            var totalCount = await ReadContext.Set<Conversation>()
                .Where(c => c.UserId == userId)
                .CountAsync(cancellationToken);
            
            var pagedResult = new PagedResult<Conversation>(
                items: items,
                totalCount: totalCount,
                pageNumber: pageNumber,
                pageSize: pageSize);
            
            return Result<PagedResult<Conversation>>.Success(pagedResult);
        }
        catch (Exception ex)
        {
            return Result<PagedResult<Conversation>>.Failure(Error.Persistence(ex.Message));
        }
    }

    public async Task<Result<PagedResult<Conversation>>> GetByStatusAsync(
        ConversationStatus status, 
        int pageNumber, 
        int pageSize, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var skip = (pageNumber - 1) * pageSize;
            
            var query = ReadContext.Set<Conversation>()
                .Where(c => c.Status == status)
                .OrderByDescending(c => EF.Property<DateTime>(c, "_updatedAtUtc"));
            
            var items = await query
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
            
            var totalCount = await ReadContext.Set<Conversation>()
                .Where(c => c.Status == status)
                .CountAsync(cancellationToken);
            
            var pagedResult = new PagedResult<Conversation>(
                items: items,
                totalCount: totalCount,
                pageNumber: pageNumber,
                pageSize: pageSize);
            
            return Result<PagedResult<Conversation>>.Success(pagedResult);
        }
        catch (Exception ex)
        {
            return Result<PagedResult<Conversation>>.Failure(Error.Persistence(ex.Message));
        }
    }

    private static ChatReadDbContext ReadContext => (ChatReadDbContext)Context;
}