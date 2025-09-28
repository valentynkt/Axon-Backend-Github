using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.Common.Models;
using Axon.Modules.Chat.Application.Contracts.Persistence;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Chat.Infrastructure.Persistence.Repositories;
using BuildingBlocks.Application;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Infrastructure.Persistence.Write;
using BuildingBlocks.Primitives.Ids;
using BuildingBlocks.Testing;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Chat.Infrastructure.Tests;

/// <summary>
/// Concurrency tests for Conversation aggregate.
/// Tests optimistic concurrency control in real-world scenarios like
/// concurrent message appending, status updates, and title changes.
/// </summary>
[TestFixture]
public class ConversationConcurrencyTests : ConcurrencyTestBase<ChatDbContext>
{
    private ChatDbContext _setupContext = null!;
    private FakeTimeProvider _timeProvider = null!;
    private EfUnitOfWork<ChatDbContext, ChatModule> _unitOfWork = null!;

    [SetUp]
    public async Task SetUp()
    {
        _timeProvider = new FakeTimeProvider();
        _timeProvider.SetUtcNow(DateTimeOffset.UtcNow);

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
            .LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information)
            .EnableSensitiveDataLogging()
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

    #region Critical Concurrency Scenarios

    [Test]
    public async Task UpdateTitle_ConcurrentModifications_ShouldThrowConcurrencyException()
    {
        // Arrange - Create and save a conversation
        var conversation = await CreateAndSaveConversationAsync();

        // Act & Assert - Simulate concurrent title updates
        var result = await SimulateConcurrentUpdatesAsync<Conversation, ConversationId>(
            conversation.Id,
            context =>
            {
                var unitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(context);
                return new ConversationRepository(context, unitOfWork);
            },
            conv1 => conv1.UpdateTitle("Title from User 1", _timeProvider),
            conv2 => conv2.UpdateTitle("Title from User 2", _timeProvider)
        );

        AssertOptimisticConcurrencyHandled(result);

        // Verify the first update was persisted
        using var verifyContext = CreateContext(CreateContextOptions());
        using var verifyUnitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(verifyContext);
        using var verifyRepo = new ConversationRepository(verifyContext, verifyUnitOfWork);
        var updated = await verifyRepo.GetByIdAsync(conversation.Id);
        updated.ShouldNotBeNull();
        updated.Title?.ShouldBe("Title from User 1");
    }

    [Test]
    public async Task AppendMessages_ConcurrentUsers_ShouldThrowConcurrencyException()
    {
        // Arrange
        var conversation = await CreateAndSaveConversationAsync();

        // Act & Assert - Two users trying to append messages simultaneously
        var result = await SimulateConcurrentUpdatesAsync<Conversation, ConversationId>(
            conversation.Id,
            context =>
            {
                var unitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(context);
                return new ConversationRepository(context, unitOfWork);
            },
            conv1 =>
            {
                var content = MessageContent.From("Message from User 1");
                return conv1.AppendUserMessageToConversation(content, _timeProvider);
            },
            conv2 =>
            {
                var content = MessageContent.From("Message from User 2");
                return conv2.AppendUserMessageToConversation(content, _timeProvider);
            }
        );

        AssertOptimisticConcurrencyHandled(result);
    }

    [Test]
    public async Task UpdateTitle_WhileAppendingMessage_ShouldThrowConcurrencyException()
    {
        // Arrange - Critical scenario: Admin updates title while user sends message
        var conversation = await CreateAndSaveConversationAsync();

        // Act & Assert
        var result = await SimulateConcurrentUpdatesAsync<Conversation, ConversationId>(
            conversation.Id,
            context =>
            {
                var unitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(context);
                return new ConversationRepository(context, unitOfWork);
            },
            conv1 => conv1.UpdateTitle("Admin Updated Title", _timeProvider),
            conv2 =>
            {
                var content = MessageContent.From("User is typing a message");
                return conv2.AppendUserMessageToConversation(content, _timeProvider);
            }
        );

        AssertOptimisticConcurrencyHandled(result);
    }

    [Test]
    public async Task MarkCompleted_WhileUserUpdating_ShouldThrowConcurrencyException()
    {
        // Arrange - System tries to auto-complete while user is still active
        var conversation = await CreateAndSaveConversationWithMessagesAsync();

        // Act & Assert
        var result = await SimulateConcurrentUpdatesAsync<Conversation, ConversationId>(
            conversation.Id,
            context =>
            {
                var unitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(context);
                return new ConversationRepository(context, unitOfWork);
            },
            conv1 => conv1.Complete(_timeProvider),
            conv2 => conv2.UpdateTitle("User still editing", _timeProvider)
        );

        AssertOptimisticConcurrencyHandled(result);
    }

    [Test]
    public async Task AppendAssistantMessage_ConcurrentResponses_ShouldThrowConcurrencyException()
    {
        // Arrange - Multiple AI services trying to append responses
        var conversation = await CreateAndSaveConversationWithUserMessageAsync();

        // Act & Assert
        var result = await SimulateConcurrentUpdatesAsync<Conversation, ConversationId>(
            conversation.Id,
            context =>
            {
                var unitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(context);
                return new ConversationRepository(context, unitOfWork);
            },
            conv1 =>
            {
                var content = MessageContent.From("AI Response 1");
                var responseId = new AiResponseId(Guid.NewGuid().ToString());
                return conv1.AppendAssistantResponseToConversation(content, responseId, _timeProvider);
            },
            conv2 =>
            {
                var content = MessageContent.From("AI Response 2");
                var responseId = new AiResponseId(Guid.NewGuid().ToString());
                return conv2.AppendAssistantResponseToConversation(content, responseId, _timeProvider);
            }
        );

        AssertOptimisticConcurrencyHandled(result);
    }

    [Test]
    public async Task DetachedConversation_ConcurrentUpdates_ShouldThrowConcurrencyException()
    {
        // Arrange - Test with detached entities (common in web scenarios)
        var conversation = await CreateAndSaveConversationAsync();

        // Act & Assert
        var result = await SimulateConcurrentDetachedUpdatesAsync<Conversation, ConversationId>(
            conversation.Id,
            context =>
            {
                var unitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(context);
                return new ConversationRepository(context, unitOfWork);
            },
            conv1 => conv1.UpdateTitle("Detached Update 1", _timeProvider),
            conv2 => conv2.UpdateTitle("Detached Update 2", _timeProvider)
        );

        AssertOptimisticConcurrencyHandled(result);
    }

    [Test]
    public async Task RapidMessageAppending_ShouldMaintainSequenceIntegrity()
    {
        // Arrange
        var conversation = await CreateAndSaveConversationAsync();
        var conversationId = conversation.Id;

        // Create a stale version BEFORE we do the rapid updates
        var staleConversation = await CreateStaleConversationInstanceAsync(conversationId);
        var staleVersion = staleConversation.Version;

        // Act - Simulate rapid-fire messages from same user (should succeed sequentially)
        using var context = CreateContext(CreateContextOptions());
        using var unitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(context);
        using var repo = new ConversationRepository(context, unitOfWork);

        var loaded = await repo.GetByIdAsync(conversationId);
        loaded.ShouldNotBeNull();

        for (int i = 1; i <= 5; i++)
        {
            // Add user message
            var userContent = MessageContent.From($"Rapid message {i}");
            var userResult = loaded.AppendUserMessageToConversation(userContent, _timeProvider);
            userResult.IsSuccess.ShouldBeTrue($"Failed to append user message {i}");

            await repo.UpdateAsync(loaded);
            await context.SaveChangesAsync();

            // Advance time slightly
            _timeProvider.Advance(TimeSpan.FromMilliseconds(50));

            // Add assistant response to maintain alternating pattern
            var assistantContent = MessageContent.From($"Rapid response {i}");
            var responseId = new AiResponseId(Guid.NewGuid().ToString());
            var assistantResult = loaded.AppendAssistantResponseToConversation(assistantContent, responseId, _timeProvider);
            assistantResult.IsSuccess.ShouldBeTrue($"Failed to append assistant message {i}");

            await repo.UpdateAsync(loaded);
            await context.SaveChangesAsync();

            // Advance time slightly for next iteration
            _timeProvider.Advance(TimeSpan.FromMilliseconds(50));
        }

        // Assert - All messages should be in correct sequence (10 total: 5 user + 5 assistant)
        loaded.GetMessageCount().ShouldBe(10);
        var messages = loaded.GetAllMessages().ToList();
        for (int i = 0; i < messages.Count; i++)
        {
            messages[i].Sequence.ShouldBe(i + 1);
        }

        // Now try concurrent update with the stale version we created earlier - should fail
        // staleConversation still has the original version from before all the updates
        staleConversation.Version.ShouldBe(staleVersion);

        using var staleContext = CreateContext(CreateContextOptions());
        using var staleUnitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(staleContext);
        using var staleRepo = new ConversationRepository(staleContext, staleUnitOfWork);

        await staleRepo.UpdateAsync(staleConversation);

        try
        {
            await staleContext.SaveChangesAsync();
            Assert.Fail("Expected concurrency exception but save succeeded");
        }
        catch (global::BuildingBlocks.Core.Diagnostics.Exceptions.ConcurrencyException)
        {
            // Expected - test passes
        }
        catch (Exception ex)
        {
            TestContext.Out.WriteLine($"Unexpected exception in RapidMessageAppending: {ex.GetType().FullName}");
            TestContext.Out.WriteLine($"Message: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region Helper Methods

    private async Task<Conversation> CreateAndSaveConversationAsync()
    {
        var ownerId = AxonUserId.New();
        var conversationResult = Conversation.StartNewConversation(ownerId, null, _timeProvider);
        conversationResult.IsSuccess.ShouldBeTrue();

        var conversation = conversationResult.Value;
        // Use the existing unitOfWork and don't dispose it here
        using var repository = new ConversationRepository(_setupContext, _unitOfWork);
        await repository.AddAsync(conversation);
        await _setupContext.SaveChangesAsync();

        return conversation;
    }

    private async Task<Conversation> CreateAndSaveConversationWithUserMessageAsync()
    {
        var conversation = await CreateAndSaveConversationAsync();

        // Add a user message
        var content = MessageContent.From("Initial user message");
        var result = conversation.AppendUserMessageToConversation(content, _timeProvider);
        result.IsSuccess.ShouldBeTrue();

        // Use the existing unitOfWork and don't dispose it here
        using (var repository = new ConversationRepository(_setupContext, _unitOfWork))
        {
            await repository.UpdateAsync(conversation);
            await _setupContext.SaveChangesAsync();
        }

        return conversation;
    }

    private async Task<Conversation> CreateAndSaveConversationWithMessagesAsync()
    {
        var conversation = await CreateAndSaveConversationWithUserMessageAsync();

        // Add assistant message
        var assistantContent = MessageContent.From("Assistant response");
        var responseId = new AiResponseId(Guid.NewGuid().ToString());
        var assistantResult = conversation.AppendAssistantResponseToConversation(
            assistantContent, responseId, _timeProvider);
        assistantResult.IsSuccess.ShouldBeTrue();

        // Use the existing unitOfWork and don't dispose it here
        using (var repository = new ConversationRepository(_setupContext, _unitOfWork))
        {
            await repository.UpdateAsync(conversation);
            await _setupContext.SaveChangesAsync();

            // Add another user/assistant pair to make it more conversational
            for (int i = 0; i < 2; i++)
            {
                // Add user message
                var userContent = MessageContent.From($"Follow-up question {i}");
                var userResult = conversation.AppendUserMessageToConversation(userContent, _timeProvider);
                userResult.IsSuccess.ShouldBeTrue($"Failed to append user message {i} in CreateAndSaveConversationWithMessagesAsync");

                await repository.UpdateAsync(conversation);
                await _setupContext.SaveChangesAsync();

                // Add assistant response to maintain alternating pattern
                var assistantContent2 = MessageContent.From($"Follow-up response {i}");
                var responseId2 = new AiResponseId(Guid.NewGuid().ToString());
                var assistantResult2 = conversation.AppendAssistantResponseToConversation(
                    assistantContent2, responseId2, _timeProvider);
                assistantResult2.IsSuccess.ShouldBeTrue($"Failed to append assistant message {i} in CreateAndSaveConversationWithMessagesAsync");

                await repository.UpdateAsync(conversation);
                await _setupContext.SaveChangesAsync();
            }
        }

        return conversation;
    }

    private async Task<Conversation> CreateStaleConversationInstanceAsync(ConversationId id)
    {
        // Load conversation in a temporary context and immediately detach
        using var tempContext = CreateContext(CreateContextOptions());
        using var tempUnitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(tempContext);
        using var tempRepo = new ConversationRepository(tempContext, tempUnitOfWork);

        var loaded = await tempRepo.GetByIdAsync(id);
        loaded.ShouldNotBeNull();

        // Detach to simulate a stale instance
        tempContext.Entry(loaded).State = EntityState.Detached;

        // Make a change without saving (simulating stale client state)
        loaded.UpdateTitle("Stale update attempt", _timeProvider);

        return loaded;
    }

    #endregion
}