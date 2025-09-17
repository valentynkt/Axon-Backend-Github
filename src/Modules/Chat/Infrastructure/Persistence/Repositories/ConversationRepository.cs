using Axon.Modules.Chat.Application.Common.Models;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Application.Contracts.Persistence;
using Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Persistence.Write;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Repositories;


public sealed class ConversationRepository : EfWriteRepository<Conversation, ConversationId>, IConversationRepository
{
    private readonly IWriteUnitOfWork<ChatModule> _unitOfWork;

    public ConversationRepository(ChatDbContext context, IWriteUnitOfWork<ChatModule> unitOfWork) : base(context)
    {
        _unitOfWork = unitOfWork;
    }

    public IWriteUnitOfWork<ChatModule> UnitOfWork => _unitOfWork;
}