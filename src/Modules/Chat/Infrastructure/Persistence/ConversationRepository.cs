namespace Axon.Modules.Chat.Infrastructure.Persistence;

using BuildingBlocks.Application;
using Axon.Modules.Chat.Application.Abstractions.Persistence;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Primitives.ValueObjects;
using BuildingBlocks.Infrastructure.Persistence.Write;

public sealed class ConversationRepository : EfWriteRepository<Conversation, ConversationId>, IConversationRepository
{
    private readonly IWriteUnitOfWork _unitOfWork;

    public ConversationRepository(ChatDbContext context, IWriteUnitOfWork unitOfWork) : base(context)
    {
        _unitOfWork = unitOfWork;
    }

    public IWriteUnitOfWork UnitOfWork => _unitOfWork;
}