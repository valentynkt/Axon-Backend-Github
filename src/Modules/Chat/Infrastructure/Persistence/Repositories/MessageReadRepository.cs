using Axon.BuildingBlocks.Core.Pagination;
using Axon.BuildingBlocks.Postgres;
using Axon.Modules.Chat.Application.Repositories;
using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using Axon.Modules.Chat.Domain.Messages;
using Axon.Modules.Chat.Domain.Messages.ValueObjects;
using Axon.Modules.Chat.Domain.Conversations.ValueObjects;
using Axon.Modules.Chat.Domain.ValueObjects;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Pagination;
using BuildingBlocks.Core.Results;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Repositories;

/// <summary>
/// Message read repository implementation for CQRS query operations
/// </summary>
public sealed class MessageReadRepository : PostgresReadRepository<Message, MessageId>, IMessageReadRepository
{
    public MessageReadRepository(ChatReadDbContext context) : base(context)
    {
    }

    public async Task<Result<IReadOnlyList<Message>>> GetByConversationAsync(
        ConversationId conversationId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var messages = await Context.Set<Message>()
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.Sequence)
                .ToListAsync(cancellationToken);
            
            return Result<IReadOnlyList<Message>>.Success(messages);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Message>>.Failure(Error.Persistence(ex.Message));
        }
    }

    public async Task<Result<Message?>> GetLatestByConversationAsync(
        ConversationId conversationId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = await Context.Set<Message>()
                .Where(m => m.ConversationId == conversationId)
                .OrderByDescending(m => m.Sequence)
                .FirstOrDefaultAsync(cancellationToken);
            
            return Result<Message?>.Success(message);
        }
        catch (Exception ex)
        {
            return Result<Message?>.Failure(Error.Persistence(ex.Message));
        }
    }

    public async Task<Result<PagedResult<Message>>> GetByConversationPagedAsync(
        ConversationId conversationId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var skip = (pageNumber - 1) * pageSize;
            
            var query = Context.Set<Message>()
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.Sequence);
            
            var items = await query
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
            
            var totalCount = await Context.Set<Message>()
                .Where(m => m.ConversationId == conversationId)
                .CountAsync(cancellationToken);
            
            var pagedResult = new PagedResult<Message>(
                items: items,
                totalCount: totalCount,
                pageNumber: pageNumber,
                pageSize: pageSize);
            
            return Result<PagedResult<Message>>.Success(pagedResult);
        }
        catch (Exception ex)
        {
            return Result<PagedResult<Message>>.Failure(Error.Persistence(ex.Message));
        }
    }
}