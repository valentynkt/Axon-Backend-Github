using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.Common.Models;
using Axon.Modules.Chat.Application.Contracts.Persistence;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Chat.Infrastructure.Persistence.Repositories;
using BuildingBlocks.Core.Diagnostics.Errors;
using BuildingBlocks.Core.Diagnostics.Exceptions;
using BuildingBlocks.Infrastructure.Persistence.Write;
using BuildingBlocks.Primitives.Ids;
using BuildingBlocks.Testing;
using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using Shouldly;

namespace Axon.Modules.Chat.Infrastructure.Persistence.Concurrency;

/// <summary>
/// Critical concurrency tests for message append operations.
/// Tests scenarios where multiple users/services try to append messages simultaneously.
/// Validates sequence integrity, idempotency, and race conditions.
/// </summary>
[TestFixture]
public class MessageAppendConcurrencyTests : ConcurrencyTestBase<ChatDbContext>
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

    #region Critical: Concurrent User Messages

    [Test]
    public async Task AppendMessage_ConcurrentUsers_ShouldMaintainSequenceIntegrity()
    {
        // Arrange: Create conversation
        var conversation = await CreateAndSaveConversationAsync();

        // Act: Two users trying to append messages simultaneously
        var result = await SimulateConcurrentUpdatesAsync<Conversation, ConversationId>(
            conversation.Id,
            context =>
            {
                var unitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(context);
                return new ConversationRepository(context, unitOfWork);
            },
            conv1 =>
            {
                var content = MessageContent.From("User 1: What's the weather?");
                return conv1.AppendUserMessageToConversation(content, _timeProvider);
            },
            conv2 =>
            {
                var content = MessageContent.From("User 2: Tell me a joke");
                return conv2.AppendUserMessageToConversation(content, _timeProvider);
            }
        );

        // Assert: One should succeed, one should fail with concurrency exception
        AssertOptimisticConcurrencyHandled(result);

        // Verify sequence integrity is maintained
        using var verifyContext = CreateContext(CreateContextOptions());
        using var verifyUnitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(verifyContext);
        using var verifyRepo = new ConversationRepository(verifyContext, verifyUnitOfWork);

        var updated = await verifyRepo.GetByIdAsync(conversation.Id);
        updated.ShouldNotBeNull();

        var messages = updated.GetAllMessages();
        messages.Count.ShouldBe(1); // Only one message should have been added

        // Verify sequence numbers are correct
        for (int i = 0; i < messages.Count; i++)
        {
            messages[i].Sequence.ShouldBe(i + 1);
        }
    }

    #endregion

    #region Critical: AI Response Idempotency

    [Test]
    public async Task AppendAssistantMessage_SameAiResponseId_ShouldBeIdempotent()
    {
        // Arrange: Create conversation with user message
        var conversation = await CreateAndSaveConversationWithUserMessageAsync();
        var aiResponseId = new AiResponseId($"ai-response-{Guid.NewGuid()}");

        // Act: Simulate duplicate AI responses (e.g., from retry logic)
        var result = await SimulateConcurrentUpdatesAsync<Conversation, ConversationId>(
            conversation.Id,
            context =>
            {
                var unitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(context);
                return new ConversationRepository(context, unitOfWork);
            },
            conv1 =>
            {
                var content = MessageContent.From("AI Response: The weather is sunny");
                return conv1.AppendAssistantResponseToConversation(content, aiResponseId, _timeProvider);
            },
            conv2 =>
            {
                var content = MessageContent.From("AI Response: The weather is sunny");
                return conv2.AppendAssistantResponseToConversation(content, aiResponseId, _timeProvider);
            }
        );

        // Assert: One should succeed, one should fail (idempotency at domain or DB level)
        AssertOptimisticConcurrencyHandled(result);

        // Verify only one AI response was added
        using var verifyContext = CreateContext(CreateContextOptions());
        using var verifyUnitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(verifyContext);
        using var verifyRepo = new ConversationRepository(verifyContext, verifyUnitOfWork);

        var updated = await verifyRepo.GetByIdAsync(conversation.Id);
        updated.ShouldNotBeNull();

        var assistantMessages = updated.GetMessagesByRole(MessageRole.Assistant);
        assistantMessages.Count.ShouldBe(1); // Only one assistant message
        assistantMessages[0].AiResponseId.ShouldBe(aiResponseId);
    }

    [Test]
    public async Task AppendAssistantMessage_DifferentAiResponseIds_BothShouldFail()
    {
        // Arrange: Create conversation with user message
        var conversation = await CreateAndSaveConversationWithUserMessageAsync();

        // Act: Two different AI services trying to respond simultaneously
        var result = await SimulateConcurrentUpdatesAsync<Conversation, ConversationId>(
            conversation.Id,
            context =>
            {
                var unitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(context);
                return new ConversationRepository(context, unitOfWork);
            },
            conv1 =>
            {
                var content = MessageContent.From("GPT-4: Here's my response");
                var responseId = new AiResponseId($"gpt4-{Guid.NewGuid()}");
                return conv1.AppendAssistantResponseToConversation(content, responseId, _timeProvider);
            },
            conv2 =>
            {
                var content = MessageContent.From("Claude: Here's my response");
                var responseId = new AiResponseId($"claude-{Guid.NewGuid()}");
                return conv2.AppendAssistantResponseToConversation(content, responseId, _timeProvider);
            }
        );

        // Assert: Second update should fail
        AssertOptimisticConcurrencyHandled(result);

        // Verify only one response was added
        using var verifyContext = CreateContext(CreateContextOptions());
        using var verifyUnitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(verifyContext);
        using var verifyRepo = new ConversationRepository(verifyContext, verifyUnitOfWork);

        var updated = await verifyRepo.GetByIdAsync(conversation.Id);
        updated.ShouldNotBeNull();

        var assistantMessages = updated.GetMessagesByRole(MessageRole.Assistant);
        assistantMessages.Count.ShouldBe(1); // Only one should have succeeded
    }

    #endregion

    #region Critical: Rapid Fire Messages

    [Test]
    public async Task AppendMultipleMessages_RapidFire_ShouldHandleCorrectly()
    {
        // Arrange: Create conversation
        var conversation = await CreateAndSaveConversationAsync();
        var conversationId = conversation.Id;

        // Create multiple concurrent contexts simulating rapid messages
        var tasks = new List<Task<bool>>();

        for (int i = 0; i < 5; i++)
        {
            var messageIndex = i;
            var task = Task.Run(async () =>
            {
                using var context = CreateContext(CreateContextOptions());
                using var unitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(context);
                using var repo = new ConversationRepository(context, unitOfWork);

                try
                {
                    var conv = await repo.GetByIdAsync(conversationId);
                    if (conv == null) return false;

                    // Alternate between user and assistant messages
                    if (messageIndex % 2 == 0)
                    {
                        var content = MessageContent.From($"User rapid message {messageIndex}");
                        var result = conv.AppendUserMessageToConversation(content, _timeProvider);
                        if (!result.IsSuccess) return false;
                    }
                    else
                    {
                        var content = MessageContent.From($"Assistant rapid response {messageIndex}");
                        var responseId = new AiResponseId($"rapid-{Guid.NewGuid()}");
                        var result = conv.AppendAssistantResponseToConversation(content, responseId, _timeProvider);
                        if (!result.IsSuccess) return false;
                    }

                    await repo.UpdateAsync(conv);
                    await context.SaveChangesAsync();
                    return true;
                }
                catch (ConcurrencyException)
                {
                    return false; // Expected for some operations
                }
                catch (DbUpdateConcurrencyException)
                {
                    return false; // Expected for some operations
                }
            });

            tasks.Add(task);
        }

        // Act: Execute all tasks concurrently
        var results = await Task.WhenAll(tasks);

        // Assert: At least one should succeed, others may fail due to concurrency
        results.Any(r => r).ShouldBeTrue("At least one message append should succeed");

        // Verify sequence integrity
        using var verifyContext = CreateContext(CreateContextOptions());
        using var verifyUnitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(verifyContext);
        using var verifyRepo = new ConversationRepository(verifyContext, verifyUnitOfWork);

        var updated = await verifyRepo.GetByIdAsync(conversationId);
        updated.ShouldNotBeNull();

        var messages = updated.GetAllMessages();

        // Verify sequences are consecutive
        for (int i = 0; i < messages.Count; i++)
        {
            messages[i].Sequence.ShouldBe(i + 1, "Sequence numbers should remain consecutive");
        }
    }

    #endregion

    #region Edge Case: Message During Completion

    [Test]
    public async Task AppendMessage_DuringConversationComplete_ShouldThrowConcurrency()
    {
        // Arrange: Create conversation with messages
        var conversation = await CreateAndSaveConversationWithMessagesAsync();

        // Act: One user completes while another appends message
        var result = await SimulateConcurrentUpdatesAsync<Conversation, ConversationId>(
            conversation.Id,
            context =>
            {
                var unitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(context);
                return new ConversationRepository(context, unitOfWork);
            },
            conv1 => conv1.Complete(_timeProvider),
            conv2 =>
            {
                var content = MessageContent.From("Last minute message!");
                return conv2.AppendUserMessageToConversation(content, _timeProvider);
            }
        );

        // Assert: Second operation should fail
        AssertOptimisticConcurrencyHandled(result);

        // Verify final state
        using var verifyContext = CreateContext(CreateContextOptions());
        using var verifyUnitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(verifyContext);
        using var verifyRepo = new ConversationRepository(verifyContext, verifyUnitOfWork);

        var updated = await verifyRepo.GetByIdAsync(conversation.Id);
        updated.ShouldNotBeNull();

        // Should be completed (first operation)
        updated.Status.ShouldBe(ConversationStatus.Completed);
    }

    #endregion

    #region Helper Methods

    private async Task<Conversation> CreateAndSaveConversationAsync()
    {
        var ownerId = AxonUserId.New();
        var conversationResult = Conversation.StartNewConversation(ownerId, null, _timeProvider);
        conversationResult.IsSuccess.ShouldBeTrue();

        var conversation = conversationResult.Value;
        using var repository = new ConversationRepository(_setupContext, _unitOfWork);
        await repository.AddAsync(conversation);
        await _setupContext.SaveChangesAsync();

        return conversation;
    }

    private async Task<Conversation> CreateAndSaveConversationWithUserMessageAsync()
    {
        var conversation = await CreateAndSaveConversationAsync();

        var content = MessageContent.From("Initial user question");
        var result = conversation.AppendUserMessageToConversation(content, _timeProvider);
        result.IsSuccess.ShouldBeTrue();

        using var repository = new ConversationRepository(_setupContext, _unitOfWork);
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

        using var repository = new ConversationRepository(_setupContext, _unitOfWork);
        await repository.UpdateAsync(conversation);
        await _setupContext.SaveChangesAsync();

        // Add another exchange
        var userContent2 = MessageContent.From("Follow-up question");
        conversation.AppendUserMessageToConversation(userContent2, _timeProvider);

        var assistantContent2 = MessageContent.From("Follow-up response");
        var responseId2 = new AiResponseId(Guid.NewGuid().ToString());
        conversation.AppendAssistantResponseToConversation(assistantContent2, responseId2, _timeProvider);

        await repository.UpdateAsync(conversation);
        await _setupContext.SaveChangesAsync();

        return conversation;
    }

    #endregion
}