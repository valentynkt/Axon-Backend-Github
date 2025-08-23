using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Errors;
using Axon.Modules.Chat.Application.Abstractions.Persistence;
using Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Persistence.Write;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Repositories;


public sealed class ConversationRepository : EfWriteRepository<Conversation, ConversationId>, IConversationRepository
{
    private readonly IWriteUnitOfWork _unitOfWork;

    public ConversationRepository(ChatDbContext context, IWriteUnitOfWork unitOfWork) : base(context)
    {
        _unitOfWork = unitOfWork;
    }

    public IWriteUnitOfWork UnitOfWork => _unitOfWork;
}