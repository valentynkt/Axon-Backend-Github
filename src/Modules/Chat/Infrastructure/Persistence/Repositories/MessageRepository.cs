using Axon.Modules.Chat.Application.Repositories;
using Axon.Modules.Chat.Domain.Conversation.Entities;
using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Shared.Common;
using BuildingBlocks.Core.Functional.Results;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Repositories;

/// <summary>
/// Simple repository implementation for Message entity
/// </summary>
public sealed class MessageRepository : Repository<Message, MessageId>, IMessageRepository
{
    public MessageRepository(ChatDbContext context) : base(context)
    {
    }

    public async Task<Result<IReadOnlyList<Message>>> GetByConversationAsync(
        ConversationId conversationId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var messages = await Context.Messages
                .AsNoTracking()
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.Sequence)
                .ToListAsync(cancellationToken);

            return Result<IReadOnlyList<Message>>.Success(messages.AsReadOnly());
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<Message>>.Failure(Error.Persistence($"Failed to get messages by conversation: {ex.Message}"));
        }
    }

    public async Task<Result<Message?>> GetLatestByConversationAsync(
        ConversationId conversationId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = await Context.Messages
                .AsNoTracking()
                .Where(m => m.ConversationId == conversationId)
                .OrderByDescending(m => m.Sequence)
                .FirstOrDefaultAsync(cancellationToken);

            return Result<Message?>.Success(message);
        }
        catch (Exception ex)
        {
            return Result<Message?>.Failure(Error.Persistence($"Failed to get latest message: {ex.Message}"));
        }
    }
}