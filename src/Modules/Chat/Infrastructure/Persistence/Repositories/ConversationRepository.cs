using Axon.Modules.Chat.Application.Commands.ProcessMessage;
using Axon.Modules.Chat.Application.Repositories;
using Axon.Modules.Chat.Domain.Conversation;
using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using Axon.Shared.Common;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Repositories;

/// <summary>
/// Simple repository implementation for Conversation aggregate
/// </summary>
public sealed class ConversationRepository : Repository<Conversation, ConversationId>, IConversationRepository
{
    public ConversationRepository(ChatDbContext context) : base(context)
    {
    }

    public async Task<Result<Conversation?>> GetAggregateAsync(ConversationId id, CancellationToken cancellationToken = default)
    {
        try
        {
            // Load conversation - messages will be loaded separately when needed
            // This approach maintains aggregate boundaries and prevents EF Core navigation issues
            var conversation = await Context.Conversations
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

            // Note: Messages are not pre-loaded here to maintain clean aggregate boundaries
            // The conversation aggregate will load messages through domain methods as needed
            // This prevents EF Core navigation property complications and shadow property issues

            return Result<Conversation?>.Success(conversation);
        }
        catch (Exception ex)
        {
            return Result<Conversation?>.Failure(Error.Persistence($"Failed to get conversation aggregate: {ex.Message}"));
        }
    }

    public async Task<Result<PagedResult<Conversation>>> GetByStatusAsync(
        ConversationStatus status, 
        int pageNumber = 1, 
        int pageSize = 20, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = Context.Conversations
                .AsNoTracking()
                .Where(c => c.Status == status)
                .OrderByDescending(c => c.CreatedAtUtc);

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var pagedResult = PagedResult<Conversation>.Create(items, pageNumber, pageSize, totalCount);
            return Result<PagedResult<Conversation>>.Success(pagedResult);
        }
        catch (Exception ex)
        {
            return Result<PagedResult<Conversation>>.Failure(Error.Persistence($"Failed to get conversations by status: {ex.Message}"));
        }
    }

    public async Task<Result<PagedResult<Conversation>>> GetRecentAsync(
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = Context.Conversations
                .AsNoTracking()
                .OrderByDescending(c => c.CreatedAtUtc);

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            var pagedResult = PagedResult<Conversation>.Create(items, pageNumber, pageSize, totalCount);
            return Result<PagedResult<Conversation>>.Success(pagedResult);
        }
        catch (Exception ex)
        {
            return Result<PagedResult<Conversation>>.Failure(Error.Persistence($"Failed to get recent conversations: {ex.Message}"));
        }
    }
}