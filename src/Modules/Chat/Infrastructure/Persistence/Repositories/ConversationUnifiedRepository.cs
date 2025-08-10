using Axon.Modules.Chat.Application.Repositories;
using Axon.Modules.Chat.Domain.Conversations;
using Axon.Modules.Chat.Domain.Conversations.Enums;
using Axon.Modules.Chat.Domain.Conversations.ValueObjects;
using BuildingBlocks.Core.Abstractions.Pagination;
using BuildingBlocks.Core.Functional.Results;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Repositories;

/// <summary>
/// Unified conversation repository that delegates to CQRS read/write repositories
/// </summary>
public sealed class ConversationUnifiedRepository : IConversationRepository
{
    private readonly IConversationWriteRepository _writeRepository;
    private readonly IConversationReadRepository _readRepository;

    public ConversationUnifiedRepository(
        IConversationWriteRepository writeRepository,
        IConversationReadRepository readRepository)
    {
        _writeRepository = writeRepository;
        _readRepository = readRepository;
    }

    // Write operations delegate to write repository
    public Task<Result> AddAsync(Conversation entity, CancellationToken cancellationToken = default)
        => _writeRepository.AddAsync(entity, cancellationToken);

    public Task<Result> UpdateAsync(Conversation entity, CancellationToken cancellationToken = default)
        => _writeRepository.UpdateAsync(entity, cancellationToken);

    public Task<Result> DeleteAsync(ConversationId id, CancellationToken cancellationToken = default)
        => _writeRepository.DeleteAsync(id, cancellationToken);

    public Task<Result<Conversation?>> GetAggregateAsync(ConversationId id, CancellationToken cancellationToken = default)
        => _writeRepository.GetAggregateAsync(id, cancellationToken);

    public Task<Result<Conversation?>> GetByIdWithMessagesAsync(ConversationId id, CancellationToken cancellationToken = default)
        => _writeRepository.GetByIdWithMessagesAsync(id, cancellationToken);

    public Task<Result<IReadOnlyList<Conversation>>> GetAggregatesWithEventsAsync(CancellationToken cancellationToken = default)
        => _writeRepository.GetAggregatesWithEventsAsync(cancellationToken);

    // Read operations delegate to read repository  
    public Task<Result<Conversation?>> GetByIdAsync(ConversationId id, CancellationToken cancellationToken = default)
        => _readRepository.GetByIdAsync(id, cancellationToken);

    public Task<Result<bool>> ExistsAsync(ConversationId id, CancellationToken cancellationToken = default)
        => _readRepository.ExistsAsync(id, cancellationToken);

    public Task<Result<int>> CountAsync(CancellationToken cancellationToken = default)
        => _readRepository.CountAsync(cancellationToken);

    public Task<Result<PagedResult<Conversation>>> GetByUserAsync(string userId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        => _readRepository.GetByUserAsync(userId, pageNumber, pageSize, cancellationToken);

    public Task<Result<PagedResult<Conversation>>> GetByStatusAsync(ConversationStatus status, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        => _readRepository.GetByStatusAsync(status, pageNumber, pageSize, cancellationToken);
}