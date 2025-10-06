using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.Entities;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Modules.Chat.Infrastructure.Persistence.Builders;
using BuildingBlocks.Primitives.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;

/// <summary>
/// Tests for ChatDbContext configuration.
/// Validates EF Core configuration, owned entities, indexes, and audit tracking.
/// </summary>
[TestFixture]
public class ChatDbContextTests : ChatPersistenceTestBase
{
    #region Owned Entity Configuration

    [Test]
    public async Task OwnedEntity_Messages_ConfiguredCorrectly()
    {
        await Task.CompletedTask;

        // Arrange: Get entity type configuration
        var conversationType = DbContext.Model.FindEntityType(typeof(Conversation));
        conversationType.ShouldNotBeNull();

        var messageType = DbContext.Model.FindEntityType(typeof(Message));
        messageType.ShouldNotBeNull();

        // Assert: Messages should be configured as owned
        messageType.IsOwned().ShouldBeTrue("Messages should be configured as owned entities");

        // Assert: Should have composite key (ConversationId, Id)
        var primaryKey = messageType.FindPrimaryKey();
        primaryKey.ShouldNotBeNull();
        primaryKey.Properties.Count.ShouldBe(2, "Messages should have composite key");

        var keyPropertyNames = primaryKey.Properties.Select(p => p.Name).ToList();
        keyPropertyNames.ShouldContain("ConversationId");
        keyPropertyNames.ShouldContain("Id");

        // Assert: Should have foreign key to Conversation
        var foreignKeys = messageType.GetForeignKeys();
        var conversationFk = foreignKeys.FirstOrDefault(fk =>
            fk.PrincipalEntityType == conversationType);
        conversationFk.ShouldNotBeNull("Messages should have FK to Conversation");

        // Assert: Delete behavior should be Cascade
        conversationFk.DeleteBehavior.ShouldBe(DeleteBehavior.Cascade,
            "Messages should cascade delete with Conversation");
    }

    [Test]
    public async Task OwnedEntity_Messages_NotDirectlyAccessible()
    {
        await Task.CompletedTask;

        // Assert: There should be no DbSet<Message> property
        var dbSetProperties = DbContext.GetType()
            .GetProperties()
            .Where(p => p.PropertyType.IsGenericType &&
                       p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .ToList();

        // Should only have Conversations DbSet
        dbSetProperties.Count.ShouldBe(1);
        dbSetProperties[0].Name.ShouldBe("Conversations");

        // Messages are accessed through Conversation aggregate only
        var messageDbSet = dbSetProperties.FirstOrDefault(p =>
            p.PropertyType == typeof(DbSet<Message>));
        messageDbSet.ShouldBeNull("Messages should not have a direct DbSet");
    }

    #endregion

    #region Audit Timestamps with TimeProvider

    [Test]
    public async Task AuditTimestamps_UseTimeProvider_NotSystemTime()
    {
        // Arrange: Set specific time
        var testTime = new DateTimeOffset(2024, 6, 15, 10, 30, 0, TimeSpan.Zero);
        TimeProvider.SetUtcNow(testTime);

        // Act: Create and save conversation
        var conversation = CreateTestConversation(timeProvider: TimeProvider);
        await SaveConversationAsync(conversation);

        // Assert: Timestamps should match TimeProvider time
        var saved = await QueryFreshAsync(() =>
            ConversationWriteRepository.GetByIdAsync(conversation.Id));
        saved.ShouldNotBeNull();

        saved.CreatedAt.ShouldBe(testTime);
        saved.UpdatedAt.ShouldBe(testTime);

        // Advance time and update
        var updateTime = testTime.AddHours(1);
        TimeProvider.SetUtcNow(updateTime);

        saved.UpdateTitle("Updated Title", TimeProvider);
        await ConversationWriteRepository.UpdateAsync(saved);
        await UnitOfWork.SaveChangesAsync();

        // Reload and verify
        var updated = await QueryFreshAsync(() =>
            ConversationWriteRepository.GetByIdAsync(conversation.Id));
        updated.ShouldNotBeNull();

        updated.CreatedAt.ShouldBe(testTime, "CreatedAt should not change");
        updated.UpdatedAt.ShouldBe(updateTime, "UpdatedAt should reflect update time");
    }

    [Test]
    public async Task AuditTimestamps_Messages_UseTimeProvider()
    {
        // Arrange: Set specific time
        var testTime = new DateTimeOffset(2024, 7, 20, 14, 45, 30, TimeSpan.Zero);
        TimeProvider.SetUtcNow(testTime);

        // Act: Create conversation with message
        var conversation = CreateTestConversation(timeProvider: TimeProvider);
        var content = MessageContent.From("Test message");
        conversation.AppendUserMessageToConversation(content, TimeProvider);

        await SaveConversationAsync(conversation);

        // Assert: Message timestamps should match TimeProvider
        var saved = await QueryFreshAsync(() =>
            ConversationWriteRepository.GetByIdAsync(conversation.Id));
        saved.ShouldNotBeNull();

        var message = saved.GetAllMessages().First();
        message.CreatedAt.ShouldBe(testTime);
        message.UpdatedAt.ShouldBe(testTime);
    }

    #endregion

    #region Schema and Table Names

    [Test]
    public async Task Schema_TablesInChatSchema_Verified()
    {
        await Task.CompletedTask;

        // Arrange: Get entity types
        var conversationType = DbContext.Model.FindEntityType(typeof(Conversation));
        var messageType = DbContext.Model.FindEntityType(typeof(Message));

        // Assert: Correct schema
        var conversationSchema = conversationType?.GetSchema();
        conversationSchema.ShouldBe("chat");

        var messageSchema = messageType?.GetSchema();
        messageSchema.ShouldBe("chat");

        // Assert: Correct table names
        var conversationTable = conversationType?.GetTableName();
        conversationTable.ShouldBe("Conversations");

        var messageTable = messageType?.GetTableName();
        messageTable.ShouldBe("Messages");
    }

    #endregion

    #region Indexes for Performance

    [Test]
    public async Task Indexes_MessageSequence_ConversationId_Exist()
    {
        await Task.CompletedTask;

        // Arrange: Get Message entity type
        var messageType = DbContext.Model.FindEntityType(typeof(Message));
        messageType.ShouldNotBeNull();

        // Assert: Index on ConversationId exists
        var indexes = messageType.GetIndexes().ToList();
        indexes.ShouldNotBeEmpty("Messages should have indexes");

        // Should have index on ConversationId
        var conversationIdIndex = indexes.FirstOrDefault(idx =>
            idx.Properties.Any(p => p.Name == "ConversationId"));
        conversationIdIndex.ShouldNotBeNull("Should have index on ConversationId");

        // Should have composite index on (ConversationId, Sequence)
        var compositeIndex = indexes.FirstOrDefault(idx =>
            idx.Properties.Count == 2 &&
            idx.Properties.Any(p => p.Name == "ConversationId") &&
            idx.Properties.Any(p => p.Name == "Sequence"));
        compositeIndex.ShouldNotBeNull("Should have composite index on (ConversationId, Sequence)");
    }

    #endregion

    #region Value Converters

    [Test]
    public async Task ValueConverters_StrongIds_WorkCorrectly()
    {
        // Arrange: Create conversation with various strong IDs
        var ownerId = AxonUserId.New();
        var conversation = CreateTestConversation(ownerId: ownerId, timeProvider: TimeProvider);

        // Add message with AI response ID
        var userContent = MessageContent.From("User question");
        conversation.AppendUserMessageToConversation(userContent, TimeProvider);

        var assistantContent = MessageContent.From("AI response");
        var aiResponseId = new AiResponseId($"test-{Guid.NewGuid()}");
        conversation.AppendAssistantResponseToConversation(assistantContent, aiResponseId, TimeProvider);

        // Act: Save and reload
        await SaveConversationAsync(conversation);

        var loaded = await QueryFreshAsync(() =>
            ConversationWriteRepository.GetByIdAsync(conversation.Id));

        // Assert: All strong IDs properly converted and restored
        loaded.ShouldNotBeNull();
        loaded.Id.ShouldBe(conversation.Id);
        loaded.OwnerId.ShouldBe(ownerId);
        loaded.LastAiResponseId.ShouldBe(aiResponseId);

        var messages = loaded.GetAllMessages();
        messages.Count.ShouldBe(2);

        // Verify message IDs
        messages[0].Id.ShouldNotBe(default(MessageId));
        messages[1].Id.ShouldNotBe(default(MessageId));

        // Verify AI response ID on assistant message
        var assistantMessage = messages.First(m => m.Role == MessageRole.Assistant);
        assistantMessage.AiResponseId.ShouldBe(aiResponseId);
    }

    [Test]
    public async Task ValueConverters_MessageContent_PreservesData()
    {
        // Arrange: Create messages with various content
        var conversation = CreateTestConversation(timeProvider: TimeProvider);

        var simpleContent = MessageContent.From("Simple text");
        conversation.AppendUserMessageToConversation(simpleContent, TimeProvider);

        var longContent = MessageContent.From(new string('A', 4000)); // Near max length
        var aiResponseId = new AiResponseId(Guid.NewGuid().ToString());
        conversation.AppendAssistantResponseToConversation(longContent, aiResponseId, TimeProvider);

        // Act: Save and reload
        await SaveConversationAsync(conversation);

        var loaded = await QueryFreshAsync(() =>
            ConversationWriteRepository.GetByIdAsync(conversation.Id));

        // Assert: Content preserved exactly
        loaded.ShouldNotBeNull();
        var messages = loaded.GetAllMessages();

        messages[0].Content.ShouldBe(simpleContent);
        messages[1].Content.Value.Length.ShouldBe(4000);
        messages[1].Content.ShouldBe(longContent);
    }

    #endregion

    #region Version Field Configuration

    [Test]
    public async Task Version_Field_ConfiguredAsRowVersion()
    {
        await Task.CompletedTask;

        // Arrange: Get Conversation entity type
        var conversationType = DbContext.Model.FindEntityType(typeof(Conversation));
        conversationType.ShouldNotBeNull();

        // Get Version property
        var versionProperty = conversationType.FindProperty("Version");
        versionProperty.ShouldNotBeNull();

        // Assert: Configured as row version
        versionProperty.IsConcurrencyToken.ShouldBeTrue("Version should be concurrency token");
        versionProperty.ValueGenerated.ShouldBe(ValueGenerated.OnAddOrUpdate,
            "Version should be generated on add or update");

        // Assert: Uses xmin column
        var columnName = versionProperty.GetColumnName();
        columnName.ShouldBe("xmin", "Version should map to PostgreSQL xmin column");

        var columnType = versionProperty.GetColumnType();
        columnType.ShouldBe("xid", "Version should use xid type");
    }

    #endregion

    #region Soft Delete Configuration

    [Test]
    public async Task SoftDelete_IsDeleted_ConfiguredProperly()
    {
        // Arrange: Create and save conversation
        var conversation = CreateTestConversation(timeProvider: TimeProvider);
        await SaveConversationAsync(conversation);

        // Act: Soft delete using domain method
        var loaded = await ConversationWriteRepository.GetByIdAsync(conversation.Id);
        loaded.ShouldNotBeNull();

        // Use the domain method for soft delete
        loaded.SoftDelete();

        await ConversationWriteRepository.UpdateAsync(loaded);
        await UnitOfWork.SaveChangesAsync();

        // Assert: Record still exists but marked as deleted
        var deletedConv = await QueryFreshAsync(() =>
            DbContext.Conversations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == conversation.Id));

        deletedConv.ShouldNotBeNull("Soft deleted record should still exist");
        deletedConv.IsDeleted.ShouldBeTrue();
        deletedConv.DeletedAt.ShouldNotBeNull("DeletedAt should be set");
    }

    #endregion
}