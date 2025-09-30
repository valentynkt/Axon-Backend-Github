using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;
using BuildingBlocks.Primitives.Ids;
using Microsoft.EntityFrameworkCore;

namespace Axon.Modules.Chat.Infrastructure.Persistence.TestInfrastructure;

/// <summary>
/// Implementation of test-specific data verification queries using LINQ.
/// Provides clean abstraction over database queries used in tests,
/// eliminating the need for direct SQL queries in test code.
/// </summary>
public sealed class TestDataVerificationRepository : ITestDataVerificationRepository
{
    private readonly ChatDbContext _context;

    public TestDataVerificationRepository(ChatReadDbContext readContext, ChatDbContext writeContext)
    {
        // We mainly use write context since messages are owned entities and need the aggregate
        // Keep readContext parameter for consistency with base classes even though we don't use it
        _ = readContext;
        _context = writeContext;
    }

    /// <inheritdoc />
    public async Task<int?> GetMessageSequenceAsync(ConversationId conversationId, MessageId messageId, CancellationToken ct = default)
    {
        // Access messages through the conversation aggregate's public methods
        var conversation = await _context.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId, ct);

        if (conversation == null) return null;

        var message = conversation.GetAllMessages()
            .FirstOrDefault(m => m.Id == messageId);

        return message?.Sequence;
    }

    /// <inheritdoc />
    public async Task<List<MessageSequenceInfo>> GetMessageSequencesAsync(ConversationId conversationId, CancellationToken ct = default)
    {
        var conversation = await _context.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId, ct);

        if (conversation == null)
            return new List<MessageSequenceInfo>();

        return conversation.GetAllMessages()
            .OrderBy(m => m.Sequence)
            .Select(m => new MessageSequenceInfo(m.Sequence, m.Id))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<bool> IsMessageDeletedAsync(MessageId messageId, CancellationToken ct = default)
    {
        // Find the message across all conversations using GetAllMessages
        var conversations = await _context.Conversations
            .AsNoTracking()
            .ToListAsync(ct);

        foreach (var conversation in conversations)
        {
            var message = conversation.GetAllMessages().FirstOrDefault(m => m.Id == messageId);
            if (message != null)
                return message.IsDeleted;
        }

        return false;
    }

    /// <inheritdoc />
    public async Task<uint> GetConversationVersionAsync(ConversationId conversationId, CancellationToken ct = default)
    {
        // Query the actual xmin value from PostgreSQL
        // xmin is mapped to the Version property in the aggregate root
        var conversation = await _context.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId, ct);

        if (conversation == null)
            return 0;

        // Return the actual Version (xmin) value from the entity
        // This is populated by EF Core when the entity is loaded from the database
        return conversation.Version;
    }

    /// <inheritdoc />
    public async Task<int> GetMessageCountAsync(ConversationId conversationId, CancellationToken ct = default)
    {
        var conversation = await _context.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId, ct);

        return conversation?.GetMessageCount() ?? 0;
    }

    /// <inheritdoc />
    public async Task<DeletedMessageInfo?> GetDeletedMessageInfoAsync(ConversationId conversationId, MessageId messageId, CancellationToken ct = default)
    {
        var conversation = await _context.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId, ct);

        var message = conversation?.GetAllMessages()
            .FirstOrDefault(m => m.Id == messageId);

        if (message == null)
            return null;

        return new DeletedMessageInfo(message.Id, message.IsDeleted);
    }

    /// <inheritdoc />
    public async Task<bool> AiResponseIdExistsAsync(AiResponseId aiResponseId, CancellationToken ct = default)
    {
        // Check across all conversations for the AI response ID
        var conversations = await _context.Conversations
            .AsNoTracking()
            .ToListAsync(ct);

        return conversations.Any(c =>
            c.GetAllMessages().Any(m => m.AiResponseId != null && m.AiResponseId == aiResponseId));
    }

    /// <inheritdoc />
    public async Task<List<Message>> GetMessagesByRoleAsync(ConversationId conversationId, MessageRole role, CancellationToken ct = default)
    {
        var conversation = await _context.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId, ct);

        if (conversation == null)
            return new List<Message>();

        return conversation.GetAllMessages()
            .Where(m => m.Role == role)
            .OrderBy(m => m.Sequence)
            .ToList();
    }
}