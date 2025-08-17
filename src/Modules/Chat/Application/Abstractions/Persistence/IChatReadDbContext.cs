using Axon.Modules.Chat.Application.DTOs;
using Axon.Modules.Chat.Application.Persistence;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;

namespace Axon.Modules.Chat.Application.Abstractions.Persistence;

public interface IChatReadDbContext : IReadDbContext<ChatModule>
{
    Task<IEnumerable<ConversationIdRow>> GetConversationIdsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ConversationHeaderRow?> GetConversationHeaderAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<MessageRow>> GetConversationMessagesAsync(Guid conversationId, CancellationToken cancellationToken = default);
}