using Axon.BuildingBlocks.Core.Pagination;
using Axon.BuildingBlocks.Persistence.Interfaces;
using Axon.Modules.Chat.Domain.Conversation;
using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using Axon.Modules.Chat.Domain.Conversations;
using Axon.Modules.Chat.Domain.Conversations.Enums;
using Axon.Modules.Chat.Domain.Conversations.ValueObjects;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Results;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;

namespace Axon.Modules.Chat.Application.Repositories;

/// <summary>
/// Simple repository interface for Conversation aggregate
/// </summary>
/// <summary>
/// Write repository interface for Conversation aggregate for command operations
/// </summary>
public interface IConversationWriteRepository : IWriteRepository<Conversation, ConversationId>
{
    /// <summary>
    /// Gets an aggregate with all its related entities for domain event processing
    /// </summary>
    Task<Result<Conversation?>> GetAggregateAsync(ConversationId id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets conversation by ID with all messages for domain operations
    /// </summary>
    Task<Result<Conversation?>> GetByIdWithMessagesAsync(ConversationId id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets aggregates that have pending domain events
    /// </summary>
    Task<Result<IReadOnlyList<Conversation>>> GetAggregatesWithEventsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Read repository interface for Conversation aggregate for query operations
/// </summary>
public interface IConversationReadRepository : IReadRepository<Conversation, ConversationId>
{
    /// <summary>
    /// Gets conversation by ID with all messages for read operations
    /// </summary>
    Task<Result<Conversation?>> GetByIdWithMessagesAsync(ConversationId id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets conversations by user with pagination
    /// </summary>
    Task<Result<PagedResult<Conversation>>> GetByUserAsync(
        string userId, 
        int pageNumber, 
        int pageSize, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets conversations by status with pagination
    /// </summary>
    Task<Result<PagedResult<Conversation>>> GetByStatusAsync(
        ConversationStatus status, 
        int pageNumber, 
        int pageSize, 
        CancellationToken cancellationToken = default);
}