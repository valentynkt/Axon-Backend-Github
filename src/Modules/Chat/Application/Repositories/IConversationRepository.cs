using Axon.Modules.Chat.Domain.Aggregates;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Shared.Common;

namespace Axon.Modules.Chat.Application.Repositories;

/// <summary>
/// Simple repository interface for Conversation aggregate
/// </summary>
public interface IConversationRepository
{
    Task<Result> AddAsync(Conversation conversation, CancellationToken cancellationToken = default);
    Task<Result> UpdateAsync(Conversation conversation, CancellationToken cancellationToken = default);
    Task<Result<Conversation?>> GetAggregateAsync(ConversationId id, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<Conversation>>> GetByStatusAsync(
        ConversationStatus status,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
    Task<Result<PagedResult<Conversation>>> GetRecentAsync(
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);
}