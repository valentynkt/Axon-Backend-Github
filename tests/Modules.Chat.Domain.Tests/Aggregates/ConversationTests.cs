using Axon.Modules.Chat.Domain.Aggregates;
using Axon.Modules.Chat.Domain.ValueObjects;
using Axon.Modules.Chat.Domain.Events;

namespace Axon.Modules.Chat.Domain.Tests.Aggregates;

[TestFixture]
[Category("Unit")]
[Category("Domain")]
[Category("Aggregate")]
public sealed class ConversationTests
{
    #region Start Tests

    [Test]
    public void Start_WithValidOwner_ShouldCreateActiveConversation()
    {
        // Arrange
        var conversationId = ConversationId.New();
        var ownerId = UserId.New();

        // Act
        var result = Conversation.Start(conversationId, ownerId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var conversation = result.Value;
        conversation.Id.ShouldBe(conversationId);
        conversation.OwnerId.ShouldBe(ownerId);
        conversation.Status.ShouldBe(ConversationStatus.Active);
        conversation.IsDefaultTitle.ShouldBeTrue();
        conversation.MessagesCount.ShouldBe(0);
        conversation.NextSequence.ShouldBe(1);
        conversation.LastAuthor.ShouldBeNull();
    }

    [Test]
    public void Start_WithNullOwner_ShouldReturnFailure()
    {
        // Arrange
        var conversationId = ConversationId.New();

        // Act
        var result = Conversation.Start(conversationId, null!);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CHAT.CONVERSATION.OWNER_REQUIRED");
    }

    [Test]
    public void Start_ShouldRaiseConversationStartedEvent()
    {
        // Arrange
        var conversationId = ConversationId.New();
        var ownerId = UserId.New();
        var title = ConversationTitle.Create("Test Chat").Value;

        // Act
        var result = Conversation.Start(conversationId, ownerId, title);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var events = result.Value.DomainEvents;
        events.ShouldContain(e => e is ConversationStartedEvent);
        
        var startedEvent = events.OfType<ConversationStartedEvent>().First();
        startedEvent.ConversationId.ShouldBe(conversationId);
        startedEvent.OwnerId.ShouldBe(ownerId);
        startedEvent.Title.ShouldBe(title);
        startedEvent.IsDefaultTitle.ShouldBeFalse();
    }

    #endregion

    #region AddUserMessage Tests

    [Test]
    public void AddUserMessage_ToActiveConversation_ShouldSucceed()
    {
        // Arrange
        var conversation = CreateConversation();
        var content = MessageContent.Create("Hello").Value;

        // Act
        var result = conversation.AddUserMessage(content);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        conversation.MessagesCount.ShouldBe(1);
        conversation.NextSequence.ShouldBe(2);
        conversation.LastAuthor.ShouldBe(MessageRole.User);
    }

    [Test]
    public void AddUserMessage_ToCompletedConversation_ShouldFail()
    {
        // Arrange
        var conversation = CreateConversation();
        conversation.Complete();
        var content = MessageContent.Create("Hello").Value;

        // Act
        var result = conversation.AddUserMessage(content);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CHAT.CONVERSATION.NOT_ACTIVE");
    }

    #endregion

    #region AddAssistantMessage Tests

    [Test]
    public void AddAssistantMessage_AsFirstMessage_ShouldSucceed()
    {
        // Arrange - Assistant CAN be first per spec
        var conversation = CreateConversation();
        var content = MessageContent.Create("Hello, how can I help?").Value;

        // Act
        var result = conversation.AddAssistantMessage(content);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        conversation.MessagesCount.ShouldBe(1);
        conversation.LastAuthor.ShouldBe(MessageRole.Assistant);
    }

    [Test]
    public void AddAssistantMessage_AfterUser_ShouldSucceed()
    {
        // Arrange
        var conversation = CreateConversation();
        conversation.AddUserMessage(MessageContent.Create("Hello").Value);
        var content = MessageContent.Create("Hi there!").Value;

        // Act
        var result = conversation.AddAssistantMessage(content);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        conversation.MessagesCount.ShouldBe(2);
    }

    [Test]
    public void AddAssistantMessage_AfterAssistant_ShouldFail()
    {
        // Arrange
        var conversation = CreateConversation();
        conversation.AddAssistantMessage(MessageContent.Create("Hello").Value);
        var content = MessageContent.Create("Hello again").Value;

        // Act
        var result = conversation.AddAssistantMessage(content);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CHAT.MESSAGE.TURN_TAKING_VIOLATION");
    }

    #endregion

    #region Message Limit Tests

    [Test]
    public void AddMessage_WhenAtLimit_ShouldFail()
    {
        // Arrange
        var conversation = CreateConversationWithMessages(9_999);
        var content = MessageContent.Create("One more").Value;

        // Act - Try to add 10,000th message
        var result = conversation.AddUserMessage(content);

        // Assert
        result.IsSuccess.ShouldBeTrue(); // 10,000th is ok
        conversation.MessagesCount.ShouldBe(10_000);

        // Try 10,001st
        var result2 = conversation.AddUserMessage(content);
        result2.IsFailure.ShouldBeTrue();
        result2.Error.Code.ShouldBe("CHAT.CONVERSATION.MESSAGE_LIMIT_EXCEEDED");
    }

    #endregion

    #region SetTitle Tests

    [Test]
    public void SetTitle_WithValidTitle_ShouldSucceed()
    {
        // Arrange
        var conversation = CreateConversation();
        var title = ConversationTitle.Create("New Title").Value;

        // Act
        var result = conversation.SetTitle(title);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        conversation.Title.ShouldBe(title);
        conversation.IsDefaultTitle.ShouldBeFalse();
    }

    [Test]
    public void SetTitle_OnCompletedConversation_ShouldFail()
    {
        // Arrange
        var conversation = CreateConversation();
        conversation.Complete();
        var title = ConversationTitle.Create("New Title").Value;

        // Act
        var result = conversation.SetTitle(title);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CHAT.CONVERSATION.NOT_ACTIVE");
    }

    #endregion

    #region Complete/Reopen Tests

    [Test]
    public void Complete_ActiveConversation_ShouldSucceed()
    {
        // Arrange
        var conversation = CreateConversation();

        // Act
        var result = conversation.Complete();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        conversation.Status.ShouldBe(ConversationStatus.Completed);
    }

    [Test]
    public void Reopen_CompletedConversation_ShouldSucceed()
    {
        // Arrange
        var conversation = CreateConversation();
        conversation.Complete();

        // Act
        var result = conversation.Reopen();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        conversation.Status.ShouldBe(ConversationStatus.Active);
    }

    [Test]
    public void Reopen_ActiveConversation_ShouldFail()
    {
        // Arrange
        var conversation = CreateConversation();

        // Act
        var result = conversation.Reopen();

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("CHAT.CONVERSATION.ALREADY_ACTIVE");
    }

    #endregion

    #region Sequence Tests

    [Test]
    public void Messages_ShouldHaveSequentialSequenceNumbers()
    {
        // Arrange
        var conversation = CreateConversation();

        // Act
        for (int i = 1; i <= 5; i++)
        {
            conversation.AddUserMessage(MessageContent.Create($"Message {i}").Value);
        }

        // Assert
        conversation.Messages.Count.ShouldBe(5);
        for (int i = 0; i < 5; i++)
        {
            conversation.Messages[i].Sequence.ShouldBe(i + 1);
        }
    }

    #endregion

    #region Domain Events Tests

    [Test]
    public void AddMessage_ShouldRaiseMessageAddedEvent()
    {
        // Arrange
        var conversation = CreateConversation();
        var content = MessageContent.Create("Test message").Value;

        // Act
        conversation.AddUserMessage(content);

        // Assert
        var events = conversation.DomainEvents;
        events.ShouldContain(e => e is MessageAddedEvent);

        var messageEvent = events.OfType<MessageAddedEvent>().Last();
        messageEvent.ConversationId.ShouldBe(conversation.Id);
        messageEvent.Sequence.ShouldBe(1);
        messageEvent.Role.ShouldBe(MessageRole.User);
        messageEvent.ContentLength.ShouldBe(content.Length);
    }

    #endregion

    #region Helper Methods

    private static Conversation CreateConversation()
    {
        var result = Conversation.Start(
            ConversationId.New(),
            UserId.New());
        
        return result.Value;
    }

    private static Conversation CreateConversationWithMessages(int count)
    {
        var conversation = CreateConversation();
        
        for (int i = 0; i < count; i++)
        {
            // Alternate between user and assistant to avoid turn-taking violations
            if (i % 2 == 0)
                conversation.AddUserMessage(MessageContent.Create($"User message {i}").Value);
            else
                conversation.AddAssistantMessage(MessageContent.Create($"Assistant message {i}").Value);
        }
        
        return conversation;
    }

    #endregion
}