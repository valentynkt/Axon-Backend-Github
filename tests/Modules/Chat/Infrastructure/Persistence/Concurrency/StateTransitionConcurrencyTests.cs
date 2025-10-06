using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.Common.Models;
using Axon.Modules.Chat.Application.Contracts.Persistence;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Chat.Infrastructure.Persistence.Repositories;
using BuildingBlocks.Infrastructure.Persistence.Write;
using BuildingBlocks.Primitives.Ids;
using BuildingBlocks.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Concurrency;

/// <summary>
/// Concurrency tests for conversation state transitions.
/// Tests scenarios where status changes, title updates, and completion operations
/// happen concurrently with other operations.
/// </summary>
[TestFixture]
public class StateTransitionConcurrencyTests : ConcurrencyTestBase<ChatDbContext>
{
    private ChatDbContext _setupContext = null!;
    private FakeTimeProvider _timeProvider = null!;
    private EfUnitOfWork<ChatDbContext, ChatModule> _unitOfWork = null!;

    [SetUp]
    public async Task SetUp()
    {
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));

        _setupContext = CreateContext(CreateContextOptions());
        _unitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(_setupContext);

        await _setupContext.Database.EnsureDeletedAsync();
        await _setupContext.Database.MigrateAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        _unitOfWork?.Dispose();
        await _setupContext.DisposeAsync();
    }

    protected override DbContextOptions<ChatDbContext> CreateContextOptions()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddEntityFrameworkNpgsql();
        var serviceProvider = serviceCollection.BuildServiceProvider();

        return CreateDbContextOptionsBuilder<ChatDbContext>()
            .UseNpgsql(ConnectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(ChatDbContext).Assembly.FullName);
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "chat");
            })
            .UseInternalServiceProvider(serviceProvider)
            .Options;
    }

    protected override ChatDbContext CreateContext(DbContextOptions<ChatDbContext> options)
    {
        return new ChatDbContext(options, _timeProvider);
    }

    protected override Task<TId> CreateAndSaveTestAggregateAsync<TAggregate, TId>()
    {
        throw new NotImplementedException("Use specific test conversation creation methods");
    }

    #region Critical: Concurrent Completion

    [Test]
    public async Task CompleteConversation_Concurrent_OnlyOneSucceeds()
    {
        // Arrange: Create conversation with messages (required for completion)
        var conversation = await CreateAndSaveConversationWithMessagesAsync();

        // Act: Two processes trying to complete the conversation simultaneously
        var result = await SimulateConcurrentUpdatesAsync<Conversation, ConversationId>(
            conversation.Id,
            context =>
            {
                var unitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(context);
                return new ConversationWriteRepository(context, unitOfWork);
            },
            conv1 => conv1.Complete(_timeProvider),
            conv2 => conv2.Complete(_timeProvider)
        );

        // Assert: Second completion should fail
        AssertOptimisticConcurrencyHandled(result);

        // Verify conversation is completed
        using var verifyContext = CreateContext(CreateContextOptions());
        using var verifyUnitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(verifyContext);
        using var verifyRepo = new ConversationWriteRepository(verifyContext, verifyUnitOfWork);

        var updated = await verifyRepo.GetByIdAsync(conversation.Id);
        updated.ShouldNotBeNull();
        updated.Status.ShouldBe(ConversationStatus.Completed);
    }

    [Test]
    public async Task CompleteConversation_WhileAddingMessage_ShouldConflict()
    {
        // Arrange: Create conversation with initial messages
        var conversation = await CreateAndSaveConversationWithMessagesAsync();

        // Act: Complete while user is adding a message
        var result = await SimulateConcurrentUpdatesAsync<Conversation, ConversationId>(
            conversation.Id,
            context =>
            {
                var unitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(context);
                return new ConversationWriteRepository(context, unitOfWork);
            },
            conv1 => conv1.Complete(_timeProvider),
            conv2 =>
            {
                var content = MessageContent.From("Wait, one more thing!");
                return conv2.AppendUserMessageToConversation(content, _timeProvider);
            }
        );

        // Assert: One should fail
        AssertOptimisticConcurrencyHandled(result);
    }

    #endregion

    #region Critical: Title Updates During Operations

    [Test]
    public async Task UpdateTitle_DuringMessageAppend_HandledCorrectly()
    {
        // Arrange: Create conversation with user message
        var conversation = await CreateAndSaveConversationWithUserMessageAsync();

        // Act: Admin updates title while user appends message
        var result = await SimulateConcurrentUpdatesAsync<Conversation, ConversationId>(
            conversation.Id,
            context =>
            {
                var unitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(context);
                return new ConversationWriteRepository(context, unitOfWork);
            },
            conv1 => conv1.UpdateTitle("Admin: Better Title", _timeProvider),
            conv2 =>
            {
                var content = MessageContent.From("Assistant response to user");
                var responseId = new AiResponseId(Guid.NewGuid().ToString());
                return conv2.AppendAssistantResponseToConversation(content, responseId, _timeProvider);
            }
        );

        // Assert: One should fail due to concurrency
        AssertOptimisticConcurrencyHandled(result);

        // Verify only one change was applied
        using var verifyContext = CreateContext(CreateContextOptions());
        using var verifyUnitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(verifyContext);
        using var verifyRepo = new ConversationWriteRepository(verifyContext, verifyUnitOfWork);

        var updated = await verifyRepo.GetByIdAsync(conversation.Id);
        updated.ShouldNotBeNull();

        // Either title was updated OR message was added, not both
        var titleUpdated = updated.Title == "Admin: Better Title";
        var messageAdded = updated.GetMessagesByRole(MessageRole.Assistant).Any();

        (titleUpdated ^ messageAdded).ShouldBeTrue("Exactly one operation should have succeeded");
    }

    [Test]
    public async Task UpdateTitle_ConcurrentTitleChanges_OnlyOneSucceeds()
    {
        // Arrange: Create conversation
        var conversation = await CreateAndSaveConversationAsync();

        // Act: Two admins updating title simultaneously
        var result = await SimulateConcurrentUpdatesAsync<Conversation, ConversationId>(
            conversation.Id,
            context =>
            {
                var unitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(context);
                return new ConversationWriteRepository(context, unitOfWork);
            },
            conv1 => conv1.UpdateTitle("Admin 1: Technical Discussion", _timeProvider),
            conv2 => conv2.UpdateTitle("Admin 2: Customer Support", _timeProvider)
        );

        // Assert: Second update should fail
        AssertOptimisticConcurrencyHandled(result);

        // Verify first title was applied
        using var verifyContext = CreateContext(CreateContextOptions());
        using var verifyUnitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(verifyContext);
        using var verifyRepo = new ConversationWriteRepository(verifyContext, verifyUnitOfWork);

        var updated = await verifyRepo.GetByIdAsync(conversation.Id);
        updated.ShouldNotBeNull();
        updated.Title.ShouldBe("Admin 1: Technical Discussion");
    }

    #endregion

    #region Edge Case: Archive Operations

    [Test]
    public async Task ArchiveConversation_WhileActive_ThrowsConcurrency()
    {
        // Arrange: Create active conversation with messages
        var conversation = await CreateAndSaveConversationWithMessagesAsync();

        // Act: Archive while user is still chatting
        var result = await SimulateConcurrentUpdatesAsync<Conversation, ConversationId>(
            conversation.Id,
            context =>
            {
                var unitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(context);
                return new ConversationWriteRepository(context, unitOfWork);
            },
            conv1 =>
            {
                // Simulate archive by completing the conversation
                return conv1.Complete(_timeProvider);
            },
            conv2 =>
            {
                // User continues chatting
                var content = MessageContent.From("Are you still there?");
                return conv2.AppendUserMessageToConversation(content, _timeProvider);
            }
        );

        // Assert: Operations should conflict
        AssertOptimisticConcurrencyHandled(result);
    }

    [Test]
    public async Task MultipleStateTransitions_RapidSuccession_MaintainIntegrity()
    {
        // Arrange: Create conversation
        var conversation = await CreateAndSaveConversationAsync();
        var conversationId = conversation.Id;

        // Prepare multiple state change operations
        var tasks = new List<Task<(string Operation, bool Success)>>();

        // Title update task
        tasks.Add(Task.Run(async () =>
        {
            using var context = CreateContext(CreateContextOptions());
            using var unitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(context);
            using var repo = new ConversationWriteRepository(context, unitOfWork);

            try
            {
                var conv = await repo.GetByIdAsync(conversationId);
                if (conv == null) return ("TitleUpdate", false);

                conv.UpdateTitle("Rapid Title Change", _timeProvider);
                await repo.UpdateAsync(conv);
                await context.SaveChangesAsync();
                return ("TitleUpdate", true);
            }
            catch
            {
                return ("TitleUpdate", false);
            }
        }));

        // Message append task
        tasks.Add(Task.Run(async () =>
        {
            using var context = CreateContext(CreateContextOptions());
            using var unitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(context);
            using var repo = new ConversationWriteRepository(context, unitOfWork);

            try
            {
                var conv = await repo.GetByIdAsync(conversationId);
                if (conv == null) return ("MessageAppend", false);

                var content = MessageContent.From("Rapid message");
                conv.AppendUserMessageToConversation(content, _timeProvider);
                await repo.UpdateAsync(conv);
                await context.SaveChangesAsync();
                return ("MessageAppend", true);
            }
            catch
            {
                return ("MessageAppend", false);
            }
        }));

        // Another title update task
        tasks.Add(Task.Run(async () =>
        {
            await Task.Delay(10); // Small delay to ensure different timing
            using var context = CreateContext(CreateContextOptions());
            using var unitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(context);
            using var repo = new ConversationWriteRepository(context, unitOfWork);

            try
            {
                var conv = await repo.GetByIdAsync(conversationId);
                if (conv == null) return ("TitleUpdate2", false);

                conv.UpdateTitle("Another Title", _timeProvider);
                await repo.UpdateAsync(conv);
                await context.SaveChangesAsync();
                return ("TitleUpdate2", true);
            }
            catch
            {
                return ("TitleUpdate2", false);
            }
        }));

        // Act: Execute all operations concurrently
        var results = await Task.WhenAll(tasks);

        // Assert: At least one should succeed
        var successfulOps = results.Where(r => r.Success).ToList();
        successfulOps.Count.ShouldBeGreaterThan(0, "At least one operation should succeed");

        // Log results for debugging
        foreach (var (operation, success) in results)
        {
            TestContext.Out.WriteLine($"Operation {operation}: {(success ? "Succeeded" : "Failed")}");
        }

        // Verify final state integrity
        using var verifyContext = CreateContext(CreateContextOptions());
        using var verifyUnitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(verifyContext);
        using var verifyRepo = new ConversationWriteRepository(verifyContext, verifyUnitOfWork);

        var finalState = await verifyRepo.GetByIdAsync(conversationId);
        finalState.ShouldNotBeNull();

        // Verify aggregate is in valid state
        finalState.Status.ShouldBeOneOf(ConversationStatus.Active, ConversationStatus.Completed);

        // If messages were added, verify sequence integrity
        var messages = finalState.GetAllMessages();
        for (int i = 0; i < messages.Count; i++)
        {
            messages[i].Sequence.ShouldBe(i + 1);
        }
    }

    #endregion

    #region Helper Methods

    private async Task<Conversation> CreateAndSaveConversationAsync()
    {
        var ownerId = AxonUserId.New();
        var conversationResult = Conversation.StartNewConversation(ownerId, "Test Conversation", _timeProvider);
        conversationResult.IsSuccess.ShouldBeTrue();

        var conversation = conversationResult.Value;
        using var repository = new ConversationWriteRepository(_setupContext, _unitOfWork);
        await repository.AddAsync(conversation);
        await _setupContext.SaveChangesAsync();

        return conversation;
    }

    private async Task<Conversation> CreateAndSaveConversationWithUserMessageAsync()
    {
        var conversation = await CreateAndSaveConversationAsync();

        var content = MessageContent.From("User message");
        var result = conversation.AppendUserMessageToConversation(content, _timeProvider);
        result.IsSuccess.ShouldBeTrue();

        using var repository = new ConversationWriteRepository(_setupContext, _unitOfWork);
        await repository.UpdateAsync(conversation);
        await _setupContext.SaveChangesAsync();

        return conversation;
    }

    private async Task<Conversation> CreateAndSaveConversationWithMessagesAsync()
    {
        var conversation = await CreateAndSaveConversationWithUserMessageAsync();

        // Add assistant response
        var assistantContent = MessageContent.From("Assistant response");
        var responseId = new AiResponseId(Guid.NewGuid().ToString());
        var assistantResult = conversation.AppendAssistantResponseToConversation(
            assistantContent, responseId, _timeProvider);
        assistantResult.IsSuccess.ShouldBeTrue();

        // Add another user message for completion eligibility
        var userContent2 = MessageContent.From("Follow-up question");
        conversation.AppendUserMessageToConversation(userContent2, _timeProvider);

        var assistantContent2 = MessageContent.From("Follow-up response");
        var responseId2 = new AiResponseId(Guid.NewGuid().ToString());
        conversation.AppendAssistantResponseToConversation(assistantContent2, responseId2, _timeProvider);

        using var repository = new ConversationWriteRepository(_setupContext, _unitOfWork);
        await repository.UpdateAsync(conversation);
        await _setupContext.SaveChangesAsync();

        return conversation;
    }

    #endregion
}