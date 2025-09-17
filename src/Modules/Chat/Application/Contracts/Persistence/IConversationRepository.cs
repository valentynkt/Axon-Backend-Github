using Axon.Modules.Chat.Application.Common.Models;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using BuildingBlocks.Application;
using BuildingBlocks.Primitives.Ids;

namespace Axon.Modules.Chat.Application.Contracts.Persistence;

/// <summary>
/// Repository for Conversation aggregate persistence
/// </summary>
public interface IConversationRepository : IWriteRepository<Conversation, ConversationId>
{
    /// <summary>
    /// Gets the associated unit of work for transaction management
    /// </summary>
    IWriteUnitOfWork<ChatModule> UnitOfWork { get; }
}