using Axon.Modules.Chat.Domain.Errors;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure.Persistence.Write;
using BuildingBlocks.Primitives.Ids;
namespace Axon.Modules.Chat.Infrastructure.Persistence;
using Axon.Modules.Chat.Application.Abstractions.Persistence;


public sealed class ConversationRepository : EfWriteRepository<ChatErrors.Conversation, ConversationId>, IConversationRepository
{
    private readonly IWriteUnitOfWork _unitOfWork;

    public ConversationRepository(ChatDbContext context, IWriteUnitOfWork unitOfWork) : base(context)
    {
        _unitOfWork = unitOfWork;
    }

    public IWriteUnitOfWork UnitOfWork => _unitOfWork;
}