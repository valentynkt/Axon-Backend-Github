using Axon.BuildingBlocks.Persistence.Interfaces;
using Axon.BuildingBlocks.Postgres;
using Axon.Modules.Chat.Application.Repositories;
using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using Axon.Modules.Chat.Domain.Conversations;
using Axon.Modules.Chat.Domain.Conversations.ValueObjects;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Results;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Repositories;

/// <summary>
/// Conversation write repository implementation for CQRS command operations
/// </summary>
public sealed class ConversationWriteRepository : PostgresWriteRepository<Conversation, ConversationId>, IConversationWriteRepository
{
    public ConversationWriteRepository(ChatWriteDbContext context) : base(context)
    {
    }

    public async Task<Result<Conversation?>> GetAggregateAsync(ConversationId id, CancellationToken cancellationToken = default)
    {
        try
        {
            var conversation = await Context.Set<Conversation>()
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            
            return Result<Conversation?>.Success(conversation);
        }
        catch (Exception ex)
        {
            return Result<Conversation?>.Failure(Error.Persistence(ex.Message));
        }
    }

    public async Task<Result<Conversation?>> GetByIdWithMessagesAsync(ConversationId id, CancellationToken cancellationToken = default)
    {
        try
        {
            var conversation = await Context.Set<Conversation>()
                .Include(c => c.Messages.OrderBy(m => m.Sequence))
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            
            return Result<Conversation?>.Success(conversation);
        }
        catch (Exception ex)
        {
            return Result<Conversation?>.Failure(Error.Persistence(ex.Message));
        }
    }

    public async Task<Result<IReadOnlyList<Conversation>>> GetAggregatesWithEventsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var conversations = await Context.Set<Conversation>()
                .Where(c => c.DomainEvents.Any())
                .Include(c => c.Messages)
                .ToListAsync(cancellationToken);
            
            return Result<IReadOnlyList<Conversation>>.Success(conversations);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Conversation>>.Failure(Error.Persistence(ex.Message));
        }
    }
}