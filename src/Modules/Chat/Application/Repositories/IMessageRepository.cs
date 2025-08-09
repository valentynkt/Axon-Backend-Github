using Axon.BuildingBlocks.Core.Pagination;
using Axon.BuildingBlocks.Persistence.Interfaces;
using Axon.Modules.Chat.Domain.Conversation.ValueObjects;
using Axon.Modules.Chat.Domain.Messages;
using Axon.Modules.Chat.Domain.Messages.ValueObjects;
using Axon.Modules.Chat.Domain.Conversations.ValueObjects;
using BuildingBlocks.Application.Abstractions.Persistence;
using BuildingBlocks.Core.Abstractions.Pagination;
using BuildingBlocks.Core.Functional.Results;
using BuildingBlocks.Core.Results;
using BuildingBlocks.Infrastructure.Persistence.Common.Interfaces;

namespace Axon.Modules.Chat.Application.Repositories;

/// <summary>
/// Simple repository interface for Message entity
/// </summary>
/// <summary>
/// Write repository interface for Message entity for command operations
/// </summary>
public interface IMessageWriteRepository : IWriteRepository<Message, MessageId>
{
    /// <summary>
    /// Gets message by ID for domain operations
    /// </summary>
    Task<Result<Message?>> GetByIdAsync(MessageId id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Read repository interface for Message entity for query operations
/// </summary>
public interface IMessageReadRepository : IReadRepository<Message, MessageId>
{
    /// <summary>
    /// Gets messages by conversation ID for read operations
    /// </summary>
    Task<Result<IReadOnlyList<Message>>> GetByConversationAsync(
        ConversationId conversationId, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the latest message in a conversation
    /// </summary>
    Task<Result<Message?>> GetLatestByConversationAsync(
        ConversationId conversationId, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets messages by conversation with pagination
    /// </summary>
    Task<Result<PagedResult<Message>>> GetByConversationPagedAsync(
        ConversationId conversationId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}