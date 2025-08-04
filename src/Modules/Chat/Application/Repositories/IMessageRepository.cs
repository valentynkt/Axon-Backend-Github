using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Shared.Common;

namespace Axon.Modules.Chat.Application.Repositories;

/// <summary>
/// Simple repository interface for Message entity
/// </summary>
public interface IMessageRepository
{
    Task<Result> AddAsync(Message message, CancellationToken cancellationToken = default);
    Task<Result> UpdateAsync(Message message, CancellationToken cancellationToken = default);
    Task<Result<Message?>> GetByIdAsync(MessageId id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<Message>>> GetByConversationAsync(
        ConversationId conversationId, 
        CancellationToken cancellationToken = default);
    Task<Result<Message?>> GetLatestByConversationAsync(
        ConversationId conversationId, 
        CancellationToken cancellationToken = default);
}