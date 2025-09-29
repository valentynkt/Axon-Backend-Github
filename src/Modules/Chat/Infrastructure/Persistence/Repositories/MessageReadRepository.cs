using Ardalis.Specification;
using Axon.Modules.Chat.Application.Abstractions.Persistence;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;
using BuildingBlocks.Primitives.Ids;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Repositories;

/// <summary>
/// Custom read-only repository implementation for message queries.
/// Since Messages are owned entities of Conversation aggregate (following DDD),
/// this repository queries messages through the Conversation aggregate root.
///
/// NOTE: This is a simplified implementation that satisfies the interface requirements
/// while respecting the owned entity constraints. For production use, consider
/// creating a database view or projection for more efficient message queries.
/// </summary>
internal sealed class MessageReadRepository : IMessageReadRepository
{
    private readonly ChatReadDbContext _dbContext;

    public MessageReadRepository(ChatReadDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Message?> GetByIdAsync<TId>(TId id, CancellationToken cancellationToken = default) where TId : notnull
    {
        // Since Message is an owned entity, we need to query through Conversation
        var conversations = await _dbContext.Set<Conversation>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Find the message in loaded conversations
        foreach (var conversation in conversations)
        {
            if (id is MessageId messageId)
            {
                var message = conversation.GetAllMessages().FirstOrDefault(m => m.Id == messageId);
                if (message != null)
                    return message;
            }
        }

        return null;
    }

    public async Task<Message?> FirstOrDefaultAsync(ISpecification<Message> specification, CancellationToken cancellationToken = default)
    {
        var messages = await GetAllMessagesAsync(cancellationToken);

        // Apply specification using Where clause
        if (specification.WhereExpressions != null && specification.WhereExpressions.Any())
        {
            var query = messages.AsQueryable();
            foreach (var whereExpression in specification.WhereExpressions)
            {
                query = query.Where(whereExpression.Filter);
            }
            return query.FirstOrDefault();
        }

        return messages.FirstOrDefault();
    }

    public async Task<TResult?> FirstOrDefaultAsync<TResult>(ISpecification<Message, TResult> specification, CancellationToken cancellationToken = default)
    {
        var message = await FirstOrDefaultAsync((ISpecification<Message>)specification, cancellationToken);

        if (message == null) return default;

        // Apply selector if present
        if (specification.Selector != null)
        {
            var compiled = specification.Selector.Compile();
            return compiled(message);
        }

        return default;
    }

    public async Task<Message?> SingleOrDefaultAsync(ISpecification<Message> specification, CancellationToken cancellationToken = default)
    {
        var messages = await GetAllMessagesAsync(cancellationToken);

        // Apply specification using Where clause
        if (specification.WhereExpressions != null && specification.WhereExpressions.Any())
        {
            var query = messages.AsQueryable();
            foreach (var whereExpression in specification.WhereExpressions)
            {
                query = query.Where(whereExpression.Filter);
            }
            return query.SingleOrDefault();
        }

        return messages.SingleOrDefault();
    }

    public async Task<TResult?> SingleOrDefaultAsync<TResult>(ISpecification<Message, TResult> specification, CancellationToken cancellationToken = default)
    {
        var message = await SingleOrDefaultAsync((ISpecification<Message>)specification, cancellationToken);

        if (message == null) return default;

        // Apply selector if present
        if (specification.Selector != null)
        {
            var compiled = specification.Selector.Compile();
            return compiled(message);
        }

        return default;
    }

    public async Task<Message?> SingleOrDefaultAsync(ISingleResultSpecification<Message> specification, CancellationToken cancellationToken = default)
    {
        return await SingleOrDefaultAsync((ISpecification<Message>)specification, cancellationToken);
    }

    public async Task<TResult?> SingleOrDefaultAsync<TResult>(ISingleResultSpecification<Message, TResult> specification, CancellationToken cancellationToken = default)
    {
        return await SingleOrDefaultAsync<TResult>((ISpecification<Message, TResult>)specification, cancellationToken);
    }

    public async Task<List<Message>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await GetAllMessagesAsync(cancellationToken);
    }

    public async Task<List<Message>> ListAsync(ISpecification<Message> specification, CancellationToken cancellationToken = default)
    {
        var messages = await GetAllMessagesAsync(cancellationToken);

        // Apply specification using Where clause
        if (specification.WhereExpressions != null && specification.WhereExpressions.Any())
        {
            var query = messages.AsQueryable();
            foreach (var whereExpression in specification.WhereExpressions)
            {
                query = query.Where(whereExpression.Filter);
            }

            // Apply ordering if specified
            if (specification.OrderExpressions != null && specification.OrderExpressions.Any())
            {
                IOrderedQueryable<Message>? orderedQuery = null;
                foreach (var orderExpression in specification.OrderExpressions)
                {
                    if (orderedQuery == null)
                    {
                        orderedQuery = orderExpression.OrderType == OrderTypeEnum.OrderBy
                            ? query.OrderBy(orderExpression.KeySelector)
                            : query.OrderByDescending(orderExpression.KeySelector);
                    }
                    else
                    {
                        orderedQuery = orderExpression.OrderType == OrderTypeEnum.OrderBy
                            ? orderedQuery.ThenBy(orderExpression.KeySelector)
                            : orderedQuery.ThenByDescending(orderExpression.KeySelector);
                    }
                }
                query = orderedQuery ?? query;
            }

            // Apply pagination if specified
            if (specification.Skip > 0)
                query = query.Skip(specification.Skip);

            if (specification.Take > 0)
                query = query.Take(specification.Take);

            return query.ToList();
        }

        return messages;
    }

    public async Task<List<TResult>> ListAsync<TResult>(ISpecification<Message, TResult> specification, CancellationToken cancellationToken = default)
    {
        var messages = await ListAsync((ISpecification<Message>)specification, cancellationToken);

        // Apply selector
        if (specification.Selector != null)
        {
            var compiled = specification.Selector.Compile();
            return messages.Select(compiled).ToList();
        }

        return new List<TResult>();
    }

    public async Task<int> CountAsync(ISpecification<Message> specification, CancellationToken cancellationToken = default)
    {
        var messages = await GetAllMessagesAsync(cancellationToken);

        // Apply specification using Where clause
        if (specification.WhereExpressions != null && specification.WhereExpressions.Any())
        {
            var query = messages.AsQueryable();
            foreach (var whereExpression in specification.WhereExpressions)
            {
                query = query.Where(whereExpression.Filter);
            }
            return query.Count();
        }

        return messages.Count;
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var messages = await GetAllMessagesAsync(cancellationToken);
        return messages.Count;
    }

    public async Task<bool> AnyAsync(ISpecification<Message> specification, CancellationToken cancellationToken = default)
    {
        var messages = await GetAllMessagesAsync(cancellationToken);

        // Apply specification using Where clause
        if (specification.WhereExpressions != null && specification.WhereExpressions.Any())
        {
            var query = messages.AsQueryable();
            foreach (var whereExpression in specification.WhereExpressions)
            {
                query = query.Where(whereExpression.Filter);
            }
            return query.Any();
        }

        return messages.Any();
    }

    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        var messages = await GetAllMessagesAsync(cancellationToken);
        return messages.Any();
    }

    public IAsyncEnumerable<Message> AsAsyncEnumerable(ISpecification<Message> specification)
    {
        return AsAsyncEnumerableInternal(specification);
    }

    private async IAsyncEnumerable<Message> AsAsyncEnumerableInternal(ISpecification<Message> specification)
    {
        var messages = await ListAsync(specification);
        foreach (var message in messages)
        {
            yield return message;
        }
    }

    /// <summary>
    /// Helper method to get all messages from all conversations.
    /// Since Messages are owned entities, we must query through the Conversation aggregate.
    /// </summary>
    private async Task<List<Message>> GetAllMessagesAsync(CancellationToken cancellationToken = default)
    {
        // Load all conversations with their messages
        var conversations = await _dbContext.Set<Conversation>()
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Extract all messages from conversations
        var allMessages = new List<Message>();
        foreach (var conversation in conversations)
        {
            allMessages.AddRange(conversation.GetAllMessages());
        }

        return allMessages;
    }
}