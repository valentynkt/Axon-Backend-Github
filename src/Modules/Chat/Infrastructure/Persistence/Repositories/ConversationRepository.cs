using Axon.Modules.Chat.Application.Common.Models;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Application.Contracts.Persistence;
using Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Persistence.Write;
using BuildingBlocks.Primitives.Ids;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Repositories;


public sealed class ConversationRepository : EfWriteRepository<Conversation, ConversationId>, IConversationRepository
{
    private readonly IWriteUnitOfWork<ChatModule> _unitOfWork;
    private readonly ChatDbContext _chatDbContext;

    public ConversationRepository(ChatDbContext context, IWriteUnitOfWork<ChatModule> unitOfWork) : base(context)
    {
        _unitOfWork = unitOfWork;
        _chatDbContext = context;
    }

    public IWriteUnitOfWork<ChatModule> UnitOfWork => _unitOfWork;

    /// <summary>
    /// Override GetByIdAsync to include Messages navigation property.
    /// The base implementation uses FindAsync which doesn't include navigation properties.
    /// This is required for domain operations that depend on the full aggregate (like Complete).
    /// </summary>
    public override async Task<Conversation?> GetByIdAsync(ConversationId id, CancellationToken ct = default)
    {
        return await _chatDbContext.Set<Conversation>()
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }
}