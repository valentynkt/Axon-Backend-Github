using Axon.BuildingBlocks.Postgres;
using Axon.Modules.Chat.Domain.Messages;
using Axon.Modules.Chat.Domain.Messages.ValueObjects;
using BuildingBlocks.Core.Results;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Repositories;

/// <summary>
/// Message write repository implementation for CQRS command operations
/// </summary>
public sealed class MessageWriteRepository : PostgresWriteRepository<Message, MessageId>, IMessageWriteRepository
{
    public MessageWriteRepository(ChatWriteDbContext context) : base(context)
    {
    }

    public async Task<Result<Message?>> GetByIdAsync(MessageId id, CancellationToken cancellationToken = default)
    {
        try
        {
            var message = await Context.Set<Message>()
                .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
            
            return Result<Message?>.Success(message);
        }
        catch (Exception ex)
        {
            return Result<Message?>.Failure(Error.Persistence(ex.Message));
        }
    }
}