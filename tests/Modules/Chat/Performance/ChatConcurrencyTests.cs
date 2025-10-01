using Axon.BuildingBlocks.Core.Primitives.ValueObjects;
using Axon.Modules.Chat.Application.Common.Models;
using Axon.Modules.Chat.Domain.Aggregates.Conversation;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Modules.Chat.Infrastructure.Persistence.DbContexts;
using Axon.Modules.Chat.Infrastructure.Persistence.Repositories;
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

namespace Axon.Modules.Chat.Performance;

/// <summary>
/// Database-level concurrency tests for Chat module.
/// Tests concurrent message processing, conversation creation, and transaction isolation
/// using real PostgreSQL database via testcontainers.
/// </summary>
[TestFixture]
public class ChatConcurrencyTests : ConcurrencyTestBase<ChatDbContext>
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
        throw new NotImplementedException("Use CreateAndSaveConversationAsync");
    }

    #region Helper Methods

    private async Task<Conversation> CreateAndSaveConversationAsync(string? title = null)
    {
        var ownerId = new AxonUserId(Guid.NewGuid());
        var result = Conversation.StartNewConversation(
            ownerId,
            title ?? "Test Conversation",
            _timeProvider);

        result.IsSuccess.ShouldBeTrue();

        _setupContext.Set<Conversation>().Add(result.Value);
        await _setupContext.SaveChangesAsync();

        return result.Value;
    }

    private async Task<Conversation> CreateConversationWithMessageAsync(string messageContent = "Initial message")
    {
        var conversation = await CreateAndSaveConversationAsync();

        var content = MessageContent.From(messageContent);
        var result = conversation.AppendUserMessageToConversation(content, _timeProvider);
        result.IsSuccess.ShouldBeTrue();

        using var repository = new ConversationRepository(_setupContext, _unitOfWork);
        await repository.UpdateAsync(conversation);
        await _setupContext.SaveChangesAsync();

        return conversation;
    }

    #endregion

    #region Concurrent Message Processing Tests

    [Test]
    public async Task ConcurrentMessageAppending_TwoUsers_ShouldThrowConcurrencyException()
    {
        // Arrange - Create empty conversation (no initial message)
        var conversation = await CreateAndSaveConversationAsync();

        // Act & Assert - Simulate two users appending messages concurrently
        var result = await SimulateConcurrentUpdatesAsync<Conversation, ConversationId>(
            conversation.Id,
            context =>
            {
                var unitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(context);
                return new ConversationRepository(context, unitOfWork);
            },
            conv1 =>
            {
                var content = MessageContent.From("User 1: Concurrent message");
                return conv1.AppendUserMessageToConversation(content, _timeProvider);
            },
            conv2 =>
            {
                var content = MessageContent.From("User 2: Concurrent message");
                return conv2.AppendUserMessageToConversation(content, _timeProvider);
            }
        );

        AssertOptimisticConcurrencyHandled(result);
    }

    [Test]
    public async Task ConcurrentUpdates_DifferentConversations_ShouldSucceed()
    {
        // Arrange - Create three separate empty conversations
        var conv1 = await CreateAndSaveConversationAsync("Conv 1");
        var conv2 = await CreateAndSaveConversationAsync("Conv 2");
        var conv3 = await CreateAndSaveConversationAsync("Conv 3");

        // Act - Update all three conversations concurrently (should NOT conflict)
        var tasks = new List<Task>();

        tasks.Add(Task.Run(async () =>
        {
            using var context = CreateContext(CreateContextOptions());
            var unitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(context);
            var repo = new ConversationRepository(context, unitOfWork);

            var conversation = await repo.GetByIdAsync(conv1.Id);
            var result = conversation!.AppendUserMessageToConversation(
                MessageContent.From("Additional message 1"), _timeProvider);
            result.IsSuccess.ShouldBeTrue();
            await repo.UpdateAsync(conversation);
            await unitOfWork.SaveChangesAsync();
        }));

        tasks.Add(Task.Run(async () =>
        {
            using var context = CreateContext(CreateContextOptions());
            var unitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(context);
            var repo = new ConversationRepository(context, unitOfWork);

            var conversation = await repo.GetByIdAsync(conv2.Id);
            var result = conversation!.AppendUserMessageToConversation(
                MessageContent.From("Additional message 2"), _timeProvider);
            result.IsSuccess.ShouldBeTrue();
            await repo.UpdateAsync(conversation);
            await unitOfWork.SaveChangesAsync();
        }));

        tasks.Add(Task.Run(async () =>
        {
            using var context = CreateContext(CreateContextOptions());
            var unitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(context);
            var repo = new ConversationRepository(context, unitOfWork);

            var conversation = await repo.GetByIdAsync(conv3.Id);
            var result = conversation!.AppendUserMessageToConversation(
                MessageContent.From("Additional message 3"), _timeProvider);
            result.IsSuccess.ShouldBeTrue();
            await repo.UpdateAsync(conversation);
            await unitOfWork.SaveChangesAsync();
        }));

        // Assert - All should succeed (no conflicts)
        await Task.WhenAll(tasks);

        // Verify each conversation was updated with a fresh context
        using var verifyContext = CreateContext(CreateContextOptions());
        var updated1 = await verifyContext.Set<Conversation>().FindAsync(conv1.Id);
        var updated2 = await verifyContext.Set<Conversation>().FindAsync(conv2.Id);
        var updated3 = await verifyContext.Set<Conversation>().FindAsync(conv3.Id);

        updated1!.GetMessageCount().ShouldBe(1);
        updated2!.GetMessageCount().ShouldBe(1);
        updated3!.GetMessageCount().ShouldBe(1);
    }

    [Test]
    public async Task ConcurrentTitleUpdate_TwoUsers_ShouldThrowConcurrencyException()
    {
        // Arrange - Create conversation
        var conversation = await CreateAndSaveConversationAsync("Original Title");

        // Act & Assert - Simulate two users updating title concurrently
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
    }

    [Test]
    public async Task ConcurrentCompletion_TwoUsers_ShouldThrowConcurrencyException()
    {
        // Arrange - Create conversation with a message (needed to complete)
        var conversation = await CreateConversationWithMessageAsync();

        // Act & Assert - Simulate two users completing conversation concurrently
        var result = await SimulateConcurrentUpdatesAsync<Conversation, ConversationId>(
            conversation.Id,
            context =>
            {
                var unitOfWork = new EfUnitOfWork<ChatDbContext, ChatModule>(context);
                return new ConversationRepository(context, unitOfWork);
            },
            conv1 => conv1.Complete(_timeProvider),
            conv2 => conv2.Complete(_timeProvider)
        );

        AssertOptimisticConcurrencyHandled(result);
    }

    #endregion
}
